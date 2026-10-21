// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Backdrops;
using osu.Game.Graphics.Containers;
using osu.Game.Overlays.Notifications;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Theming;
using osuTK;
using osuTK.Graphics;
using NotificationsStrings = osu.Game.Localisation.NotificationsStrings;

namespace osu.Game.Overlays
{
    public partial class NotificationOverlay : OsuFocusedOverlayContainer, INamedOverlayComponent, INotificationOverlay
    {
        public IconUsage Icon => OsuIcon.Notification;
        public LocalisableString Title => NotificationsStrings.HeaderTitle;
        public LocalisableString Description => NotificationsStrings.HeaderDescription;

        protected override double PopInOutSampleBalance => OsuGameBase.SFX_STEREO_STRENGTH;

        public const float WIDTH = 320;

        public const float TRANSITION_LENGTH = 600;

        public IEnumerable<Notification> AllNotifications =>
            IsLoaded ? toastTray.Notifications.Concat(sections.SelectMany(s => s.Notifications)) : Array.Empty<Notification>();

        private FlowContainer<NotificationSection> sections = null!;

        [Resolved]
        private AudioManager audio { get; set; } = null!;

        [Resolved]
        private OsuGame? game { get; set; }

        [Resolved(canBeNull: true)]
        private SpecialConfigManager? specialConfig { get; set; }

        [Cached]
        private OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Purple);

        /// <summary>
        /// The screen edge this panel is currently docked to.
        /// </summary>
        public NotificationPanelPosition PanelPosition { get; private set; } = NotificationPanelPosition.Right;

        /// <summary>
        /// The screen corner temporary notifications are currently displayed in.
        /// </summary>
        public NotificationDisplayPosition DisplayPosition { get; private set; } = NotificationDisplayPosition.TopRight;

        private Bindable<NotificationPanelPosition>? panelPositionBindable;
        private Bindable<NotificationDisplayPosition>? displayPositionBindable;
        private Bindable<bool>? muteToastNotificationsBindable;

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
        {
            if (State.Value == Visibility.Visible)
                return base.ReceivePositionalInputAt(screenSpacePos);

            if (toastTray.IsDisplayingToasts)
                return toastTray.ReceivePositionalInputAt(screenSpacePos);

            return false;
        }

        public override bool PropagatePositionalInputSubTree => base.PropagatePositionalInputSubTree || toastTray.IsDisplayingToasts;

        private NotificationOverlayToastTray toastTray = null!;

        private Container mainContent = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            Width = WIDTH;
            RelativeSizeAxes = Axes.Y;

