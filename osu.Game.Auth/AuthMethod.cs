// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Auth
{
    /// <summary>
    /// Resolves the signing method selected via the <c>OSU_AUTH_METHOD</c> environment variable.
    /// </summary>
    /// <remarks>
    /// <c>transplant</c> (the default, as well as any unrecognised value) delegates to the
    /// proprietary managed module; the <c>native</c>/<c>reimpl</c>/<c>cleanroom</c> aliases select
    /// the open reimplementation.
    /// </remarks>
    internal static class AuthMethod
    {
        private const string method_env = "OSU_AUTH_METHOD";

        private const string transplant = "transplant";

        /// <summary>Whether the selected method transplants the proprietary managed module.</summary>
        public static bool IsTransplant()
        {
            string method = (Environment.GetEnvironmentVariable(method_env) ?? transplant).Trim().ToLowerInvariant();

            return method is not ("native" or "reimpl" or "cleanroom");
        }
    }
}
