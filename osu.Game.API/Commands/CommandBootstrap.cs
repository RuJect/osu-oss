// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Screens;
using osu.Game.API.Commands.Syntax;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Select;
using osu.Game.Users;
using osu.Game.Utils;

namespace osu.Game.API.Commands
{
    /// <summary>
    /// Owns a <see cref="CommandRegistry"/> and registers the built-in commands. Created by external hosts
    /// (see <c>osu.Desktop</c>) once the <see cref="OsuGame"/> instance is available.
    /// </summary>
    public class CommandBootstrap : IDisposable
    {
        private readonly OsuGame game;
        private readonly List<IDisposable> registrations = new List<IDisposable>();

        public CommandRegistry Registry { get; } = new CommandRegistry();

        public CommandBootstrap(OsuGame game)
        {
            this.game = game;

            registerBuiltIns();
        }

        private void registerBuiltIns()
        {
            registrations.Add(Registry.Add("exit")
                                    .Description("Exit the game.")
                                    .Aliases("quit")
                                    .Handler(() => game.AttemptExit()));

            registrations.Add(Registry.Add("search")
                                    .Description("Search beatmaps.")
                                    .Argument<string>("query", argument => argument.Description("The search query."))
                                    .Handler<string>(query =>
                                    {
                                        game.PerformFromScreen(screen =>
                                        {
                                            if (screen is SongSelect songSelect)
                                                songSelect.Search(query);
                                            else
                                                game.SearchBeatmapSet(query);
                                        }, new[] { typeof(SongSelect) });
                                    }));

            registrations.Add(Registry.Add("mods")
                                    .Description("Set the active mods.")
                                    .Argument<string[]>("mods", argument => argument
                                                                             .Description("Mod acronyms to enable.")
                                                                             .SuggestElements(suggestMods))
                                    .Handler<string[]>(acronyms =>
                                    {
                                        if (game.TrySetModsByAcronyms(acronyms, out string? error))
                                        {
                                            string applied = string.Join(", ", game.CurrentMods.Select(m => m.Acronym));
                                            game.Notify(new SimpleNotification { Text = applied.Length > 0 ? $"Mods set: {applied}" : "Mods cleared" });
                                        }
                                        else
                                        {
                                            game.Notify(new SimpleNotification { Text = error! });
                                        }
                                    }));

            registrations.Add(Registry.Add("skin")
                                    .Description("Select the next skin, or a skin by name.")
                                    .Aliases("skins")
                                    .Argument<string>("name", argument => argument
                                                                          .Description("The skin name, or 'random'.")
                                                                          .Optional()
                                                                          .Suggest(suggestSkins))
                                    .Handler<string?>(name =>
                                    {
                                        if (string.IsNullOrWhiteSpace(name))
                                        {
                                            game.SelectNextSkin();
                                            notifySkin();
                                        }
                                        else if (game.TrySelectSkin(name, out string? error))
                                        {
                                            notifySkin();
                                        }
                                        else
                                        {
                                            game.Notify(new SimpleNotification { Text = error! });
                                        }
                                    }));

            registrations.Add(Registry.Add("randomskin")
                                    .Description("Select a random skin.")
                                    .Aliases("random-skin")
                                    .Handler(() =>
                                    {
                                        game.SelectRandomSkin();
                                        notifySkin();
                                    }));

            registrations.Add(Registry.Add("resetmods")
                                    .Description("Clear all active mods.")
                                    .Aliases("nomods", "clearmods")
                                    .Handler(() =>
                                    {
                                        game.ClearMods();
                                        game.Notify(new SimpleNotification { Text = "Mods cleared" });
                                    }));

            registrations.Add(Registry.Add("ruleset")
                                    .Description("Select the next ruleset, or a ruleset by name.")
                                    .Aliases("mode")
                                    .Argument<string>("ruleset", argument => argument
                                                                             .Description("The ruleset short name.")
                                                                             .Optional()
                                                                             .Suggest(suggestRulesets))
                                    .Handler<string?>(name =>
                                    {
                                        if (string.IsNullOrWhiteSpace(name))
                                        {
                                            game.SelectNextRuleset();
                                            notifyRuleset();
                                        }
                                        else if (game.TrySelectRuleset(name, out string? error))
                                        {
                                            notifyRuleset();
                                        }
                                        else
                                        {
                                            game.Notify(new SimpleNotification { Text = error! });
                                        }
                                    }));

            registrations.Add(Registry.Add("randommap")
                                    .Description("Select a random beatmap.")
                                    .Aliases("randombeatmap", "random-map")
                                    .Handler(selectRandomBeatmap));

            registrations.Add(Registry.Add("profile")
                                    .Description("Open user profile overlay.")
                                    .Argument<string>("user", argument => argument
                                                                                .Description("Username or ID of the user. If omitted, shows current user's profile.")
                                                                                .Optional())
                                    .Argument<string>("ruleset", argument => argument
                                                                                            .Description("Ruleset short name (e.g. osu, taiko, fruits, mania). If omitted, uses user's default playmode.")
                                                                                            .Optional()
                                                                                            .Suggest(suggestRulesets))
                                    .Handler<string?, string?>((user, ruleset) => game.ShowUserProfile(user, ruleset)));
        }

