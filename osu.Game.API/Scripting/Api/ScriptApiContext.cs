// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Jint;
using Jint.Native;
using Jint.Runtime.Interop;

namespace osu.Game.API.Scripting.Api
{
    /// <summary>
    /// Context handed to <see cref="IScriptApiProvider"/>s so they can extend the global <c>osu</c> object
    /// for a single script instance.
    /// </summary>
    public class ScriptApiContext
    {
        public Engine Engine { get; }
        public ScriptInstance Instance { get; }
        public ScriptServices Services { get; }

        /// <summary>The runner owning <see cref="Instance"/>, used to trigger cross-script behaviour.</summary>
        public ScriptRunner Runner { get; }

        /// <summary>The root <c>osu</c> object exposed to the script.</summary>
        public JsObject Api { get; }

        public ScriptApiContext(Engine engine, ScriptInstance instance, ScriptServices services, ScriptRunner runner, JsObject api)
        {
            Engine = engine;
            Instance = instance;
            Services = services;
            Runner = runner;
            Api = api;
        }

        public JsObject CreateObject() => new JsObject(Engine);

        public void AddFunction(JsObject target, string name, Func<JsValue, JsValue[], JsValue> function)
            => target.Set(name, new ClrFunction(Engine, name, (thisObj, args) =>
            {
                try
                {
                    return function(thisObj, args);
                }
                catch (Exception e)
                {
                    Services.Output.Log(ScriptOutputLevel.Error, Instance.Name, e.Message);
                    return JsValue.Undefined;
                }
            }));

        public void AddProperty(JsObject target, string name, object? value)
            => target.Set(name, JsValue.FromObject(Engine, value));
    }
}
