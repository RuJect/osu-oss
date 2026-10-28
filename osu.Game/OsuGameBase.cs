// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Development;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input;
using osu.Framework.Input.Handlers;
using osu.Framework.Input.Handlers.Joystick;
using osu.Framework.Input.Handlers.Midi;
using osu.Framework.Input.Handlers.Mouse;
using osu.Framework.Input.Handlers.Pen;
using osu.Framework.Input.Handlers.Tablet;
using osu.Framework.Input.Handlers.Touch;
using osu.Framework.IO.Stores;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Timing;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Formats;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Events;
using osu.Game.Extensions;
using osu.Game.Graphics;
using osu.Game.Graphics.Cursor;
using osu.Game.Graphics.UserInterface;
using osu.Game.Input;
using osu.Game.Input.Bindings;
using osu.Game.IO;
using osu.Game.Localisation;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.Chat;
using osu.Game.Online.Leaderboards;
using osu.Game.Online.Metadata;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Server;
using osu.Game.Online.Spectator;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.Settings.Sections;
using osu.Game.Overlays.Settings.Sections.Input;
using osu.Game.Resources;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Skinning;
using osu.Game.Theming;
using osu.Game.Utils;
using RuntimeInfo = osu.Framework.RuntimeInfo;

namespace osu.Game
{
    /// <summary>
    /// The most basic <see cref="Game"/> that can be used to host osu! components and systems.
    /// Unlike <see cref="OsuGame"/>, this class will not load any kind of UI, allowing it to be used
    /// for provide dependencies to test cases without interfering with them.
    /// </summary>
    [Cached(typeof(OsuGameBase))]
    public partial class OsuGameBase : Framework.Game, ICanAcceptFiles, IBeatSyncProvider
    {
        public const string GAME_NAME = "osu!oss";

        public const string OSU_PROTOCOL = "osuoss://";

        /// <summary>
        /// The filename of the main client database.
        /// </summary>
        public const string CLIENT_DATABASE_FILENAME = @"client.realm";

        public const int SAMPLE_CONCURRENCY = 6;

        public const double SFX_STEREO_STRENGTH = 0.6;

        /// <summary>
        /// Length of debounce (in milliseconds) for commonly occuring sample playbacks that could stack.
        /// </summary>
        public const int SAMPLE_DEBOUNCE_TIME = 20;

        /// <summary>
        /// The maximum volume at which audio tracks should play back at. This can be set lower than 1 to create some head-room for sound effects.
        /// </summary>
        private const double global_track_volume_adjust = 0.8;

        public virtual EndpointConfiguration CreateEndpoints() => new ProductionEndpointConfiguration();

        /// <summary>
        /// The manager that bridges the runtime <see cref="IServerRegistry"/> with the user's
        /// persisted server preference. Only populated when the server subsystem is wired
        /// (see <see cref="CreateServerRegistry"/> / <see cref="CreateServerManager"/>).
        /// </summary>
        protected IServerManager ServerManager { get; set; }

        /// <summary>
        /// Hook for subclasses that reference <c>osu.Game.API</c> (e.g. the desktop game) to
        /// provide a concrete <see cref="IServerRegistry"/> instance. Returns <c>null</c> by default,
        /// in which case the legacy <see cref="CreateEndpoints"/> path is used.
        /// </summary>
        protected virtual IServerRegistry CreateServerRegistry() => null;

        /// <summary>
        /// Hook for subclasses that reference <c>osu.Game.API</c> (e.g. the desktop game) to
        /// wire the <see cref="IServerManager"/> from a <see cref="IServerRegistry"/>.
        /// Returns <c>null</c> by default, in which case the legacy <see cref="CreateEndpoints"/> path is used.
        /// </summary>
        protected virtual IServerManager CreateServerManager(IServerRegistry registry) => null;

        /// <summary>
        /// Whether all outbound network access should be disabled for this run.
        /// </summary>
        public bool NoNetwork { get; init; }

        /// <summary>
        /// An optional data directory to use for this run, overriding the configured storage path.
        /// </summary>
        public string DataPath { get; init; }

        protected override OnlineStore CreateOnlineStore() => NoNetwork ? new OfflineOnlineStore() : new TrustedDomainOnlineStore();

        public virtual Version AssemblyVersion => Assembly.GetEntryAssembly()?.GetName().Version ?? new Version();

        public bool IsDeployedBuild => AssemblyVersion.Major > 0;

        /// <summary>
        /// MD5 representation of the game executable.
        /// </summary>
        public string VersionHash { get; private set; }

        public virtual string Version
        {
            get
            {
                string informationalVersion = Assembly.GetEntryAssembly()?
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                    .InformationalVersion;

                // Example: [assembly: AssemblyInformationalVersion("2025.613.0-tachyon+d934e574b2539e8787956c3c9ecce9dadebb10ee")]
                if (!string.IsNullOrEmpty(informationalVersion))
                    return informationalVersion.Split('+').First();

                Version version = AssemblyVersion;
                return $@"{version.Major}.{version.Minor}.{version.Build}-lazer";
            }
        }

        /// <summary>
        /// The <see cref="Edges"/> that the game should be drawn over at a top level.
        /// Defaults to <see cref="Edges.None"/>.
        /// </summary>
        protected virtual Edges SafeAreaOverrideEdges => Edges.None;