        private IEnumerable<CommandSuggestion> suggestMods(ElementCompletionContext context)
        {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (context.ValueNode is CommandArrayNode array)
            {
                for (int i = 0; i < array.Items.Count; i++)
                {
                    if (i != context.Index && array.Items[i] is CommandScalarNode scalar)
                        used.Add(scalar.Text);
                }
            }

            return availableMods()
                   .Where(m => !used.Contains(m.Acronym))
                   .GroupBy(m => m.Acronym, StringComparer.OrdinalIgnoreCase)
                   .Select(g => g.First())
                   .OrderBy(m => m.Acronym, StringComparer.OrdinalIgnoreCase)
                   .Select(m => new CommandSuggestion(m.Acronym, Description: m.Name));
        }

        private IEnumerable<CommandSuggestion> suggestSkins(ArgumentCompletionContext context)
            => game.AvailableSkins
                   .Select(s => CommandSuggestions.String(s.Name, s.Name))
                   .Append(CommandSuggestions.String("random", "Select a random skin"));

        private IEnumerable<CommandSuggestion> suggestRulesets(ArgumentCompletionContext context)
            => game.AvailableRulesets.Select(r => new CommandSuggestion(r.ShortName, r.Name));

        private IEnumerable<Mod> availableMods()
            => ModUtils.FlattenMods(game.AvailableMods.Value.Values.SelectMany(mods => mods));

        private void notifySkin()
        {
            game.Notify(new SimpleNotification { Text = $"Skin: {game.CurrentSkinInfo.Name}" });
        }

        private void notifyRuleset()
        {
            game.Notify(new SimpleNotification { Text = $"Ruleset: {game.CurrentRuleset.Name}" });
        }

        private void selectRandomBeatmap()
        {
            game.PerformFromScreen(screen =>
            {
                if (screen is SongSelect songSelect)
                {
                    selectRandomBeatmap(songSelect);
                    return;
                }

                // Navigate to song select so the command works from anywhere in the game.
                var newSongSelect = new SoloSongSelect();
                screen.Push(newSongSelect);
                selectRandomBeatmap(newSongSelect);
            }, new[] { typeof(SongSelect) });
        }

        private static void selectRandomBeatmap(SongSelect songSelect)
        {
            if (songSelect.IsLoaded)
                songSelect.SelectRandomBeatmap();
            else
                songSelect.OnLoadComplete += _ => songSelect.SelectRandomBeatmap();
        }

        public void Dispose()
        {
            foreach (var registration in registrations)
                registration.Dispose();

            registrations.Clear();
        }
    }
}