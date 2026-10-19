// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Game.API.Commands;
using osu.Game.API.Commands.Syntax;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.API.UI.Commands
{
    /// <summary>
    /// A rofi/wofi-style command palette. Type to fuzzy-search the registered commands, then type the command's
    /// arguments using the syntax hints shown below the list.
    /// Toggled with <c>Ctrl+Space</c> while the game is running.
    /// </summary>
    public partial class CommandPaletteOverlay : OsuFocusedOverlayContainer
    {
        private const float panel_width = 720;
        private const float panel_top = 220;
        private const float popup_gap = 8;

        private const int max_results = 50;
        private const int max_visible_results = 6;

        private const float results_vertical_padding = 6;

        private readonly CommandRegistry registry;
        private readonly List<CommandMatch> matches = new List<CommandMatch>();
        private readonly List<CommandListEntry> entries = new List<CommandListEntry>();

        private CommandTextBox input = null!;
        private OsuScrollContainer resultsScroll = null!;
        private FillFlowContainer results = null!;
        private ArgumentHintBar hintBar = null!;
        private CommandSuggestionList suggestionList = null!;

        private string argument = string.Empty;
        private bool hasArgument;
        private string lastNameQuery = string.Empty;
        private int selectedIndex = -1;
        private CompletionCycle? cycle;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        public CommandPaletteOverlay(CommandRegistry registry)
        {
            this.registry = registry;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.Both;

            var panel = new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Masking = true,
                CornerRadius = 12,
                EdgeEffect = new EdgeEffectParameters
                {
                    Type = EdgeEffectType.Shadow,
                    Colour = Color4.Black.Opacity(0.5f),
                    Radius = 12,
                    Offset = new Vector2(0, 4),
                },
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Colour4.FromHex("1B1B24"),
                    },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Children = new Drawable[]
                        {
                            createInputRow(),
                            separator(),
                            hintBar = new ArgumentHintBar(),
                            separator(),
                            resultsScroll = new OsuScrollContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = CommandListEntry.HEIGHT,
                                ScrollbarOverlapsContent = true,
                                Child = results = new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Vertical,
                                    Padding = new MarginPadding { Vertical = results_vertical_padding },
                                },
                            },
                        },
                    },
                },
            };

            Add(new Container
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Y = panel_top,
                Width = panel_width,
                AutoSizeAxes = Axes.Y,
                Child = panel,
            });

            Add(suggestionList = new CommandSuggestionList
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.BottomCentre,
                Y = panel_top - popup_gap,
                Width = panel_width,
                EdgeEffect = new EdgeEffectParameters
                {
                    Type = EdgeEffectType.Shadow,
                    Colour = Color4.Black.Opacity(0.5f),
                    Radius = 12,
                    Offset = new Vector2(0, 4),
                },
            });
        }

        private Drawable createInputRow()
        {
            var row = new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = 58,
                Children = new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Text = "/",
                        Font = OsuFont.GetFont(size: 20, weight: FontWeight.Bold),
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Position = new Vector2(18, 0),
                        Colour = colours.Blue1,
                        Shadow = false,
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 40,
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Padding = new MarginPadding { Left = 44, Right = 18 },
                        Child = input = new CommandTextBox
                        {
                            RelativeSizeAxes = Axes.X,
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            PlaceholderText = "Type a command...",
                            FontSize = 18,
                        },
                    },
                },
            };

            return row;
        }

        private static Drawable separator() => new Box
        {
            RelativeSizeAxes = Axes.X,
            Height = 1,
            Colour = Colour4.FromHex("33333F"),
        };

        protected override void LoadComplete()
        {
            base.LoadComplete();

            input.Current.BindValueChanged(_ => rebuild());
            input.HandleKey = onInputKeyDown;
            suggestionList.Clicked = onSuggestionClicked;
            rebuild();
        }

        protected override void PopIn()
        {
            this.FadeIn(200, Easing.OutQuint);

            Schedule(() =>
            {
                input.Current.Value = string.Empty;
                input.TakeFocus();
                rebuild();
            });
        }

        protected override void PopOut()
        {
            base.PopOut();

            this.FadeOut(200, Easing.OutQuint);

            cycle = null;
            suggestionList.PopupVisible = false;

            if (input.HasFocus)
                GetContainingFocusManager()?.ChangeFocus(null);
        }

        public override bool AcceptsFocus => true;

        protected override void OnFocus(FocusEvent e)
        {
            input.TakeFocus();
            base.OnFocus(e);
        }

        private bool onInputKeyDown(KeyDownEvent e)
        {
            switch (e.Key)
            {
                case Key.Up:
                    moveSelection(-1);
                    return true;

                case Key.Down:
                    moveSelection(1);
                    return true;

                case Key.Tab:
                    if (!hasArgument)
                        acceptSelected();
                    else
                        cycleSuggestion(e.ShiftPressed ? -1 : 1);

                    return true;

                case Key.Enter:
                case Key.KeypadEnter:
                    execute();
                    return true;
            }

            return false;
        }

        private void moveSelection(int direction)
        {
            if (entries.Count == 0)
                return;

            cycle = null;
            selectedIndex = (selectedIndex + direction + entries.Count) % entries.Count;
            updateSelection();
            updateHint();
            updateSuggestionPopup();
        }

        private void acceptSelected()
        {
            var command = getSelected();

            if (command == null || hasArgument)
                return;

            setInputForCommand(command, command.Arguments.Count > 0);
        }

        private void cycleSuggestion(int direction)
        {
            var command = getSelected();
            var completion = hintBar.Completion;

            if (command == null || completion == null)
                return;

            if (cycle == null || !cycle.Matches(command, input.Current.Value))
            {
                if (completion.Suggestions.Count == 0)
                    return;

                cycle = new CompletionCycle(command, completion.Suggestions);
            }

            int count = cycle.Suggestions.Count;

            if (count == 0)
                return;

            int index = cycle.Index < 0
                ? (direction > 0 ? 0 : count - 1)
                : (cycle.Index + direction + count) % count;

            cycle.Index = index;
            insertSuggestion(cycle.Suggestions[index].InsertText, completion.ReplacementSpan);
        }

        private void onSuggestionClicked(int index)
        {
            var command = getSelected();
            var completion = hintBar.Completion;

            if (command == null || completion == null)
                return;

            var suggestions = suggestionList.Suggestions;

            if (index < 0 || index >= suggestions.Count)
                return;

            cycle = new CompletionCycle(command, suggestions)
            {
                Index = index,
            };

            insertSuggestion(suggestions[index].InsertText, completion.ReplacementSpan);
            input.TakeFocus();
        }

        private void insertSuggestion(string insertText, CommandTextSpan span)
        {
            string current = input.Current.Value;
            int baseOffset = current.Length - argument.Length;
            int start = Math.Clamp(baseOffset + span.Start, 0, current.Length);
            int end = Math.Clamp(baseOffset + span.End, start, current.Length);

            string updated = string.Concat(current.AsSpan(0, start), insertText.AsSpan(), current.AsSpan(end));

            if (cycle != null)
                cycle.ExpectedText = updated;

            input.Current.Value = updated;
            rebuild();
        }

        private void execute()
        {
            var command = getSelected();

            if (command == null)
                return;

            if (!hasArgument && command.Arguments.Count > 0 && command.Arguments.Any(a => !a.IsOptional))
            {
                beginArguments(command);
                return;
            }

            var result = registry.Execute(command, hasArgument ? argument : string.Empty);

            if (result.Succeeded)
                Hide();
            else
            {
                hintBar.Show(command, argument, result.Message);
                suggestionList.PopupVisible = false;
            }
        }

        private void beginArguments(CommandDefinition command) => setInputForCommand(command, true);

        private void setInputForCommand(CommandDefinition command, bool withSpace)
        {
            input.Current.Value = withSpace ? command.Name + " " : command.Name;
            rebuild();

            int index = matches.FindIndex(m => m.Command == command);

            if (index >= 0)
            {
                selectedIndex = index;
                updateSelection();
                updateHint();
                updateSuggestionPopup();
            }
        }

        private CommandDefinition? getSelected()
            => selectedIndex >= 0 && selectedIndex < matches.Count ? matches[selectedIndex].Command : null;

        private void rebuild()
        {
            var parsed = CommandInput.Parse(input.Current.Value);

            string nameQuery = parsed.Name;

            argument = parsed.Argument;
            hasArgument = parsed.HasArgument;

            if (cycle != null && cycle.ExpectedText != input.Current.Value)
                cycle = null;

            var previouslySelected = getSelected();
            bool sameQuery = nameQuery == lastNameQuery;
            lastNameQuery = nameQuery;

            matches.Clear();
            matches.AddRange(registry.Search(nameQuery).Take(max_results));

            selectedIndex = matches.Count > 0 ? 0 : -1;

            if (sameQuery && previouslySelected != null)
            {
                int index = matches.FindIndex(m => m.Command == previouslySelected);

                if (index >= 0)
                    selectedIndex = index;
            }

            rebuildEntries();
            updateHint();
            updateSuggestionPopup();
        }

        private void rebuildEntries()
        {
            results.Clear();
            entries.Clear();

            for (int i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                int index = i;

                var entry = new CommandListEntry(match.Command, match.Indices)
                {
                    Selected = i == selectedIndex,
                };

                entry.Clicked = () =>
                {
                    selectedIndex = index;
                    execute();
                };

                entries.Add(entry);
                results.Add(entry);
            }

            if (matches.Count == 0)
            {
                results.Add(new OsuSpriteText
                {
                    Text = "No matching commands",
                    Font = OsuFont.GetFont(size: 14, italics: true),
                    Colour = colours.Gray5,
                    Shadow = false,
                    Margin = new MarginPadding { Horizontal = CommandListEntry.PADDING, Vertical = 8 },
                });
            }

            resultsScroll.Height = matches.Count == 0
                ? 54
                : Math.Min(matches.Count, max_visible_results) * CommandListEntry.HEIGHT + results_vertical_padding * 2;

            scrollSelectionIntoView();
        }

        private void updateSelection()
        {
            for (int i = 0; i < entries.Count; i++)
                entries[i].Selected = i == selectedIndex;

            scrollSelectionIntoView();
        }

        private void scrollSelectionIntoView()
        {
            if (selectedIndex < 0 || selectedIndex >= entries.Count)
                return;

            ScheduleAfterChildren(() => resultsScroll.ScrollIntoView(entries[selectedIndex], false));
        }

        private void updateHint()
        {
            var command = getSelected();

            if (command == null)
                hintBar.Show(null);
            else
                hintBar.Show(command, hasArgument ? argument : string.Empty);
        }

        private void updateSuggestionPopup()
        {
            var command = getSelected();

            if (command == null || !hasArgument)
            {
                suggestionList.PopupVisible = false;
                return;
            }

            IReadOnlyList<CommandSuggestion> suggestions;
            int selection = -1;

            if (cycle != null && cycle.Matches(command, input.Current.Value))
            {
                suggestions = cycle.Suggestions;
                selection = cycle.Index;
            }
            else
            {
                suggestions = hintBar.Completion?.Suggestions ?? Array.Empty<CommandSuggestion>();
            }

            if (suggestions.Count == 0)
            {
                suggestionList.PopupVisible = false;
                return;
            }

            suggestionList.SetSuggestions(suggestions);
            suggestionList.PopupVisible = true;
            suggestionList.SetSelection(selection);
        }

        private sealed class CompletionCycle
        {
            public CommandDefinition Command { get; }
            public IReadOnlyList<CommandSuggestion> Suggestions { get; }
            public string ExpectedText { get; set; }
            public int Index { get; set; } = -1;

            public CompletionCycle(CommandDefinition command, IReadOnlyList<CommandSuggestion> suggestions)
            {
                Command = command;
                Suggestions = suggestions;
                ExpectedText = string.Empty;
            }

            public bool Matches(CommandDefinition command, string text)
                => Command == command && ExpectedText == text;
        }

        private partial class CommandTextBox : SearchTextBox
        {
            public Func<KeyDownEvent, bool>? HandleKey;

            protected override bool OnKeyDown(KeyDownEvent e)
            {
                switch (e.Key)
                {
                    case Key.Up:
                    case Key.Down:
                    case Key.Tab:
                        if (HandleKey?.Invoke(e) == true)
                            return true;
                        break;

                    case Key.Enter:
                    case Key.KeypadEnter:
                        if (ImeCompositionActive)
                            return base.OnKeyDown(e);

                        if (HandleKey?.Invoke(e) == true)
                            return true;
                        break;
                }

                return base.OnKeyDown(e);
            }
        }
    }
}