        protected OsuConfigManager LocalConfig { get; private set; }

        /// <summary>
        /// Config manager for osu!oss-specific settings, persisted to <c>osu-oss/config.ini</c>.
        /// </summary>
        protected SpecialConfigManager SpecialConfig { get; private set; }

        protected SessionStatics SessionStatics { get; private set; }

        protected OsuColour Colours { get; private set; }

        protected BeatmapManager BeatmapManager { get; private set; }

        protected BeatmapModelDownloader BeatmapDownloader { get; private set; }

        protected ScoreManager ScoreManager { get; private set; }

        protected ScoreModelDownloader ScoreDownloader { get; private set; }

        protected SkinManager SkinManager { get; private set; }

        protected RealmRulesetStore RulesetStore { get; private set; }

        protected RealmKeyBindingStore KeyBindingStore { get; private set; }

        protected GlobalCursorDisplay GlobalCursorDisplay { get; private set; }

        protected MusicController MusicController { get; private set; }

        protected IAPIProvider API { get; set; }

        protected Storage Storage { get; set; }

        /// <summary>
        /// The language in which the game is currently displayed in.
        /// </summary>
        public Bindable<Language> CurrentLanguage { get; } = new Bindable<Language>();

        protected Bindable<WorkingBeatmap> Beatmap { get; private set; } // cached via load() method

        /// <summary>
        /// The current ruleset selection for the local user.
        /// </summary>
        [Cached]
        [Cached(typeof(IBindable<RulesetInfo>))]
        protected internal readonly Bindable<RulesetInfo> Ruleset = new Bindable<RulesetInfo>();

        /// <summary>
        /// The current mod selection for the local user.
        /// </summary>
        /// <remarks>
        /// If a mod select overlay is present, mod instances set to this value are not guaranteed to remain as the provided instance and will be overwritten by a copy.
        /// In such a case, changes to settings of a mod will *not* propagate after a mod is added to this collection.
        /// As such, all settings should be finalised before adding a mod to this collection.
        /// </remarks>
        [Cached]
        [Cached(typeof(IBindable<IReadOnlyList<Mod>>))]
        protected readonly Bindable<IReadOnlyList<Mod>> SelectedMods = new Bindable<IReadOnlyList<Mod>>(Array.Empty<Mod>());

        /// <summary>
        /// The mods currently applied to the player, exposed read-only for external modules (see <c>osu.Game.API</c>).
        /// </summary>
        public IReadOnlyList<Mod> CurrentMods => SelectedMods.Value;

        /// <summary>
        /// Replaces the player's mods with those matching <paramref name="acronyms"/>, exposed for external modules
        /// (see <c>osu.Game.API</c>).
        /// </summary>
        /// <returns>Whether the mods were resolved and compatible.</returns>
        public bool TrySetModsByAcronyms(IEnumerable<string> acronyms, out string error)
        {
            var available = ModUtils.FlattenMods(AvailableMods.Value.Values.SelectMany(m => m)).ToList();
            var resolved = new List<Mod>();

            foreach (string acronym in acronyms)
            {
                var mod = available.FirstOrDefault(m => string.Equals(m.Acronym, acronym.Trim(), StringComparison.OrdinalIgnoreCase));

                if (mod == null)
                {
                    error = $"unknown mod '{acronym}'";
                    return false;
                }

                resolved.Add(mod.DeepClone());
            }

            if (!ModUtils.CheckCompatibleSet(resolved, out var invalidMods))
            {
                error = $"incompatible mods: {string.Join(", ", invalidMods.Select(m => m.Acronym))}";
                return false;
            }

            SelectedMods.Value = resolved;
            error = null;
            return true;
        }

        /// <summary>
        /// Clears all currently active mods, exposed for external modules (see <c>osu.Game.API</c>).
        /// </summary>
        public void ClearMods() => SelectedMods.Value = Array.Empty<Mod>();

        /// <summary>
        /// The skins available for selection, exposed for external modules (see <c>osu.Game.API</c>).
        /// </summary>
        public IReadOnlyList<SkinInfo> AvailableSkins => SkinManager.GetAllUsableSkins().Select(s => s.Value).ToList();

        /// <summary>
        /// The currently selected skin, exposed for external modules (see <c>osu.Game.API</c>).
        /// </summary>
        public SkinInfo CurrentSkinInfo => SkinManager.CurrentSkinInfo.Value.Value;

        /// <summary>
        /// Selects the next available skin, exposed for external modules (see <c>osu.Game.API</c>).
        /// </summary>
        public void SelectNextSkin() => SkinManager.SelectNextSkin();

        /// <summary>
        /// Selects a random skin, exposed for external modules (see <c>osu.Game.API</c>).
        /// </summary>
        public void SelectRandomSkin() => SkinManager.SelectRandomSkin();

