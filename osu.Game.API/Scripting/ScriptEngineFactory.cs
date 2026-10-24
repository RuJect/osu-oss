// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using Jint;
using Jint.Native;
using Jint.Runtime.Modules;
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

        /// <summary>
        /// Whether created engines may access arbitrary .NET types via the CLR bridge.
        /// </summary>
        public bool AllowClr { get; set; } = true;

        public ScriptEngineFactory(ScriptServices services, Func<IReadOnlyList<IScriptApiProvider>> providerFactory)
        {
            this.services = services;
            this.providerFactory = providerFactory;
        }

        public Engine Create(ScriptInstance instance, ScriptRunner runner)
        {
            Options options = new Options()
                              .CatchClrExceptions()
                              .EnableModules(new InlineModuleLoader())
                              .CancellationToken(instance.Cancellation.Token);

            if (AllowClr)
            {
                options.AllowClr(AppDomain.CurrentDomain.GetAssemblies());
                options.AllowClrWrite();
            }

            // Enable Task -> Promise interop so provider methods can return awaitable Tasks (e.g. osu.sleep).
            options.ExperimentalFeatures = ExperimentalFeature.TaskInterop;

            var engine = new Engine(options);
            instance.Engine = engine;

            JsObject api = new ScriptApiBuilder(engine, services, runner, instance, providerFactory()).Build();

            engine.SetValue("osu", api);

            return engine;
        }

        // Scripts run as modules so they can use top-level await. Imports are not supported; only the
        // inline module registered for the script can be loaded.
        private sealed class InlineModuleLoader : IModuleLoader
        {
            public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
                => new ResolvedSpecifier(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.Bare);

            public Module LoadModule(Engine engine, ResolvedSpecifier resolved)
                => throw new InvalidOperationException($"Module '{resolved.Key}' is not registered.");
        }
    }
}
