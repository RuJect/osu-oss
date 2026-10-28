// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Logging;

namespace osu.Game.Auth.Isolation
{
    /// <summary>
    /// Linux isolation: a process-wide <c>SECCOMP_RET_USER_NOTIF</c> filter plus a separate
    /// supervisor process that attributes notified syscalls to the proprietary module by
    /// instruction pointer.
    /// </summary>
    /// <remarks>
    /// The supervisor is spawned (unfiltered) first, then the filter is installed with
    /// <c>TSYNC</c> so existing threads are covered, and finally the listener fd is handed over as
    /// an <c>SCM_RIGHTS</c> ancillary message. The proprietary module's mappings cannot be known at
    /// install time, so they are published by polling <c>/proc/self/maps</c> (a small fail-open
    /// window while the mappings are being published).
    /// </remarks>
    internal sealed class LinuxSeccompBackend : IIsolationBackend
    {
        private static readonly Logger logger = Logger.GetLogger("auth");

        private readonly IsolationOptions options;

        private IntPtr ctx = IntPtr.Zero;
        private int ctrlFd = -1;
        private Process? supervisor;
        private CancellationTokenSource? rangeCts;

        public LinuxSeccompBackend(IsolationOptions options)
        {
            this.options = options;
        }

        public bool IsSupported => OperatingSystem.IsLinux();

        public void Start()
        {
            if (alreadyFiltered())
            {
                logger.Add("Auth blob isolation: process is already under seccomp; skipping install.");
                return;
            }

            string supervisorPath = resolveSupervisor();

            if (!File.Exists(supervisorPath))
            {
                logger.Add($"Auth blob isolation: supervisor not found at {supervisorPath}; disabled.");
                return;
            }

            uint api = Seccomp.seccomp_api_get();

            if (api < Seccomp.API_TSYNC_AND_NOTIFY)
            {
                logger.Add($"Auth blob isolation: libseccomp API {api} lacks TSYNC+notify support; disabled.");
                return;
            }

            IsolationPolicy.Resolve();

            int[] sv = new int[2];

            if (Libc.socketpair(Libc.AF_UNIX, Libc.SOCK_SEQPACKET, 0, sv) != 0)
            {
                logger.Add($"Auth blob isolation: socketpair failed (errno {Marshal.GetLastPInvokeError()}).");
                return;
            }

            int parentFd = sv[0];
            int childFd = sv[1];

            // The parent end must not leak into later children; the child end must be inherited by
            // the supervisor (cleared FD_CLOEXEC).
            Libc.fcntl(parentFd, Libc.F_SETFD, Libc.FD_CLOEXEC);
            Libc.fcntl(childFd, Libc.F_SETFD, 0);

            try
            {
                supervisor = spawn(supervisorPath, childFd);
            }
            finally
            {
                Libc.close(childFd);
            }

            ctrlFd = parentFd;

            int listener = installFilter();

            if (listener < 0)
            {
                Dispose();
                return;
            }

            if (!Libc.SendFileDescriptor(ctrlFd, listener))
            {
                logger.Add($"Auth blob isolation: failed to hand listener to supervisor (errno {Marshal.GetLastPInvokeError()}).");
                Libc.close(listener);
                Dispose();
                return;
            }

            Libc.close(listener);

            startRangePublisher();
        }