        /// <summary>
        /// Selects the skin with the given name, exposed for external modules (see <c>osu.Game.API</c>).
        /// </summary>
        /// <param name="name">The name of the skin to select, or <c>random</c>.</param>
        /// <param name="error">A human readable error if no matching skin was found.</param>
        /// <returns>Whether a matching skin was selected.</returns>
        public bool TrySelectSkin(string name, out string error)
        {
            name = name.Trim();

            if (string.Equals(name, "random", StringComparison.OrdinalIgnoreCase))
            {
                SelectRandomSkin();
                error = null;
                return true;
            }

            var skins = SkinManager.GetAllUsableSkins();
            string simplifiedName = simplifySkinName(name);

            // prefer an exact name match, then fall back to the "common" name (e.g. `argon` for `osu! "argon" (2022)`).
            var match = skins.FirstOrDefault(s => string.Equals(s.Value.Name, name, StringComparison.OrdinalIgnoreCase))
                        ?? skins.FirstOrDefault(s => string.Equals(simplifySkinName(s.Value.Name), simplifiedName, StringComparison.OrdinalIgnoreCase));

            if (match == null)
            {
                error = $"unknown skin '{name}'";
                return false;
            }

            if (match.Value.ID == SkinInfo.RANDOM_SKIN)
                SelectRandomSkin();
            else
                SkinManager.CurrentSkinInfo.Value = match;

            error = null;
            return true;
        }

