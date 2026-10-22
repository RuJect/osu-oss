// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Platform;

namespace osu.Game.API.Scripting.Api.Providers
{
    /// <summary>
    /// Provides the <c>internal</c> global object, exposing the raw <c>game</c> and <c>host</c> instances
    /// for advanced/unsafe scripts.
    /// </summary>
    public class InternalApiProvider : ScriptApiProvider
    {
        [ScriptApi(Target = "internal", Global = true)]
        public OsuGame Game => Context.Services.Game;

        [ScriptApi(Target = "internal", Global = true)]
        public GameHost Host => Context.Services.Host;
    }
}