        private Process spawn(string path, int childFd)
        {
            var psi = new ProcessStartInfo(path)
            {
                UseShellExecute = false,
            };

            psi.ArgumentList.Add(childFd.ToString(CultureInfo.InvariantCulture));
            psi.ArgumentList.Add(Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
            psi.ArgumentList.Add("net=" + (options.AllowNetwork ? "allow" : "deny"));
            psi.ArgumentList.Add("audit=" + (options.Audit ? "1" : "0"));

            if (!string.IsNullOrEmpty(options.LogPath))
                psi.ArgumentList.Add("log=" + options.LogPath);

            // The supervisor must not preload the redirect shim (it would recursively spawn another
            // supervisor) and must not re-enter the isolation setup.
            psi.Environment.Remove("LD_PRELOAD");
            psi.Environment.Remove("LD_AUDIT");
            psi.Environment.Remove("AUTHISOLATE");

            return Process.Start(psi) ?? throw new InvalidOperationException("Process.Start returned null.");
        }

        private int installFilter()
        {
            ctx = Seccomp.seccomp_init(Seccomp.ACT_ALLOW);

            if (ctx == IntPtr.Zero)
            {
                logger.Add("Auth blob isolation: seccomp_init failed.");
                return -1;
            }

            if (Seccomp.seccomp_attr_set(ctx, Seccomp.FLTATTR_CTL_NNP, 1) != 0)
                logger.Add("Auth blob isolation: could not set NNP attribute.");

            if (Seccomp.seccomp_attr_set(ctx, Seccomp.FLTATTR_CTL_TSYNC, 1) != 0)
                logger.Add("Auth blob isolation: could not set TSYNC attribute.");

            int added = 0;

            foreach (int nr in IsolationPolicy.NotifySyscalls)
            {
                if (nr < 0)
                    continue;

                if (Seccomp.seccomp_rule_add(ctx, Seccomp.ACT_NOTIFY, nr, 0) == 0)
                    added++;
                else
                    logger.Add($"Auth blob isolation: could not add rule for syscall {nr}.");
            }

            int load = Seccomp.seccomp_load(ctx);

            if (load != 0)
            {
                logger.Add($"Auth blob isolation: seccomp_load failed (errno {-load}).");
                Seccomp.seccomp_release(ctx);
                ctx = IntPtr.Zero;
                return -1;
            }

            int fd = Seccomp.seccomp_notify_fd(ctx);
            logger.Add($"Auth blob isolation active ({added} syscalls, mode={(options.Audit ? "audit" : "enforce")}).");
            return fd;
        }

        private void startRangePublisher()
        {
            rangeCts = new CancellationTokenSource();
            CancellationToken token = rangeCts.Token;

            Task.Run(async () =>
            {
                // ~2 minutes should comfortably outlast the module's load-and-init.
                for (int i = 0; i < 1200 && !token.IsCancellationRequested; i++)
                {
                    if (publishRanges("AuthNative"))
                        return;

                    try
                    {
                        await Task.Delay(100, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
            });
        }

        private bool publishRanges(string needle)
        {
            if (ctrlFd < 0)
                return false;

            string[] lines;

            try
            {
                lines = File.ReadAllLines("/proc/self/maps");
            }
            catch
            {
                return false;
            }

            int sent = 0;

            unsafe
            {
                foreach (string line in lines)
                {
                    if (!line.Contains(needle, StringComparison.Ordinal))
                        continue;

                    int dash = line.IndexOf('-');
                    int space = dash < 0 ? -1 : line.IndexOf(' ', dash);

                    if (dash <= 0 || space < 0)
                        continue;

                    if (!ulong.TryParse(line.AsSpan(0, dash), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong start))
                        continue;

                    if (!ulong.TryParse(line.AsSpan(dash + 1, space - dash - 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong end))
                        continue;

                    var message = new ControlProtocol.RangeMessage
                    {
                        Magic = ControlProtocol.RANGE_MAGIC,
                        Base = start,
                        End = end,
                    };

                    if (!Libc.SendMessage(ctrlFd, &message, (nuint)sizeof(ControlProtocol.RangeMessage)))
                        break;

                    sent++;
                }
            }

            if (sent > 0)
            {
                logger.Add($"Auth blob isolation: published {sent} mapping range(s) for {needle}.");
                return true;
            }

            return false;
        }

        private static bool alreadyFiltered()
        {
            try
            {
                foreach (string line in File.ReadLines("/proc/self/status"))
                {
                    if (line.StartsWith("Seccomp:", StringComparison.Ordinal))
                        return line[8..].Trim() == "2"; // 2 = SECCOMP_MODE_FILTER
                }
            }
            catch
            {
                // treat as unfiltered.
            }

            return false;
        }

        private static string resolveSupervisor()
        {
            string? @override = Environment.GetEnvironmentVariable("AUTHSUPERVISOR");

            if (!string.IsNullOrEmpty(@override))
                return Path.GetFullPath(@override);

            string name = OperatingSystem.IsWindows() ? "osu-auth-supervisor.exe" : "osu-auth-supervisor";
            return Path.Combine(AppContext.BaseDirectory, name);
        }

        public void Dispose()
        {
            rangeCts?.Cancel();
            rangeCts?.Dispose();
            rangeCts = null;

            if (ctrlFd >= 0)
            {
                Libc.close(ctrlFd);
                ctrlFd = -1;
            }

            if (ctx != IntPtr.Zero)
            {
                Seccomp.seccomp_release(ctx);
                ctx = IntPtr.Zero;
            }

            if (supervisor != null)
            {
                try
                {
                    if (!supervisor.HasExited)
                    {
                        supervisor.Kill();
                        supervisor.WaitForExit(1000);
                    }
                }
                catch
                {
                    // best effort.
                }

                supervisor.Dispose();
                supervisor = null;
            }
        }
    }
}