        private static string simplifySkinName(string name)
        {
            // turns `osu! "argon" (2022)` into `argon`, allowing built-in skins to be selected by their common name.
            if (name.StartsWith("osu!", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(4);

            name = name.Replace("\"", string.Empty).Trim();

            int versionStart = name.LastIndexOf('(');

            if (versionStart > 0 && name.EndsWith(')'))
                name = name.Substring(0, versionStart).Trim();

            return name;
        }

        /// <summary>
        /// The rulesets available for selection, exposed for external modules (see <c>osu.Game.API</c>).
        /// </summary>
        public IReadOnlyList<RulesetInfo> AvailableRulesets => RulesetStore.AvailableRulesets.ToList();

        /// <summary>
        /// The currently selected ruleset, exposed for external modules (see <c>osu.Game.API</c>).
        /// </summary>
        public RulesetInfo CurrentRuleset => Ruleset.Value;

        /// <summary>
        /// Selects the next available ruleset, exposed for external modules (see <c>osu.Game.API</c>).
        /// </summary>
        public void SelectNextRuleset()
        {
            var available = RulesetStore.AvailableRulesets.ToList();

            if (available.Count == 0)
                return;

            int index = available.FindIndex(r => string.Equals(r.ShortName, Ruleset.Value.ShortName, StringComparison.OrdinalIgnoreCase));
            Ruleset.Value = available[(index + 1 + available.Count) % available.Count];
        }

        /// <summary>
        /// Selects the ruleset matching the given name, exposed for external modules (see <c>osu.Game.API</c>).
        /// </summary>
        /// <param name="name">
        /// A ruleset name or short name (e.g. <c>osu</c>, <c>taiko</c>, <c>fruits</c>/<c>catch</c>, <c>mania</c>).
        /// </param>
        /// <param name="error">A human readable error if no matching ruleset was found.</param>
        /// <returns>Whether a matching ruleset was selected.</returns>
        public bool TrySelectRuleset(string name, out string error)
        {
            name = name.Trim();

            var match = AvailableRulesets.FirstOrDefault(r =>
                string.Equals(r.ShortName, name, StringComparison.OrdinalIgnoreCase)
                || string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)
                || string.Equals(r.Name.TrimEnd('!'), name, StringComparison.OrdinalIgnoreCase)
                // allows the trailing component of names like "osu!taiko" / "osu!catch" / "osu!mania".
                || r.Name.EndsWith("!" + name, StringComparison.OrdinalIgnoreCase));

            if (match == null)
            {
                error = $"unknown ruleset '{name}' (available: {string.Join(", ", AvailableRulesets.Select(r => r.ShortName))})";
                return false;
            }

            Ruleset.Value = match;
            error = null;
            return true;
        }

        /// <summary>
        /// Mods available for the current <see cref="Ruleset"/>.
        /// </summary>
        public readonly Bindable<Dictionary<ModType, IReadOnlyList<Mod>>> AvailableMods = new Bindable<Dictionary<ModType, IReadOnlyList<Mod>>>(new Dictionary<ModType, IReadOnlyList<Mod>>());

        /// <summary>
        /// Central transport for game-wide events, consumed by external modules (see <c>osu.Game.API</c>).
        /// </summary>
        public OsuEventBus Events { get; private set; }

        private BeatmapDifficultyCache difficultyCache;

        private IBeatmapUpdater beatmapUpdater;
        private OnlineAssetCachingStore onlineAssetStore;

        private UserLookupCache userCache;
        private BeatmapLookupCache beatmapCache;
        protected LeaderboardManager LeaderboardManager { get; private set; }

        private RulesetConfigCache rulesetConfigCache;

        private SessionAverageHitErrorTracker hitErrorTracker;

        protected SpectatorClient SpectatorClient { get; private set; }

        protected MultiplayerClient MultiplayerClient { get; private set; }

        private MetadataClient metadataClient;

        private RealmAccess realm;

        protected SafeAreaContainer SafeAreaContainer { get; private set; }

        /// <summary>
        /// For now, this is used as a source specifically for beat synced components.
        /// Going forward, it could potentially be used as the single source-of-truth for beatmap timing.
        /// </summary>
        private readonly FramedBeatmapClock beatmapClock = new FramedBeatmapClock(applyOffsets: true, requireDecoupling: false);

        protected override Container<Drawable> Content => content;

        private Container content;

        private DependencyContainer dependencies;

        private readonly BindableNumber<double> globalTrackVolumeAdjust = new BindableNumber<double>(global_track_volume_adjust);

        private Bindable<string> frameworkLocale = null!;

        private IBindable<LocalisationParameters> localisationParameters = null!;

        /// <summary>
        /// Number of unhandled exceptions to allow before aborting execution.
        /// </summary>
        /// <remarks>
        /// When an unhandled exception is encountered, an internal count will be decremented.
        /// If the count hits zero, the game will crash.
        /// Each second, the count is incremented until reaching the value specified.
        /// </remarks>
        protected virtual int UnhandledExceptionsBeforeCrash => 0;

        public OsuGameBase()
        {
            Name = GAME_NAME;

            allowableExceptions = UnhandledExceptionsBeforeCrash;
        }

        [BackgroundDependencyLoader]
        private void load(ReadableKeyCombinationProvider keyCombinationProvider, FrameworkConfigManager frameworkConfig)
        {
            try
            {
                using (var str = File.OpenRead(typeof(OsuGameBase).Assembly.Location))
                    VersionHash = str.ComputeMD5Hash();
            }
            catch
            {
                // special case for android builds, which can't read DLLs from a packed apk.
                // should eventually be handled in a better way.
                VersionHash = $"{Version}-{RuntimeInfo.OS}".ComputeMD5Hash();
            }

            Resources.AddStore(new DllResourceStore(OsuResources.ResourceAssembly));

            dependencies.Cache(realm = new RealmAccess(Storage, CLIENT_DATABASE_FILENAME, Host.UpdateThread));

            dependencies.CacheAs<RulesetStore>(RulesetStore = new RealmRulesetStore(realm, Storage));
            dependencies.CacheAs<IRulesetStore>(RulesetStore);

            Decoder.RegisterDependencies(RulesetStore);

            dependencies.CacheAs(Storage);

            var largeStore = new LargeTextureStore(Host.Renderer, Host.CreateTextureLoaderStore(new NamespacedResourceStore<byte[]>(Resources, @"Textures")));
            largeStore.AddTextureSource(Host.CreateTextureLoaderStore(CreateOnlineStore()));
            dependencies.Cache(largeStore);

            dependencies.Cache(onlineAssetStore = new OnlineAssetCachingStore(Host, realm, CreateOnlineStore()));

            dependencies.CacheAs(LocalConfig);
            dependencies.CacheAs<IGameplaySettings>(LocalConfig);

            dependencies.CacheAs(SpecialConfig);

            InitialiseFonts();

            addFilesWarning();

            Audio.Samples.PlaybackConcurrency = SAMPLE_CONCURRENCY;

            dependencies.Cache(SkinManager = new SkinManager(Storage, realm, Host, Resources, Audio, Scheduler));
            dependencies.CacheAs<ISkinSource>(SkinManager);

            var defaultBeatmap = new DummyWorkingBeatmap(Audio, Textures);

            // Initialise the server registry and manager.
            // Concrete implementations (and the default Bancho/Dev servers) live in osu.Game.API.
            // Subclasses that reference osu.Game.API (e.g. the desktop game) override the hooks below
            // to wire the subsystem; otherwise we fall back to the legacy CreateEndpoints() path.
            var serverRegistry = CreateServerRegistry();
            var serverManager = CreateServerManager(serverRegistry);

            if (serverManager != null)
            {
                this.ServerManager = serverManager;
                dependencies.CacheAs<IServerRegistry>(serverRegistry);
                dependencies.CacheAs<IServerManager>(serverManager);

                MessageFormatter.WebsiteRootUrl = serverManager.ActiveServer.Value.Endpoints.WebsiteUrl;

                dependencies.CacheAs(API ??= new APIAccess(this, LocalConfig, serverManager, VersionHash, NoNetwork));

                dependencies.Cache(difficultyCache = new BeatmapDifficultyCache());

                // ordering is important here to ensure foreign keys rules are not broken in ModelStore.Cleanup()
                dependencies.Cache(ScoreManager = new ScoreManager(RulesetStore, () => BeatmapManager, Storage, realm, API, LocalConfig));

                dependencies.Cache(BeatmapManager = new BeatmapManager(Storage, realm, API, Audio, Resources, Host, defaultBeatmap, difficultyCache, performOnlineLookups: true));
                dependencies.CacheAs<IWorkingBeatmapCache>(BeatmapManager);

                dependencies.Cache(BeatmapDownloader = new BeatmapModelDownloader(BeatmapManager, API));

                // Downloads complete on a background thread, so marshal the notification to the update thread.
                BeatmapDownloader.DownloadCompleted += downloaded => Schedule(() =>
                {
                    foreach (Live<BeatmapSetInfo> live in downloaded)
                        Events.Publish(new BeatmapDownloadedEvent(live.PerformRead(set => set.Detach())) { UI = null });
                });
                dependencies.Cache(ScoreDownloader = new ScoreModelDownloader(ScoreManager, API));

                // Add after all the above cache operations as it depends on them.
                base.Content.Add(difficultyCache);

                // TODO: OsuGame or OsuGameBase?
                dependencies.CacheAs(beatmapUpdater = CreateBeatmapUpdater());
                dependencies.CacheAs(SpectatorClient = new OnlineSpectatorClient(API!.Endpoints));
                dependencies.CacheAs(MultiplayerClient = new OnlineMultiplayerClient(API!.Endpoints));
                dependencies.CacheAs(metadataClient = new OnlineMetadataClient(API!.Endpoints));
            }
            else
            {
                // Legacy fallback: use the static EndpointConfiguration provider.
                EndpointConfiguration endpoints = CreateEndpoints();

                MessageFormatter.WebsiteRootUrl = endpoints.WebsiteUrl;

                dependencies.CacheAs(API ??= new APIAccess(this, LocalConfig, endpoints, VersionHash, NoNetwork));

                dependencies.Cache(difficultyCache = new BeatmapDifficultyCache());

                // ordering is important here to ensure foreign keys rules are not broken in ModelStore.Cleanup()
                dependencies.Cache(ScoreManager = new ScoreManager(RulesetStore, () => BeatmapManager, Storage, realm, API, LocalConfig));

                dependencies.Cache(BeatmapManager = new BeatmapManager(Storage, realm, API, Audio, Resources, Host, defaultBeatmap, difficultyCache, performOnlineLookups: true));
                dependencies.CacheAs<IWorkingBeatmapCache>(BeatmapManager);

                dependencies.Cache(BeatmapDownloader = new BeatmapModelDownloader(BeatmapManager, API));

                // Downloads complete on a background thread, so marshal the notification to the update thread.
                BeatmapDownloader.DownloadCompleted += downloaded => Schedule(() =>
                {
                    foreach (Live<BeatmapSetInfo> live in downloaded)
                        Events.Publish(new BeatmapDownloadedEvent(live.PerformRead(set => set.Detach())) { UI = null });
                });
                dependencies.Cache(ScoreDownloader = new ScoreModelDownloader(ScoreManager, API));

                // Add after all the above cache operations as it depends on them.
                base.Content.Add(difficultyCache);

                // TODO: OsuGame or OsuGameBase?
                dependencies.CacheAs(beatmapUpdater = CreateBeatmapUpdater());
                dependencies.CacheAs(SpectatorClient = new OnlineSpectatorClient(API!.Endpoints));
                dependencies.CacheAs(MultiplayerClient = new OnlineMultiplayerClient(API!.Endpoints));
                dependencies.CacheAs(metadataClient = new OnlineMetadataClient(API!.Endpoints));
            }

            base.Content.Add(new BeatmapOnlineChangeIngest(beatmapUpdater, realm, metadataClient));

            BeatmapManager.ProcessBeatmap = (beatmapSet, scope) => beatmapUpdater.Process(beatmapSet, scope);

            dependencies.Cache(userCache = new UserLookupCache());
            base.Content.Add(userCache);

            dependencies.Cache(beatmapCache = new BeatmapLookupCache());
            base.Content.Add(beatmapCache);

            dependencies.CacheAs<IRulesetConfigCache>(rulesetConfigCache = new RulesetConfigCache(realm, RulesetStore));

            var powerStatus = CreateBatteryInfo();
            if (powerStatus != null)
                dependencies.CacheAs(powerStatus);

            dependencies.Cache(Events = new OsuEventBus());

            BeatmapManager.Events = Events;

            dependencies.Cache(SessionStatics = new SessionStatics());
            dependencies.Cache(hitErrorTracker = new SessionAverageHitErrorTracker());

            ThemeManager.Initialise(Storage, SpecialConfig.GetBindable<string>(SpecialSetting.Theme).Value,
                SpecialConfig.GetBindable<bool>(SpecialSetting.ThemeGlassEffects).Value,
                SpecialConfig.GetBindable<bool>(SpecialSetting.AggressiveTinting).Value);
            dependencies.Cache(Colours = new OsuColour());

            RegisterImportHandler(BeatmapManager);
            RegisterImportHandler(ScoreManager);
            RegisterImportHandler(SkinManager);

            // drop track volume game-wide to leave some head-room for UI effects / samples.
            // this means that for the time being, gameplay sample playback is louder relative to the audio track, compared to stable.
            // we may want to revisit this if users notice or complain about the difference (consider this a bit of a trial).
            Audio.Tracks.AddAdjustment(AdjustableProperty.Volume, globalTrackVolumeAdjust);

            Beatmap = new NonNullableBindable<WorkingBeatmap>(defaultBeatmap);

            dependencies.CacheAs<IBindable<WorkingBeatmap>>(Beatmap);
            dependencies.CacheAs(Beatmap);

            dependencies.Cache(LeaderboardManager = new LeaderboardManager());
            base.Content.Add(LeaderboardManager);

            // add api components to hierarchy.
            if (API is APIAccess apiAccess)
                base.Content.Add(apiAccess);

            base.Content.Add(SpectatorClient);
            base.Content.Add(MultiplayerClient);
            base.Content.Add(metadataClient);

            base.Content.Add(rulesetConfigCache);

            PreviewTrackManager previewTrackManager;
            dependencies.Cache(previewTrackManager = new PreviewTrackManager(BeatmapManager.BeatmapTrackStore, CreateOnlineStore()));
            base.Content.Add(previewTrackManager);

            base.Content.Add(MusicController = new MusicController());
            dependencies.CacheAs(MusicController);

            MusicController.TrackChanged += onTrackChanged;
            base.Content.Add(beatmapClock);

            GlobalActionContainer globalBindings;

            OsuMenuSamples menuSamples;
            dependencies.Cache(menuSamples = new OsuMenuSamples());
            base.Content.Add(menuSamples);

            base.Content.Add(SafeAreaContainer = new SafeAreaContainer
            {
                SafeAreaOverrideEdges = SafeAreaOverrideEdges,
                RelativeSizeAxes = Axes.Both,
                Child = CreateScalingContainer().WithChild(globalBindings = new GlobalActionContainer(this)
                {
                    Children = new Drawable[]
                    {
                        (GlobalCursorDisplay = new GlobalCursorDisplay
                        {
                            RelativeSizeAxes = Axes.Both
                        }).WithChild(content = new OsuTooltipContainer(GlobalCursorDisplay.MenuCursor)
                        {
                            RelativeSizeAxes = Axes.Both
                        }),
                    }
                })
            });

            base.Content.Add(new TouchInputInterceptor());
            base.Content.Add(hitErrorTracker);

            KeyBindingStore = new RealmKeyBindingStore(realm, keyCombinationProvider);
            KeyBindingStore.Register(globalBindings, RulesetStore.AvailableRulesets);
            dependencies.Cache(KeyBindingStore);

            dependencies.Cache(globalBindings);

            Ruleset.BindValueChanged(onRulesetChanged);
            Beatmap.BindValueChanged(onBeatmapChanged);

            // a single user action (or external update) can touch this bindable more than once per frame
            // (e.g. ModSelectOverlay updating its local selection in response to the change), so coalesce
            // same-frame changes into a single notification carrying the final selection.
            SelectedMods.BindValueChanged(_ => Scheduler.AddOnce(raiseSelectedModsChanged));

            // make config aware of how to lookup skins for on-screen display purposes.
            // if this becomes a more common thing, tracked settings should be reconsidered to allow local DI.
            LocalConfig.LookupSkinName = id => SkinManager.Query(s => s.ID == id)?.ToString() ?? "Unknown";
            LocalConfig.LookupKeyBindings = l => KeyBindingStore.GetBindingsStringFor(l);
        }

        private void updateLanguage() => CurrentLanguage.Value = LanguageExtensions.GetLanguageFor(frameworkLocale.Value, localisationParameters.Value);

        private void raiseSelectedModsChanged() => Events.Publish(new SelectedModsChangedEvent(SelectedMods.Value) { UI = null });

        private void addFilesWarning()
        {
            const string filename = "IMPORTANT READ ME.txt";

            if (!Storage.Exists(filename))
            {
                using (var stream = Storage.CreateFileSafely(filename))
                using (var textWriter = new StreamWriter(stream))
                {
                    textWriter.WriteLine(@"This folder contains all your user files and configuration.");
                    textWriter.WriteLine(@"Please DO NOT make manual changes to this folder.");
                    textWriter.WriteLine();
                    textWriter.WriteLine(@"- If you want to back up your game files, please back up THE ENTIRETY OF THIS DIRECTORY.");
                    textWriter.WriteLine(@"- If you want to delete all of your game files, please delete THE ENTIRETY OF THIS DIRECTORY.");
                    textWriter.WriteLine();
                    textWriter.WriteLine(@"To be very clear, the ""files/"" directory inside this directory stores all the raw pieces of your beatmaps, skins, and replays.");
                    textWriter.WriteLine(@"Importantly, it is NOT the only directory you need a backup of to avoid losing data. If you copy only the ""files/"" directory, YOU WILL LOSE DATA.");
                    textWriter.WriteLine();
                    textWriter.WriteLine(@"For more information on how these files are organised,");
                    textWriter.WriteLine(@"see https://github.com/ppy/osu/wiki/User-file-storage");
                }
            }
        }

        private void onTrackChanged(WorkingBeatmap beatmap, TrackChangeDirection direction) => beatmapClock.ChangeSource(beatmap.Track);

        protected virtual void InitialiseFonts()
        {
            AddFont(Resources, @"Fonts/Torus/Torus-Regular");
            AddFont(Resources, @"Fonts/Torus/Torus-Light");
            AddFont(Resources, @"Fonts/Torus/Torus-SemiBold");
            AddFont(Resources, @"Fonts/Torus/Torus-Bold");

            AddFont(Resources, @"Fonts/Torus-Alternate/Torus-Alternate-Regular");
            AddFont(Resources, @"Fonts/Torus-Alternate/Torus-Alternate-Light");
            AddFont(Resources, @"Fonts/Torus-Alternate/Torus-Alternate-SemiBold");
            AddFont(Resources, @"Fonts/Torus-Alternate/Torus-Alternate-Bold");

            AddFont(Resources, @"Fonts/Inter/Inter-Regular");
            AddFont(Resources, @"Fonts/Inter/Inter-RegularItalic");
            AddFont(Resources, @"Fonts/Inter/Inter-Light");
            AddFont(Resources, @"Fonts/Inter/Inter-LightItalic");
            AddFont(Resources, @"Fonts/Inter/Inter-SemiBold");
            AddFont(Resources, @"Fonts/Inter/Inter-SemiBoldItalic");
            AddFont(Resources, @"Fonts/Inter/Inter-Bold");
            AddFont(Resources, @"Fonts/Inter/Inter-BoldItalic");

            AddFont(Resources, @"Fonts/Noto/Noto-Basic");
            AddFont(Resources, @"Fonts/Noto/Noto-Bopomofo");
            AddFont(Resources, @"Fonts/Noto/Noto-CJK-Basic");
            AddFont(Resources, @"Fonts/Noto/Noto-CJK-Compatibility");
            AddFont(Resources, @"Fonts/Noto/Noto-Hangul");
            AddFont(Resources, @"Fonts/Noto/Noto-Thai");

            AddFont(Resources, @"Fonts/Venera/Venera-Light");
            AddFont(Resources, @"Fonts/Venera/Venera-Bold");
            AddFont(Resources, @"Fonts/Venera/Venera-Black");

            Fonts.AddStore(new OsuIcon.OsuIconStore(Textures));
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            var localeMappings = Enum.GetValues<Language>().Select(language =>
            {
                if (language == Language.debug)
                    return new LocaleMapping("debug", new DebugLocalisationStore());

                string cultureCode = language.ToCultureCode();

                try
                {
                    return new LocaleMapping(new ResourceManagerLocalisationStore(cultureCode));
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Could not load localisations for language \"{cultureCode}\"");
                    return null;
                }
            }).Where(m => m != null);

            Localisation.AddLocaleMappings(localeMappings);
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent) =>
            dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

