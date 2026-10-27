// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using Jint;
using Jint.Native;
using Jint.Runtime.Interop;
using osu.Game.API.Plugins;
using osu.Game.API.Services;

namespace osu.Game.API.Scripting.Api
{
    /// <summary>
    /// Context handed to <see cref="IScriptApiProvider"/>s so they can extend the global <c>osu</c> object
    /// for a single script instance.
    /// </summary>
    public class ScriptApiContext
    {
        private readonly Dictionary<string, JsObject> objects = new Dictionary<string, JsObject>();

        public Engine Engine { get; }
        public ScriptInstance Instance { get; }

        /// <summary>The plugin context whose services are exposed to the script.</summary>
        public PluginContext Plugin { get; }

        /// <summary>The scripting manager owning this context.</summary>
        public ScriptManager Manager { get; }

        /// <summary>Compatibility view of the plugin's services plus scripting collaborators.</summary>
        public ScriptServices Services => Manager.Services;

        public ScriptRepository Repository => Manager.Repository;

        public ScriptEventBus Events => Manager.Events;

        /// <summary>The runner owning <see cref="Instance"/>, used to trigger cross-script behaviour.</summary>
        public ScriptRunner Runner => Manager.Runner;

        /// <summary>The root <c>osu</c> object exposed to the script.</summary>
        public JsObject Api { get; }

        public ScriptApiContext(Engine engine, ScriptInstance instance, PluginContext plugin, ScriptManager manager, JsObject api)
        {
            Engine = engine;
            Instance = instance;
            Plugin = plugin;
            Manager = manager;
            Api = api;
        }

        public JsObject CreateObject() => new JsObject(Engine);

        /// <summary>
        /// Returns (creating and caching if needed) the object for the given target. A <see langword="null"/> or
        /// empty <paramref name="name"/> returns the root <c>osu</c> object. A <paramref name="global"/> target is a
        /// top-level global (e.g. <c>console</c>); otherwise it is nested under <c>osu</c> (e.g. <c>osu.storage</c>).
        /// </summary>
        public JsObject GetObject(string? name, bool global = false)
        {
            if (string.IsNullOrEmpty(name))
                return Api;

            string key = (global ? "global:" : "osu:") + name;

            if (!objects.TryGetValue(key, out JsObject? obj))
            {
                obj = new JsObject(Engine);
                objects[key] = obj;

                if (global)
                    Engine.SetValue(name!, obj);
                else
                    Api.Set(name!, obj);
            }

            return obj;
        }

        public void AddFunction(JsObject target, string name, Func<JsValue, JsValue[], JsValue> function)
            => target.Set(name, new ClrFunction(Engine, name, (thisObj, args) =>
            {
                try
                {
                    return function(thisObj, args);
                }
                catch (Exception e)
                {
                    Services.Output.Log(OutputLevel.Error, Instance.Name, e.Message);
                    return JsValue.Undefined;
                }
            }));

        public void AddProperty(JsObject target, string name, object? value)
            => target.Set(name, JsValue.FromObject(Engine, value));
    }
}
