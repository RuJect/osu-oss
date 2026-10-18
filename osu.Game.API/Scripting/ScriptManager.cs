// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Platform;
using osu.Game.API.Scripting.Api;
using osu.Game.API.Scripting.Api.Providers;

namespace osu.Game.API.Scripting
{
    /// <summary>
    /// Composition root of the scripting subsystem. Owns the collaborators that implement the actual
    /// behaviour and exposes them for consumers:
    /// <list type="bullet">
    /// <item><see cref="Repository"/> - known definitions and on-disk persistence.</item>
    /// <item><see cref="Output"/> - output history and log.</item>
    /// <item><see cref="Events"/> - script event subscriptions.</item>
    /// <item><see cref="Runner"/> - running instances and execution.</item>
    /// </list>
    /// </summary>
    public class ScriptManager
    {
        private readonly List<IScriptApiProvider> apiProviders = new List<IScriptApiProvider>();

        public ScriptRepository Repository { get; }

        public ScriptOutputLog Output { get; }

        public ScriptEventBus Events { get; }

        internal ScriptApiEnvironment Environment { get; }

        public ScriptRunner Runner { get; }

        public ScriptManager(GameHost host, OsuGame game)
        {
            Output = new ScriptOutputLog();
            Repository = new ScriptRepository(Output);
            Events = new ScriptEventBus();
            Environment = new ScriptApiEnvironment(game, host, Repository, Output, Events);

            var engineFactory = new ScriptEngineFactory(Environment, buildProviderList);
            Runner = new ScriptRunner(Environment, engineFactory);

            RegisterApiProvider(new ConsoleApiProvider());
            RegisterApiProvider(new CoreApiProvider());
            RegisterApiProvider(new SchedulerApiProvider());
            RegisterApiProvider(new EventsApiProvider());
            RegisterApiProvider(new StorageApiProvider());
            RegisterApiProvider(new TypeApiProvider());
            RegisterApiProvider(new NotificationApiProvider());
            RegisterApiProvider(new InternalApiProvider());
        }

        public void RegisterApiProvider(IScriptApiProvider provider)
        {
            if (!apiProviders.Contains(provider))
                apiProviders.Add(provider);
        }

        private IReadOnlyList<IScriptApiProvider> buildProviderList() => new List<IScriptApiProvider>(apiProviders);
    }
}
