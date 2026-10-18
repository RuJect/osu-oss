// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using Jint;
using Jint.Native;

namespace osu.Game.API.Scripting.Api
{
    /// <summary>
    /// Builds the global <c>osu</c> object for a single script instance by running every registered provider.
    /// </summary>
    internal class ScriptApiBuilder
    {
        private readonly Engine engine;
        private readonly ScriptApiEnvironment environment;
        private readonly ScriptRunner runner;
        private readonly ScriptInstance instance;
        private readonly IReadOnlyList<IScriptApiProvider> providers;

        public ScriptApiBuilder(Engine engine, ScriptApiEnvironment environment, ScriptRunner runner, ScriptInstance instance, IReadOnlyList<IScriptApiProvider> providers)
        {
            this.engine = engine;
            this.environment = environment;
            this.runner = runner;
            this.instance = instance;
            this.providers = providers;
        }

        public JsObject Build()
        {
            var api = new JsObject(engine);
            var context = new ScriptApiContext(engine, instance, environment, runner, api);

            foreach (IScriptApiProvider provider in providers)
            {
                try
                {
                    provider.Register(context);
                }
                catch (System.Exception e)
                {
                    environment.Output.Log(ScriptOutputLevel.Error, instance.Name, $"failed to register API provider {provider.GetType().Name}: {e.Message}");
                }
            }

            return api;
        }
    }
}
