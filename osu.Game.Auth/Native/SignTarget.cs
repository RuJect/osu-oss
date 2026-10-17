// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Auth
{
    /// <summary>
    /// The kind of object a native <c>Sign</c> call signs in place. The numeric values match the
    /// opcodes expected by the native module.
    /// </summary>
    internal enum SignTarget
    {
        /// <summary>An API <c>WebRequest</c>; adds the <c>x-token</c> header.</summary>
        WebRequest = 0,

        /// <summary>A SignalR <c>HttpConnectionOptions</c>; replaces the version-hash header with an attestation blob.</summary>
        HubConnection = 1,
    }
}