        public override void SetHost(GameHost host)
        {
            base.SetHost(host);

            // may be non-null for certain tests
            Storage ??= host.Storage;

            LocalConfig ??= DebugUtils.IsDebugBuild
                ? new DevelopmentOsuConfigManager(Storage)
                : new OsuConfigManager(Storage);
            SpecialConfig ??= new SpecialConfigManager(SpecialStorage.GetRoot(Storage));

            host.ExceptionThrown += onExceptionThrown;
        }

        #region Exit handling

        /// <summary>
        /// Use to programatically exit the game as if the user was triggering via alt-f4.
        /// By default, will keep persisting until an exit occurs (exit may be blocked multiple times).
        /// May be interrupted (see <see cref="OsuGame"/>'s override).
        /// </summary>
        public virtual void AttemptExit()
        {
            if (!OnExiting())
                Exit();
            else
                Scheduler.AddDelayed(AttemptExit, 2000);
        }

        /// <summary>
        /// An action that restarts the application after it has exited.
        /// </summary>
        [CanBeNull]
        public Action RestartOnExitAction { private get; set; }

        /// <summary>
        /// Signals that the application should not be restarted after it is exited.
        /// </summary>
        public void CancelRestartOnExit()
        {
            RestartOnExitAction = null;
        }

        /// <summary>
        /// If supported by the platform, the game will automatically restart after the next exit.
        /// </summary>
        /// <returns>Whether a restart operation was queued.</returns>
        public virtual bool RestartAppWhenExited() => false;

