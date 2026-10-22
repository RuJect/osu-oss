// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Jint.Native;

namespace osu.Game.API.Scripting.Api.Providers
{
    /// <summary>
    /// Provides the <c>internal</c> global object, exposing the raw <c>game</c> and <c>host</c> instances
    /// for advanced/unsafe scripts.
    /// </summary>
    public class InternalApiProvider : IScriptApiProvider
    {
        public void Register(ScriptApiContext context)
        {
            JsObject internalApi = context.CreateObject();

            context.AddProperty(internalApi, "game", context.Services.Game);
            context.AddProperty(internalApi, "host", context.Services.Host);

            context.Engine.SetValue("internal", internalApi);
        }
    }
}
