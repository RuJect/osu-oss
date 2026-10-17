// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Logging;

namespace osu.Game.Auth.Isolation
{
    /// <summary>
    /// macOS isolation. seccomp is Linux-only and there is no sanctioned syscall-interception
    /// equivalent, so for now the measurement redirect shim is the only layer.
    /// </summary>
    internal sealed class MacShimBackend : IIsolationBackend
    {
        private static readonly Logger logger = Logger.GetLogger("auth");

        public MacShimBackend(IsolationOptions options)
        {
        }

        public bool IsSupported => OperatingSystem.IsMacOS();

        public void Start()
        {
            logger.Add("Auth blob isolation: macOS uses the interpose shim as the only available layer.");
        }

        public void Dispose()
        {
        }
    }
}
