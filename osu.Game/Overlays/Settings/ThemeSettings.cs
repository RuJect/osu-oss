// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.IO;
using osu.Game.Overlays.Dialog;
using osu.Game.Theming;
using osuTK;

namespace osu.Game.Overlays.Settings
{
    /// <summary>
    /// Theme picker with an inline preview. Because theme colours are baked into drawables at
    /// construction time, applying a selection requires a restart; the preview gives immediate
    /// feedback before the restart is confirmed.
    /// </summary>
    public partial class ThemeSettings : CompositeDrawable
    {
        [Resolved]
        private SpecialConfigManager config { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private OsuGame? game { get; set; }

        [Resolved(CanBeNull = true)]
        private IDialogOverlay? dialogOverlay { get; set; }

        private readonly Bindable<ThemeSnapshot> selectedTheme = new Bindable<ThemeSnapshot>();

        private Bindable<bool> aggressiveTinting = null!;

        private SettingsDropdown<ThemeSnapshot> dropdown = null!;
        private ThemePreviewPanel preview = null!;
        private bool suppressSelectionChange;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            ThemeSnapshot initial = ThemeManager.AvailableThemes.FirstOrDefault(t => string.Equals(t.Id, config.Get<string>(SpecialSetting.Theme), StringComparison.OrdinalIgnoreCase))
                                    ?? ThemeManager.Current;

            selectedTheme.Value = initial;
            aggressiveTinting = config.GetBindable<bool>(SpecialSetting.AggressiveTinting);

            dropdown = new SettingsDropdown<ThemeSnapshot>
            {
                LabelText = "UI theme",
                TooltipText = "The theme is applied after restarting the game. The preview below updates immediately.",
                Items = ThemeManager.AvailableThemes,
                Current = selectedTheme,
            };

            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 10),
                Padding = new MarginPadding { Vertical = 5 },
                Children = new Drawable[]
                {
                    dropdown,
                    preview = new ThemePreviewPanel
                    {
                        RelativeSizeAxes = Axes.X,
                        Current = { BindTarget = selectedTheme },
                        AggressiveTinting = { BindTarget = aggressiveTinting },
                    },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 8),
                        Children = new Drawable[]
                        {
                            new SettingsButton
                            {
                                Text = "Reload themes",
                                Action = reloadThemes,
                                Margin = new MarginPadding(),
                            },
                            new SettingsButton
                            {
                                Text = "Open themes folder",
                                Action = openThemesFolder,
                                Margin = new MarginPadding(),
                            },
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            selectedTheme.BindValueChanged(onThemeSelected, true);
        }

        private void onThemeSelected(ValueChangedEvent<ThemeSnapshot> change)
        {
            if (change.OldValue == null || change.NewValue == null || suppressSelectionChange)
                return;

            if (change.NewValue.Id == change.OldValue.Id)
                return;

            if (game == null || dialogOverlay == null)
            {
                applyTheme(change.NewValue);
                return;
            }

            dialogOverlay.Push(new ConfirmDialog(
                $"The game will close to apply \"{change.NewValue.Name}\". Open it again to see the new theme.",
                () => applyTheme(change.NewValue),
                () =>
                {
                    suppressSelectionChange = true;
                    selectedTheme.Value = change.OldValue;
                    suppressSelectionChange = false;
                }));
        }

        private void applyTheme(ThemeSnapshot theme)
        {
            config.GetBindable<string>(SpecialSetting.Theme).Value = theme.Id;
            game?.AttemptExit();
        }

        private void reloadThemes()
        {
            ThemeManager.Reload(config.Get<string>(SpecialSetting.Theme));

            suppressSelectionChange = true;

            dropdown.Items = ThemeManager.AvailableThemes;
            selectedTheme.Value = ThemeManager.AvailableThemes.FirstOrDefault(t => string.Equals(t.Id, config.Get<string>(SpecialSetting.Theme), StringComparison.OrdinalIgnoreCase))
                                  ?? ThemeManager.Current;

            suppressSelectionChange = false;
        }

        private void openThemesFolder()
        {
            if (ThemeManager.ThemesPath == null)
                return;

            Directory.CreateDirectory(ThemeManager.ThemesPath);
            SpecialStorage.GetRoot(storage).PresentFileExternally(ThemeManager.THEMES_FOLDER);
        }

        private partial class ThemePreviewPanel : CompositeDrawable
        {
            public readonly Bindable<ThemeSnapshot> Current = new Bindable<ThemeSnapshot>();
            public readonly Bindable<bool> AggressiveTinting = new BindableBool();

