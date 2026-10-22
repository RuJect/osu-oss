// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using Jint;
using Jint.Native;
using osu.Game.API.Scripting.Api;

namespace osu.Game.API.Scripting
{
    /// <summary>
    /// Creates Jint <see cref="Engine"/>s for script instances, wiring up the CLR access policy and the
    /// global API objects (<c>osu</c>, <c>console</c>, <c>internal</c>).
    /// </summary>
    public class ScriptEngineFactory
    {
        private readonly ScriptServices services;
        private readonly Func<IReadOnlyList<IScriptApiProvider>> providerFactory;

        public ScriptEngineFactory(ScriptServices services, Func<IReadOnlyList<IScriptApiProvider>> providerFactory)
        {
            this.services = services;
            this.providerFactory = providerFactory;
        }

        public Engine Create(ScriptInstance instance, ScriptRunner runner)
        {
            Options options = new Options()
                              .AllowClr(AppDomain.CurrentDomain.GetAssemblies())
                              .AllowClrWrite()
                              .CatchClrExceptions()
                              .CancellationToken(instance.Cancellation.Token);

            var engine = new Engine(options);
            instance.Engine = engine;

            JsObject api = new ScriptApiBuilder(engine, services, runner, instance, providerFactory()).Build();

            engine.SetValue("osu", api);

            return engine;
        }
    }
}