        #endregion

        /// <summary>
        /// Perform migration of user data to a specified path.
        /// </summary>
        /// <param name="path">The path to migrate to.</param>
        /// <returns>Whether migration succeeded to completion. If <c>false</c>, some files were left behind.</returns>
        /// <exception cref="TimeoutException"></exception>
        public bool MigrateUserData(string path)
        {
            Logger.Log($@"Migrating osu! data from ""{Storage.GetFullPath(string.Empty)}"" to ""{path}""...");

            IDisposable realmBlocker = null;

            try
            {
                ManualResetEventSlim readyToRun = new ManualResetEventSlim();

                bool success = false;

                Scheduler.Add(() =>
                {
                    try
                    {
                        realmBlocker = realm.BlockAllOperations("migration");
                        success = true;
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Attempting to block all operations failed: {ex}", LoggingTarget.Database);
                    }

                    readyToRun.Set();
                }, false);

                if (!readyToRun.Wait(30000) || !success)
                    throw new TimeoutException("Attempting to block for migration took too long.");

                bool? cleanupSucceeded = (Storage as OsuStorage)?.Migrate(Host.GetStorage(path));

                Logger.Log(@"Migration complete!");
                return cleanupSucceeded != false;
            }
            finally
            {
                realmBlocker?.Dispose();
            }
        }

