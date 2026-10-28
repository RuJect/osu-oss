// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Runtime.InteropServices;

namespace osu.Game.Auth.Redirect
{
    /// <summary>
    /// Sets environment variables in the process' native environment.
    /// </summary>
    /// <remarks>
    /// On Unix, <see cref="Environment.SetEnvironmentVariable(string,string)"/> does not
    /// update the native <c>environ</c> block, so it is not visible to native code via
    /// <c>getenv</c> (e.g. the LD_PRELOAD measurement redirect shim). This helper calls
    /// libc directly so native code observes the value.
    /// </remarks>
    internal static class NativeEnvironment
    {
        [DllImport("libc", SetLastError = true)]
        private static extern int setenv(string name, string value, int overwrite);

        public static void Set(string name, string value)
        {
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                setenv(name, value, 1);
            else
                Environment.SetEnvironmentVariable(name, value);
        }
    }
}
