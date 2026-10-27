// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using Jint;
using Jint.Native;
using osu.Game.API.Plugins;
using osu.Game.API.Services;

namespace osu.Game.API.Scripting.Api
{
    /// <summary>
    /// Builds the global <c>osu</c> object for a single script instance by running every registered provider.
    /// </summary>
    internal class ScriptApiBuilder
    {
        private readonly Engine engine;
        private readonly PluginContext plugin;
        private readonly ScriptManager manager;
        private readonly ScriptInstance instance;
        private readonly IReadOnlyList<IScriptApiProvider> providers;

        public ScriptApiBuilder(Engine engine, PluginContext plugin, ScriptManager manager, ScriptRunner runner, ScriptInstance instance, IReadOnlyList<IScriptApiProvider> providers)
        {
            this.engine = engine;
            this.plugin = plugin;
            this.manager = manager;
            this.instance = instance;
            this.providers = providers;
        }

        public JsObject Build()
        {
            var api = new JsObject(engine);
            var context = new ScriptApiContext(engine, instance, plugin, manager, api);

            foreach (IScriptApiProvider provider in providers)
            {
                try
                {
                    provider.Register(context);
                }
                catch (Exception e)
                {
                    manager.Output.Log(OutputLevel.Error, instance.Name, $"failed to register API provider {provider.GetType().Name}: {e.Message}");
                }
            }

            return api;
        }
    }
}