        protected virtual IBeatmapUpdater CreateBeatmapUpdater() => new BeatmapUpdater(BeatmapManager, difficultyCache, API, Storage, NoNetwork);

        protected override UserInputManager CreateUserInputManager() => new OsuUserInputManager();

        protected virtual BatteryInfo CreateBatteryInfo() => null;

        protected virtual Container CreateScalingContainer() => new DrawSizePreservingFillContainer();

        protected override Storage CreateStorage(GameHost host, Storage defaultStorage) => new OsuStorage(host, defaultStorage, DataPath);

        /// <summary>
        /// Creates an input settings subsection for an <see cref="InputHandler"/>.
        /// </summary>
        /// <remarks>Should be overriden per-platform to provide settings for platform-specific handlers.</remarks>
        public virtual SettingsSubsection CreateSettingsSubsectionFor(InputHandler handler)
        {
            // One would think that this could be moved to the `OsuGameDesktop` class, but doing so means that
            // OsuGameTestScenes will not show any input options (as they are based on OsuGame not OsuGameDesktop).
            //
            // This in turn makes it hard for ruleset creators to adjust input settings while testing their ruleset
            // within the test browser interface.
            if (RuntimeInfo.IsDesktop)
            {
                switch (handler)
                {
                    case ITabletHandler th:
                        return new TabletSettings(th);
                }
            }

            switch (handler)
            {
                case MouseHandler mh:
                    return new MouseSettings(mh);

                case JoystickHandler jh:
                    return new JoystickSettings(jh);

                case TouchHandler th:
                    return new TouchSettings(th);

                case PenHandler ph:
                    return new PenSettings(ph);

                case MidiHandler:
                    return new InputSubsection(handler);

                // return null for handlers that shouldn't have settings.
                default:
                    return null;
            }
        }

