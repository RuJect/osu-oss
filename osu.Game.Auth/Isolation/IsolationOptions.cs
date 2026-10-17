// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Auth.Isolation
{
    /// <summary>
    /// Configuration of the auth blob isolation, sourced from the environment.
    /// </summary>
    /// <remarks>
    /// <c>AUTHISOLATE</c> is <c>enforce</c> (default) or <c>audit</c> (log-only); <c>0</c> disables
    /// the layer. <c>AUTHISOLATE_NET</c> is <c>deny</c> (default) or <c>allow</c>.
    /// <c>AUTHISOLATE_LOG</c> (or the pre-existing <c>AUTHREDIRECT_LOG</c>) selects the supervisor log.
    /// </remarks>
    internal sealed class IsolationOptions
    {
        public bool Enabled { get; private init; } = true;

        public bool Audit { get; private init; }

        public bool AllowNetwork { get; private init; }

        public string? LogPath { get; private init; }

        public static IsolationOptions FromEnvironment()
        {
            string mode = (Environment.GetEnvironmentVariable("AUTHISOLATE") ?? string.Empty).Trim();
            string net = (Environment.GetEnvironmentVariable("AUTHISOLATE_NET") ?? string.Empty).Trim();

            string? log = Environment.GetEnvironmentVariable("AUTHISOLATE_LOG");

            if (string.IsNullOrEmpty(log))
                log = Environment.GetEnvironmentVariable("AUTHREDIRECT_LOG");

            return new IsolationOptions
            {
                Enabled = mode != "0",
                Audit = string.Equals(mode, "audit", StringComparison.OrdinalIgnoreCase),
                AllowNetwork = string.Equals(net, "allow", StringComparison.OrdinalIgnoreCase),
                LogPath = string.IsNullOrEmpty(log) ? null : log,
            };
        }
    }
}
