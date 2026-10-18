// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.API.Scripting.Api
{
    /// <summary>
    /// A module that contributes members to the global <c>osu</c> object exposed to scripts.
    /// This is the extension point used by built-in features and, later, by plugins.
    /// </summary>
    public interface IScriptApiProvider
    {
        void Register(ScriptApiContext context);
    }
}