        private void onBeatmapChanged(ValueChangedEvent<WorkingBeatmap> beatmap)
        {
            if (IsLoaded && !ThreadSafety.IsUpdateThread)
                throw new InvalidOperationException("Global beatmap bindable must be changed from update thread.");

            Logger.Log($"Game-wide working beatmap updated to {beatmap.NewValue}");
        }

        private void onRulesetChanged(ValueChangedEvent<RulesetInfo> r)
        {
            if (IsLoaded && !ThreadSafety.IsUpdateThread)
                throw new InvalidOperationException("Global ruleset bindable must be changed from update thread.");

            Ruleset instance = null;

            if (r.NewValue?.Available == true)
            {
                try
                {
                    instance = r.NewValue.CreateInstance();
                }
                catch (Exception e)
                {
                    Rulesets.RulesetStore.LogRulesetFailure(r.NewValue, e);
                }
            }

            if (instance == null)
            {
                // reject the change if the ruleset is not available.
                revertRulesetChange();
                return;
            }

            var dict = new Dictionary<ModType, IReadOnlyList<Mod>>();

            try
            {
                foreach (ModType type in Enum.GetValues<ModType>())
                {
                    dict[type] = instance.GetModsFor(type)
                                         // Rulesets should never return null mods, but let's be defensive just in case.
                                         // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                                         .Where(mod => mod != null)
                                         .ToList();
                }
            }
            catch (Exception e)
            {
                Rulesets.RulesetStore.LogRulesetFailure(r.NewValue, e);
                revertRulesetChange();
                return;
            }

            AvailableMods.Value = dict;

            Events.Publish(new RulesetChangedEvent(r.NewValue) { UI = null });

            if (SelectedMods.Disabled)
                return;

            var convertedMods = SelectedMods.Value.Select(mod =>
            {
                var newMod = instance.CreateModFromAcronym(mod.Acronym);
                newMod?.CopyCommonSettingsFrom(mod);
                return newMod;
            }).Where(newMod => newMod != null).ToList();

            if (SpecialConfig?.Get<bool>(SpecialSetting.AllowIncompatibleMods) != true)
            {
                if (!ModUtils.CheckValidForGameplay(convertedMods, out var invalid))
                    invalid.ForEach(newMod => convertedMods.Remove(newMod));
            }

            SelectedMods.Value = convertedMods;

            void revertRulesetChange() => Ruleset.Value = r.OldValue?.Available == true ? r.OldValue : RulesetStore.AvailableRulesets.First();
        }

        private int allowableExceptions;

        /// <summary>
        /// Allows a maximum of one unhandled exception, per second of execution.
        /// </summary>
        /// <returns>Whether to ignore the exception and continue running.</returns>
        private bool onExceptionThrown(Exception ex)
        {
            if (Interlocked.Decrement(ref allowableExceptions) < 0)
            {
                Logger.Log("Too many unhandled exceptions, crashing out.");
                RulesetStore?.TryDisableCustomRulesetsCausing(ex);
                return false;
            }

            Logger.Log($"Unhandled exception has been allowed with {allowableExceptions} more allowable exceptions.");
            // restore the stock of allowable exceptions after a short delay.
            Task.Delay(1000).ContinueWith(_ => Interlocked.Increment(ref allowableExceptions));

            return true;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            RulesetStore?.Dispose();
            LocalConfig?.Dispose();
            SpecialConfig?.Dispose();

            beatmapUpdater?.Dispose();

            onlineAssetStore?.Dispose();

            realm?.Dispose();

            if (Host != null)
                Host.ExceptionThrown -= onExceptionThrown;

            RestartOnExitAction?.Invoke();
        }

        ControlPointInfo IBeatSyncProvider.ControlPoints => Beatmap.Value.BeatmapLoaded ? Beatmap.Value.Beatmap.ControlPointInfo : null;
        IClock IBeatSyncProvider.Clock => beatmapClock;
        ChannelAmplitudes IHasAmplitudes.CurrentAmplitudes => Beatmap.Value.TrackLoaded ? Beatmap.Value.Track.CurrentAmplitudes : ChannelAmplitudes.Empty;
    }
}
