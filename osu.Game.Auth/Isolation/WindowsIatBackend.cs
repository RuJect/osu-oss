// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Logging;
using osu.Game.Auth.Redirect;

namespace osu.Game.Auth.Isolation
{
    /// <summary>
    /// Windows isolation. There is no seccomp equivalent, so enforcement is the measurement redirect
    /// shim's import-address-table backend (loaded by <see cref="RedirectShim"/> before the
    /// proprietary module). This backend only records that the shim-based layer is in effect.
    /// </summary>
    internal sealed class WindowsIatBackend : IIsolationBackend
    {
        private static readonly Logger logger = Logger.GetLogger("auth");

        public WindowsIatBackend(IsolationOptions options)
        {
        }

        public bool IsSupported => OperatingSystem.IsWindows();

        public void Start()
        {
            logger.Add("Auth blob isolation: Windows uses the IAT shim as the enforcement layer.");
        }

        public void Dispose()
        {
        }
    }
}
