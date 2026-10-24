// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
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
    /// <item><see cref="Locator"/> - resolution of definitions by name or file.</item>
    /// <item><see cref="Runner"/> - running instances and execution.</item>
    /// </list>
    /// </summary>
    public class ScriptManager
    {
        private readonly List<Func<IScriptApiProvider>> apiProviderFactories = new List<Func<IScriptApiProvider>>();

        public ScriptRepository Repository { get; }

        public ScriptOutputLog Output { get; }

        public ScriptEventBus Events { get; }

        public ScriptLocator Locator { get; }

        public ScriptRunner Runner { get; }

        public ScriptServices Services { get; }

        public ScriptManager(GameHost host, OsuGame game)
        {
            Output = new ScriptOutputLog();
            Repository = new ScriptRepository(Output);
            Events = new ScriptEventBus();
            Services = new ScriptServices(game, host, Repository, Output, Events);
            Locator = new ScriptLocator(Repository);

            var engineFactory = new ScriptEngineFactory(Services, buildProviderList);
            Runner = new ScriptRunner(Services, engineFactory, Locator);

            RegisterApiProvider(() => new ConsoleApiProvider());
            RegisterApiProvider(() => new CoreApiProvider());
            RegisterApiProvider(() => new SchedulerApiProvider());
            RegisterApiProvider(() => new EventsApiProvider());
            RegisterApiProvider(() => new StorageApiProvider());
            RegisterApiProvider(() => new TypeApiProvider());
            RegisterApiProvider(() => new NotificationApiProvider());
            RegisterApiProvider(() => new InternalApiProvider());
            RegisterApiProvider(() => new UtilityApiProvider());
            RegisterApiProvider(() => new ConfigApiProvider());
            RegisterApiProvider(() => new MusicApiProvider());
            RegisterApiProvider(() => new UiApiProvider());
            RegisterApiProvider(() => new CursorApiProvider());
            RegisterApiProvider(() => new KeyboardApiProvider());
        }

        /// <summary>
        /// Binds the on-disk script and data stores once the game has finished loading.
        /// </summary>
        public void AttachStorage(ScriptStorage scriptStorage, Storage dataStorage)
        {
            Repository.Storage = scriptStorage;
            Services.DataStorage = new ScriptDataStorage(dataStorage);
            Repository.Refresh();
        }

        public ScriptInstance? RunByName(string name) => Runner.RunByName(name);

        public ScriptInstance? RunFile(string fileName) => Runner.RunFile(fileName);

        /// <summary>
        /// Registers a factory for an API provider. A fresh provider instance is created for every script
        /// engine, because providers keep per-script state (notably their <see cref="ScriptApiProvider.Context"/>).
        /// </summary>
        public void RegisterApiProvider(Func<IScriptApiProvider> providerFactory)
        {
            ArgumentNullException.ThrowIfNull(providerFactory);

            if (!apiProviderFactories.Contains(providerFactory))
                apiProviderFactories.Add(providerFactory);
        }

        private IReadOnlyList<IScriptApiProvider> buildProviderList() => apiProviderFactories.Select(factory => factory()).ToList();
    }
}
