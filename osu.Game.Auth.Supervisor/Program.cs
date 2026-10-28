// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Runtime.InteropServices;

namespace osu.Game.Auth.Isolation.Supervisor
{
    /// <summary>
    /// Seccomp user-notification supervisor. Spawned by the game before the filter is installed; it
    /// receives the listener fd over the inherited control socket, then decides every notified
    /// syscall based on whether the faulting instruction pointer lies inside a published
    /// proprietary-module range.
    /// </summary>
    /// <remarks>
    /// Usage: <c>osu-auth-supervisor &lt;ctrl_fd&gt; &lt;target_pid&gt; [net=allow|deny] [audit=0|1] [log=PATH]</c>
    /// </remarks>
    internal static class Program
    {
        private const int eintr = 4;
        private const int enoent = 2;

        private static readonly object log_lock = new object();

        private static TextWriter? logWriter;
        private static bool audit;
        private static bool net_deny = true;

        private static int Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("usage: osu-auth-supervisor <ctrl_fd> <target_pid> [net=allow|deny] [audit=0|1] [log=PATH]");
                return 2;
            }

            int ctrl = int.Parse(args[0]);
            int targetPid = int.Parse(args[1]);

            for (int i = 2; i < args.Length; i++)
            {
                string arg = args[i];

                if (arg.StartsWith("net=", StringComparison.Ordinal))
                    net_deny = !string.Equals(arg[4..], "allow", StringComparison.OrdinalIgnoreCase);
                else if (arg.StartsWith("audit=", StringComparison.Ordinal))
                    audit = arg[6..] != "0";
                else if (arg.StartsWith("log=", StringComparison.Ordinal))
                {
                    try
                    {
                        logWriter = new StreamWriter(arg[4..], append: true) { AutoFlush = true };
                    }
                    catch
                    {
                        // fall back to stderr.
                    }
                }
            }

            IsolationPolicy.Resolve();

            int listener = Libc.ReceiveFileDescriptor(ctrl);

            if (listener < 0)
            {
                log("did not receive listener fd; exiting");
                return 1;
            }

            log($"supervisor started (target={targetPid}, net={(net_deny ? "deny" : "allow")}, mode={(audit ? "audit" : "enforce")})");

            if (Seccomp.seccomp_notify_alloc(out IntPtr req, out IntPtr resp) != 0)
            {
                log("seccomp_notify_alloc failed; exiting");
                return 1;
            }

            var ranges = new RangeTable();

            unsafe
            {
                var fds = stackalloc Libc.PollFd[2];
                fds[0] = new Libc.PollFd { Fd = listener, Events = Libc.POLLIN };
                fds[1] = new Libc.PollFd { Fd = ctrl, Events = Libc.POLLIN };

                while (true)
                {
                    int r = Libc.poll(fds, 2, -1);

                    if (r < 0)
                    {
                        int err = Marshal.GetLastPInvokeError();

                        if (err == eintr)
                            continue;

                        log($"poll failed (errno={err}); exiting");
                        break;
                    }

                    if ((fds[0].Revents & Libc.POLLIN) != 0 && !handleNotify(listener, req, resp, ranges))
                        break;

                    if ((fds[1].Revents & (Libc.POLLIN | Libc.POLLHUP)) != 0 && !handleControl(ctrl, ranges))
                        break;
                }
            }

            Seccomp.seccomp_notify_free(req, resp);
            return 0;
        }

        private static bool handleNotify(int listener, IntPtr reqPtr, IntPtr respPtr, RangeTable ranges)
        {
            // SECCOMP_IOCTL_NOTIF_RECV expects a zeroed request struct; a leftover id from the
            // previous notification makes the kernel reject it with EINVAL.
            unsafe
            {
                new Span<byte>((void*)reqPtr, sizeof(SeccompNotif)).Clear();
            }

            int rc = Seccomp.seccomp_notify_receive(listener, reqPtr);

            if (rc < 0)
            {
                if (-rc == eintr)
                    return true;

                // ENOENT means the target task is gone.
                log($"notif recv failed (rc={rc}, errno={Marshal.GetLastPInvokeError()}); exiting");
                return false;
            }

            var notif = Marshal.PtrToStructure<SeccompNotif>(reqPtr);
            bool allow = decide(in notif, ranges);
            bool wouldDeny = !allow;

            if (!allow && audit)
                allow = true;

            var resp = new SeccompNotifResp { Id = notif.Id };

            if (allow)
            {
                resp.Flags = Seccomp.USER_NOTIF_FLAG_CONTINUE;
                resp.Error = 0;
                resp.Val = 0;
            }
            else
            {
                resp.Flags = 0;
                resp.Error = -1; // -EPERM
                resp.Val = 0;
            }

            Marshal.StructureToPtr(resp, respPtr, false);

            if (Seccomp.seccomp_notify_respond(listener, respPtr) < 0)
            {
                int err = Marshal.GetLastPInvokeError();

                if (err == enoent)
                {
                    log("target gone while replying; exiting");
                    return false;
                }

                log($"notif send failed (errno={err})");
            }
            else
                log($"{(wouldDeny ? (audit ? "WOULD-DENY" : "DENY") : "allow")} nr={notif.Data.Nr} ip=0x{notif.Data.InstructionPointer:x}");

            return true;
        }

        private static bool handleControl(int ctrl, RangeTable ranges)
        {
            const int range_message_size = 24; // ControlProtocol.RangeMessage (two u32 + two u64)
            Span<byte> buffer = stackalloc byte[range_message_size];
            int n;

            unsafe
            {
                fixed (byte* p = buffer)
                    n = (int)Libc.recv(ctrl, p, (nuint)buffer.Length, 0);
            }

            if (n == 0)
            {
                log("control channel closed; exiting");
                return false;
            }

            if (n < 0)
            {
                int err = Marshal.GetLastPInvokeError();
                return err == eintr;
            }

            if (n >= range_message_size)
            {
                var message = MemoryMarshal.Read<ControlProtocol.RangeMessage>(buffer);

                if (message.Magic == ControlProtocol.RANGE_MAGIC)
                {
                    ranges.Add(message.Base, message.End);
                    log($"published range [0x{message.Base:x},0x{message.End:x})");
                }
            }

            return true;
        }

        private static bool decide(in SeccompNotif notif, RangeTable ranges)
        {
            if (notif.Data.Arch != IsolationPolicy.AUDIT_ARCH_X86_64)
                return true;

            if (!ranges.Contains(notif.Data.InstructionPointer))
                return true;

            int nr = notif.Data.Nr;

            if (nr == IsolationPolicy.CloneSyscall)
                return (notif.Data.Arg0 & IsolationPolicy.CLONE_THREAD) != 0;

            // clone3 (struct in target memory) cannot be inspected here; allow it (documented gap).
            if (nr == IsolationPolicy.Clone3Syscall)
                return true;

            if (IsolationPolicy.IsNetwork(nr))
                return !net_deny;

            return false;
        }

        private static void log(string message)
        {
            lock (log_lock)
            {
                if (logWriter != null)
                    logWriter.WriteLine($"[supervisor {Environment.ProcessId}] {message}");
                else
                    Console.Error.WriteLine($"[supervisor {Environment.ProcessId}] {message}");
            }
        }
    }
}
