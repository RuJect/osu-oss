// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.API.Scripting
{
    /// <summary>
    /// Global scripting preferences, shared by every plugin's scripting host.
    /// </summary>
    public sealed class ScriptingSettings
    {
        /// <summary>Whether scripts are allowed to start.</summary>
        public bool Enabled { get; private set; } = true;

        /// <summary>Whether scripts may access arbitrary .NET types via the CLR bridge.</summary>
        public bool AllowClrAccess { get; private set; } = true;

        /// <summary>Updates both preferences.</summary>
        public void Update(bool enabled, bool allowClrAccess)
        {
            Enabled = enabled;
            AllowClrAccess = allowClrAccess;
        }
    }
}
