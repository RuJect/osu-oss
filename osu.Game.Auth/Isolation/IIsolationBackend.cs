// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Auth.Isolation
{
    /// <summary>
    /// A platform mechanism that constrains the proprietary auth blobs. All methods are best-effort:
    /// isolation failing must never prevent signing.
    /// </summary>
    internal interface IIsolationBackend : IDisposable
    {
        /// <summary>Whether this backend can run on the current platform.</summary>
        bool IsSupported { get; }

        /// <summary>Starts the backend. Called before the proprietary module is loaded.</summary>
        void Start();
    }
}
