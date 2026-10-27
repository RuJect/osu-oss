// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using osu.Framework.Logging;

namespace osu.Game.Auth
{
    /// <summary>
    /// Makes the measurement redirect shim transparent on Linux/macOS.
    /// </summary>
    /// <remarks>
    /// The shim (<c>authredirect</c>) must be present when the process starts, because it
    /// interposes libc file APIs that are already bound by the time managed code runs. To
    /// avoid requiring a manual prelude, this re-executes the current process with
    /// <c>LD_PRELOAD</c> / <c>DYLD_INSERT_LIBRARIES</c> pointing at the shim. A marker is set
    /// in the child environment so the re-exec happens at most once.
    /// <para>
    /// Set <c>OSU_AUTH_NO_REEXEC=1</c> to opt out (e.g. when attaching a debugger); the
    /// shim may then be preloaded by hand. If the shim is unavailable, signing is disabled.
    /// </para>
    /// </remarks>
    public static class RedirectBootstrap
    {
        private const string disable_env = "OSU_AUTH_NO_REEXEC";

        /// <summary>
        /// Re-executes the process with the redirect shim preloaded when required,
        /// returning only if no re-exec is necessary (or if it failed).
        /// </summary>
        public static void EnsurePreloaded()
        {
            if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
                return;

            if (Environment.GetEnvironmentVariable(RedirectShim.ACTIVE_ENV) == "1")
                return;

            string shim = RedirectShim.ResolvePath();

            if (!File.Exists(shim))
            {
                Logger.Log($"Auth redirect shim not found at {shim}; signing disabled.", LoggingTarget.Runtime, LogLevel.Verbose);
                return;
            }

            if (Environment.GetEnvironmentVariable(disable_env) == "1")
            {
                Logger.Log($"{disable_env} is set; not re-executing with the auth redirect shim.", LoggingTarget.Runtime, LogLevel.Verbose);
                return;
            }

            reexec(shim);
        }

        private static void reexec(string shim)
        {
            bool macos = OperatingSystem.IsMacOS();
            string loaderVar = macos ? "DYLD_INSERT_LIBRARIES" : "LD_PRELOAD";
            string separator = macos ? ":" : " ";

            var env = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                if (entry.Key is string key && entry.Value is string value)
                    env[key] = value;
            }

            env.TryGetValue(loaderVar, out string? existing);

            if (existing != null && existing.Contains(RedirectShim.SHIM_NAME, StringComparison.Ordinal))
                return;

            env[loaderVar] = string.IsNullOrEmpty(existing) ? shim : shim + separator + existing;
            env[RedirectShim.ACTIVE_ENV] = "1";

            string? processPath = Environment.ProcessPath;

            if (string.IsNullOrEmpty(processPath))
            {
                Logger.Log("Could not resolve the process path; not re-executing with the auth redirect shim.", LoggingTarget.Runtime, LogLevel.Verbose);
                return;
            }

            Logger.Log($"Re-executing with auth redirect shim {shim} preloaded.", LoggingTarget.Runtime, LogLevel.Verbose);

            int error = exec(processPath, getArgv(), env.Select(kv => $"{kv.Key}={kv.Value}").ToArray());

            // execve only returns on failure.
            Logger.Log($"Failed to re-execute with the auth redirect shim (errno {error}); continuing without it.", LoggingTarget.Runtime, LogLevel.Verbose);
        }

        /// <summary>Returns the process' own argv, preserving the original <c>argv[0]</c>.</summary>
        private static string[] getArgv()
        {
            if (OperatingSystem.IsLinux())
            {
                try
                {
                    string raw = Encoding.UTF8.GetString(File.ReadAllBytes("/proc/self/cmdline"));

                    if (raw.EndsWith('\0'))
                        raw = raw[..^1];

                    if (raw.Length > 0)
                        return raw.Split('\0');
                }
                catch
                {
                    // fall through to the managed command line.
                }
            }

            return Environment.GetCommandLineArgs();
        }

        private static int exec(string path, string[] argv, string[] envp)
        {
            IntPtr[] argvPtr = toNative(argv);
            IntPtr[] envpPtr = toNative(envp);

            try
            {
                execve(path, argvPtr, envpPtr);
                return Marshal.GetLastPInvokeError();
            }
            finally
            {
                free(argvPtr);
                free(envpPtr);
            }
        }

        private static IntPtr[] toNative(string[] values)
        {
            var result = new IntPtr[values.Length + 1];

            for (int i = 0; i < values.Length; i++)
                result[i] = Marshal.StringToCoTaskMemUTF8(values[i]);

            return result;
        }

        private static void free(IntPtr[] values)
        {
            foreach (IntPtr value in values)
            {
                if (value != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(value);
            }
        }

        [DllImport("libc", SetLastError = true)]
        private static extern int execve(string path, IntPtr[] argv, IntPtr[] envp);
    }
}
