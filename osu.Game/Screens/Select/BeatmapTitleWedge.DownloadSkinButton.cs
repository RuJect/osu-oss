// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Select
{
    public partial class BeatmapTitleWedge
    {
        public partial class DownloadSkinButton : VisibilityContainer
        {
            private DownloadButton button = null!;
            private Bindable<bool>? showButton;
            private WorkingBeatmap? beatmap;

            [Resolved]
            private SkinManager skins { get; set; } = null!;

            [Resolved]
            private SpecialConfigManager specialConfig { get; set; } = null!;

            [Resolved(canBeNull: true)]
            private INotificationOverlay? notifications { get; set; }

            private bool importing;

            public override bool IsPresent => State.Value == Visibility.Visible && base.IsPresent;

            public DownloadSkinButton()
            {
                AutoSizeAxes = Axes.Both;
                State.Value = Visibility.Hidden;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                showButton = specialConfig.GetBindable<bool>(SpecialSetting.ShowBeatmapSkinDownloadButton);
                showButton.BindValueChanged(_ => updateVisibility());

                Child = button = new DownloadButton
                {
                    Action = downloadSkin,
                };

                updateVisibility();
            }

            public void SetBeatmap(WorkingBeatmap? newBeatmap)
            {
                beatmap = newBeatmap;
                updateVisibility();
            }

            private void updateVisibility()
            {
                if (showButton == null)
                    return;

                bool hasCustomSkin = beatmap != null && BeatmapSkinArchive.HasCustomSkin(beatmap);
                State.Value = showButton.Value && hasCustomSkin ? Visibility.Visible : Visibility.Hidden;
            }

            private void downloadSkin()
            {
                if (importing || beatmap == null)
                    return;

                var currentBeatmap = beatmap;

                importing = true;
                button.SetImporting(true);

                var notification = new ProgressNotification
                {
                    State = ProgressNotificationState.Active,
                    Text = "Installing beatmap skin...",
                };

                notifications?.Post(notification);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        using (MemoryStream? archive = BeatmapSkinArchive.CreateArchive(currentBeatmap, out string filename))
                        {
                            if (archive == null)
                            {
                                notification.State = ProgressNotificationState.Cancelled;
                                notification.Text = "No beatmap skin files were found.";
                                return;
                            }

                            var imported = (await skins.Import(notification, new[] { new ImportTask(archive, filename) }).ConfigureAwait(false)).FirstOrDefault();

                            if (imported != null)
                                Schedule(() => skins.CurrentSkinInfo.Value = imported);
                        }
                    }
                    catch (Exception e)
                    {
                        notification.State = ProgressNotificationState.Cancelled;
                        Logger.Error(e, "Failed to install beatmap skin.");
                        Schedule(() => notifications?.Post(new SimpleErrorNotification
                        {
                            Text = "Failed to install beatmap skin.",
                            Icon = FontAwesome.Solid.Times,
                        }));
                    }
                    finally
                    {
                        Schedule(() =>
                        {
                            importing = false;
                            button.SetImporting(false);
                        });
                    }
                });
            }

            protected override void PopIn()
            {
                button.FadeIn(100, Easing.OutQuint);
            }

            protected override void PopOut()
            {
                button.FadeOut(100, Easing.OutQuint);
            }

            private partial class DownloadButton : OsuClickableContainer, IHasTooltip
            {
                private Box hoverLayer = null!;
                private SpriteIcon icon = null!;
                private LoadingSpinner spinner = null!;

                public override LocalisableString TooltipText => "Install beatmap skin";

                public DownloadButton()
                {
                    AutoSizeAxes = Axes.X;
                    Height = 30;
                    Masking = true;
                    CornerRadius = 5;
                    Shear = OsuGame.SHEAR;
                }

                [BackgroundDependencyLoader]
                private void load(OverlayColourProvider colourProvider)
                {
                    AddRange(new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Color4.Black.Opacity(0.2f),
                        },
                        new Container
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            AutoSizeAxes = Axes.Both,
                            Margin = new MarginPadding { Horizontal = 10, Vertical = 5 },
                            Shear = -OsuGame.SHEAR,
                            Child = new Container
                            {
                                Size = new Vector2(OsuFont.Style.Heading2.Size),
                                Children = new Drawable[]
                                {
                                    icon = new SpriteIcon
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Icon = OsuIcon.SkinA,
                                        Colour = colourProvider.Content2,
                                    },
                                    spinner = new LoadingSpinner
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        State = { Value = Visibility.Hidden },
                                    },
                                },
                            },
                        },
                        hoverLayer = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Alpha = 0,
                            Colour = Colour4.White.Opacity(0.1f),
                            Blending = BlendingParameters.Additive,
                        },
                    });
                }

                public void SetImporting(bool importing)
                {
                    Enabled.Value = !importing;
                    icon.Alpha = importing ? 0 : 1;
                    spinner.State.Value = importing ? Visibility.Visible : Visibility.Hidden;
                }

                protected override bool OnHover(HoverEvent e)
                {
                    hoverLayer.FadeIn(500, Easing.OutQuint);
                    return true;
                }

                protected override void OnHoverLost(HoverLostEvent e)
                {
                    base.OnHoverLost(e);
                    hoverLayer.FadeOut(500, Easing.OutQuint);
                }
            }
        }
    }
}