            Children = new Drawable[]
            {
                toastTray = new NotificationOverlayToastTray
                {
                    ForwardNotificationToPermanentStore = addPermanently,
                },
                mainContent = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    EdgeEffect = new EdgeEffectParameters
                    {
                        Colour = Color4.Black.Opacity(0),
                        Type = EdgeEffectType.Shadow,
                        Radius = 10,
                        Hollow = true,
                    },
                    Children = new Drawable[]
                    {
                        new GlassBackdrop
                        {
                            TintColour = colourProvider.Background4.Opacity(ThemeManager.Current.GetSurfaceAlpha("notification", 0.9f)),
                        },
                        new OsuScrollContainer
                        {
                            Masking = true,
                            RelativeSizeAxes = Axes.Both,
                            Children = new[]
                            {
                                sections = new FillFlowContainer<NotificationSection>
                                {
                                    Direction = FillDirection.Vertical,
                                    AutoSizeAxes = Axes.Y,
                                    RelativeSizeAxes = Axes.X,
                                    Children = new[]
                                    {
                                        // The main section adds as a catch-all for notifications which don't group into other sections.
                                        new NotificationSection(AccountsStrings.NotificationsTitle),
                                        new NotificationSection(NotificationsStrings.RunningTasks, new[] { typeof(ProgressNotification) }),
                                    }
                                }
                            }
                        }
                    }
                },
            };

            panelPositionBindable = specialConfig?.GetBindable<NotificationPanelPosition>(SpecialSetting.NotificationPanelPosition);
            displayPositionBindable = specialConfig?.GetBindable<NotificationDisplayPosition>(SpecialSetting.NotificationDisplayPosition);
            muteToastNotificationsBindable = specialConfig?.GetBindable<bool>(SpecialSetting.MuteToastNotifications);

            PanelPosition = panelPositionBindable?.Value ?? NotificationPanelPosition.Right;
            DisplayPosition = displayPositionBindable?.Value ?? NotificationDisplayPosition.TopRight;

            updatePanelPosition();

            toastTray.PanelPosition = PanelPosition;
            toastTray.DisplayPosition = DisplayPosition;

            panelPositionBindable?.BindValueChanged(e =>
            {
                PanelPosition = e.NewValue;
                updatePanelPosition();
                toastTray.PanelPosition = PanelPosition;
            }, true);

            displayPositionBindable?.BindValueChanged(e =>
            {
                DisplayPosition = e.NewValue;
                toastTray.DisplayPosition = DisplayPosition;
            }, true);

            muteToastNotificationsBindable?.BindValueChanged(e =>
            {
                if (e.NewValue)
                    toastTray.FlushAllToasts(true);
            });
        }

        private bool muteToastNotifications => muteToastNotificationsBindable?.Value ?? false;

        private float hiddenX => PanelPosition == NotificationPanelPosition.Left ? -WIDTH : WIDTH;

        private void updatePanelPosition()
        {
            Anchor anchor = PanelPosition == NotificationPanelPosition.Left ? Anchor.TopLeft : Anchor.TopRight;

            Anchor = anchor;
            Origin = anchor;

            ClearTransforms();
            X = State.Value == Visibility.Visible ? 0 : hiddenX;
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            updateToastTrayPosition();
        }

        private void updateToastTrayPosition()
        {
            if (toastTray == null || Parent == null)
                return;

            float overlayLeft = PanelPosition == NotificationPanelPosition.Left ? X : Parent.DrawWidth - WIDTH + X;

            bool leftAligned = DisplayPosition is NotificationDisplayPosition.TopLeft or NotificationDisplayPosition.BottomLeft;
            bool topAligned = DisplayPosition is NotificationDisplayPosition.TopLeft or NotificationDisplayPosition.TopRight;

            float trayHeight = toastTray.Padding.Top + toastTray.ContentHeight + toastTray.Padding.Bottom;

            Vector2 target = new Vector2(
                (leftAligned ? 0 : Parent.DrawWidth - toastTray.DrawWidth) - overlayLeft,
                topAligned ? 0 : Parent.DrawHeight - trayHeight);

            if (toastTray.Position != target)
                toastTray.Position = target;
        }

        private ScheduledDelegate? notificationsEnabler;

        private void updateProcessingMode()
        {
            bool enabled = OverlayActivationMode.Value != OverlayActivation.Disabled || State.Value == Visibility.Visible;

            notificationsEnabler?.Cancel();

            if (enabled)
                // we want a slight delay before toggling notifications on to avoid the user becoming overwhelmed.
                notificationsEnabler = Scheduler.AddDelayed(() => processingPosts = true, State.Value == Visibility.Visible ? 0 : 250);
            else
            {
                processingPosts = false;
                toastTray.FlushAllToasts();
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            State.BindValueChanged(_ => updateProcessingMode());
            OverlayActivationMode.BindValueChanged(_ => updateProcessingMode(), true);
        }

        public IBindable<int> UnreadCount => unreadCount;

        public int ToastCount => toastTray.UnreadCount;

        private readonly BindableInt unreadCount = new BindableInt();

        private int runningDepth;

        private readonly Scheduler postScheduler = new Scheduler();
        private readonly Scheduler criticalPostScheduler = new Scheduler();

        public override bool IsPresent =>
            // Delegate presence as we need to consider the toast tray in addition to the main overlay.
            State.Value == Visibility.Visible || mainContent.IsPresent || toastTray.IsPresent || postScheduler.HasPendingTasks || criticalPostScheduler.HasPendingTasks;

        private bool processingPosts = true;

        private double? lastSamplePlayback;

        public void Post(Notification notification) => (notification.IsCritical ? criticalPostScheduler : postScheduler).Add(() =>
        {
            ++runningDepth;

            Logger.Log($"⚠️ {notification.Text}");

            notification.Closed += () => notificationClosed(notification);

            if (notification is IHasCompletionTarget hasCompletionTarget)
                hasCompletionTarget.CompletionTarget ??= Post;

            playDebouncedSample(notification.PopInSampleName);

            if (notification.IsImportant)
            {
                game?.Window?.Flash();
                notification.Closed += () => game?.Window?.CancelFlash();
            }

            if (State.Value == Visibility.Hidden && !muteToastNotifications)
            {
                notification.IsInToastTray = true;
                toastTray.Post(notification);
            }
            else
                addPermanently(notification);

            updateCounts();
        });

        private void addPermanently(Notification notification)
        {
            notification.IsInToastTray = false;
            notification.DisplayPosition = PanelPosition == NotificationPanelPosition.Left
                ? NotificationDisplayPosition.TopLeft
                : NotificationDisplayPosition.TopRight;

            var ourType = notification.GetType();
            int depth = notification.DisplayOnTop ? -runningDepth : runningDepth;

            var section = sections.Children.FirstOrDefault(s => s.AcceptedNotificationTypes?.Any(accept => accept.IsAssignableFrom(ourType)) == true)
                          ?? sections.First();

            section.Add(notification, depth);

            updateCounts();
        }

        protected override void Update()
        {
            base.Update();

            criticalPostScheduler.Update();

            if (processingPosts)
                postScheduler.Update();
        }

        protected override void PopIn()
        {
            this.MoveToX(0, TRANSITION_LENGTH, Easing.OutQuint);
            mainContent.FadeTo(1, TRANSITION_LENGTH / 2, Easing.OutQuint);
            mainContent.FadeEdgeEffectTo(WaveContainer.SHADOW_OPACITY, WaveContainer.APPEAR_DURATION, Easing.Out);

            toastTray.FlushAllToasts(true);
        }

        protected override void PopOut()
        {
            base.PopOut();

            markAllRead();

            this.MoveToX(hiddenX, TRANSITION_LENGTH, Easing.OutQuint);
            mainContent.FadeTo(0, TRANSITION_LENGTH / 2, Easing.OutQuint);
            mainContent.FadeEdgeEffectTo(0, WaveContainer.DISAPPEAR_DURATION, Easing.In);
        }

        private void notificationClosed(Notification notification) => Schedule(() =>
        {
            updateCounts();

            // this debounce is currently shared between popin/popout sounds, which means one could potentially not play when the user is expecting it.
            // popout is constant across all notification types, and should therefore be handled using playback concurrency instead, but seems broken at the moment.
            playDebouncedSample(notification.PopOutSampleName);
        });

        private void playDebouncedSample(string sampleName)
        {
            if (string.IsNullOrEmpty(sampleName))
                return;

            if (lastSamplePlayback == null || Time.Current - lastSamplePlayback > OsuGameBase.SAMPLE_DEBOUNCE_TIME)
            {
                audio.Samples.Get(sampleName)?.Play();
                lastSamplePlayback = Time.Current;
            }
        }

        private void markAllRead()
        {
            sections.Children.ForEach(s => s.MarkAllRead());
            toastTray.MarkAllRead();
            updateCounts();
        }

        private void updateCounts()
        {
            unreadCount.Value = sections.Select(c => c.UnreadCount).Sum() + toastTray.UnreadCount;
        }
    }
}