            private Container previewSurface = null!;
            private Box surfaceBox = null!;
            private Box accentBar = null!;
            private Box sampleCard = null!;
            private Box sampleAccent = null!;
            private Box sampleLinePrimary = null!;
            private Box sampleLineSecondary = null!;
            private Container sampleContainer = null!;
            private OsuSpriteText nameText = null!;
            private OsuSpriteText authorText = null!;
            private OsuTextFlowContainer descriptionText = null!;
            private FillFlowContainer swatches = null!;

            [BackgroundDependencyLoader]
            private void load()
            {
                Height = 200;
                Masking = true;

                AddInternal(previewSurface = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    Children = new Drawable[]
                    {
                        surfaceBox = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                        },
                        accentBar = new Box
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 3,
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                        },
                    },
                });

                AddInternal(new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Padding = new MarginPadding { Horizontal = 18, Top = 16 },
                    Spacing = new Vector2(0, 2),
                    Children = new Drawable[]
                    {
                        nameText = new OsuSpriteText
                        {
                            Font = OsuFont.GetFont(size: 24, weight: FontWeight.Bold),
                        },
                        authorText = new OsuSpriteText
                        {
                            Font = OsuFont.GetFont(size: 12, weight: FontWeight.SemiBold),
                        },
                        descriptionText = new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 13))
                        {
                            RelativeSizeAxes = Axes.X,
                            Width = 0.72f,
                            AutoSizeAxes = Axes.Y,
                            Padding = new MarginPadding { Top = 4 },
                        },
                    },
                });

                AddInternal(sampleContainer = new Container
                {
                    Anchor = Anchor.BottomRight,
                    Origin = Anchor.BottomRight,
                    Margin = new MarginPadding(16),
                    Size = new Vector2(140, 58),
                    Masking = true,
                    Children = new Drawable[]
                    {
                        sampleCard = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                        },
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding(12),
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 5),
                            Children = new Drawable[]
                            {
                                sampleAccent = new Box
                                {
                                    Size = new Vector2(56, 5),
                                },
                                sampleLinePrimary = new Box
                                {
                                    Size = new Vector2(108, 4),
                                },
                                sampleLineSecondary = new Box
                                {
                                    Size = new Vector2(76, 4),
                                },
                            },
                        },
                    },
                });

                AddInternal(swatches = new FillFlowContainer
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Margin = new MarginPadding(16),
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(4, 0),
                });
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                Current.BindValueChanged(_ => updatePreview(), true);
                AggressiveTinting.BindValueChanged(_ => updatePreview());
            }

            private void updatePreview()
            {
                ThemeSnapshot theme = Current.Value;
                bool aggressive = AggressiveTinting.Value;

                Colour4 tint(Colour4 colour) => aggressive ? theme.ApplyAggressiveTint(colour) : colour;

                Colour4 surface = tint(theme.TryGetColour("B5", out Colour4 b5) ? b5 : new Colour4(0.09f, 0.09f, 0.11f, 1));
                Colour4 accent = tint(theme.PreviewColours.Count > 0 ? theme.PreviewColours[0] : Colour4.White);
                Colour4 content = tint(theme.TryGetColour("PurpleLighter", out Colour4 lighter) ? lighter : Colour4.White);

                surfaceBox.Colour = surface;
                accentBar.Colour = accent;
                sampleCard.Colour = surface.Lighten(0.12f);
                sampleAccent.Colour = accent;
                sampleLinePrimary.Colour = content.Opacity(0.75f);
                sampleLineSecondary.Colour = content.Opacity(0.35f);

                CornerRadius = theme.CornerRadius;
                previewSurface.CornerRadius = theme.CornerRadius;
                sampleContainer.CornerRadius = Math.Max(2, theme.CornerRadius * 0.65f);

                nameText.Text = theme.Name;
                nameText.Colour = content;
                authorText.Text = string.IsNullOrEmpty(theme.Author) ? (theme.IsBuiltIn ? "Built-in theme" : "Custom theme") : $"by {theme.Author}";
                authorText.Colour = content.Opacity(0.55f);
                descriptionText.Text = theme.Description;
                descriptionText.Colour = content.Opacity(0.75f);

                swatches.Clear();

                foreach (Colour4 colour in theme.PreviewColours.Take(8))
                {
                    swatches.Add(new Container
                    {
                        Size = new Vector2(18),
                        Masking = true,
                        CornerRadius = Math.Clamp(theme.CornerRadius / 3f, 2, 8),
                        Child = new Box { RelativeSizeAxes = Axes.Both, Colour = tint(colour) },
                    });
                }
            }
        }
    }
}
