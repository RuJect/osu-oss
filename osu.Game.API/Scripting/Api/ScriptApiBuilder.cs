// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using Jint;
using Jint.Native;
using osu.Game.API.Services;

namespace osu.Game.API.Scripting.Api
{
    /// <summary>
    /// Builds the global <c>osu</c> object for a single script instance by running every registered provider.
    /// </summary>
    internal class ScriptApiBuilder
    {
        private readonly Engine engine;
        private readonly ScriptServices services;
        private readonly ScriptRunner runner;
        private readonly ScriptInstance instance;
        private readonly IReadOnlyList<IScriptApiProvider> providers;

        public ScriptApiBuilder(Engine engine, ScriptServices services, ScriptRunner runner, ScriptInstance instance, IReadOnlyList<IScriptApiProvider> providers)
        {
            this.engine = engine;
            this.services = services;
            this.runner = runner;
            this.instance = instance;
            this.providers = providers;
        }

        public JsObject Build()
        {
            var api = new JsObject(engine);
            var context = new ScriptApiContext(engine, instance, services, runner, api);

            foreach (IScriptApiProvider provider in providers)
            {
                try
                {
                    provider.Register(context);
                }
                catch (Exception e)
                {
                    services.Output.Log(OutputLevel.Error, instance.Name, $"failed to register API provider {provider.GetType().Name}: {e.Message}");
                }
            }

            return api;
        }
    }
}
