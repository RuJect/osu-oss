// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Utils;
using osu.Game.Configuration;
using osu.Game.Overlays.Notifications;
using osuTK;

namespace osu.Game.Overlays
{
    /// <summary>
    /// A tray which attaches to the left of <see cref="NotificationOverlay"/> to show temporary toasts.
    /// </summary>
    public partial class NotificationOverlayToastTray : CompositeDrawable
    {
        public override bool IsPresent => toastContentBackground.Height > 0 || toastFlow.Count > 0;

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => toastFlow.ReceivePositionalInputAt(screenSpacePos);

        /// <summary>
        /// All notifications currently being displayed by the toast tray.
        /// </summary>
        public IEnumerable<Notification> Notifications => toastFlow.Concat(InternalChildren.OfType<Notification>());

        public bool IsDisplayingToasts => toastFlow.Count > 0;

        /// <summary>
        /// The height of the currently displayed toast stack, excluding padding.
        /// </summary>
        public float ContentHeight => toastFlow.DrawHeight;

        private FillFlowContainer<Notification> toastFlow = null!;
        private BufferedContainer toastContentBackground = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private SpecialConfigManager? specialConfig { get; set; }

        public required Action<Notification> ForwardNotificationToPermanentStore { get; init; }

        private NotificationPanelPosition panelPosition = NotificationPanelPosition.Right;
        private NotificationDisplayPosition displayPosition = NotificationDisplayPosition.TopRight;

        private Drawable? blurContainer;

        public NotificationPanelPosition PanelPosition
        {
            get => panelPosition;
            set => panelPosition = value;
        }

        public NotificationDisplayPosition DisplayPosition
        {
            get => displayPosition;
            set
            {
                if (displayPosition == value)
                    return;

                displayPosition = value;
                applyDisplayPosition();
            }
        }

        private bool transparency => specialConfig?.Get<bool>(SpecialSetting.NotificationTransparency) ?? false;

        public int UnreadCount => Notifications.Count(n => !n.WasClosed && !n.Read);

        private int runningDepth;

        private readonly HashSet<Notification> forwardingNotifications = new HashSet<Notification>();

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Padding = new MarginPadding(20);
            Anchor = Anchor.TopLeft;
            Origin = Anchor.TopLeft;

            InternalChildren = new Drawable[]
            {
                toastContentBackground = (new Box
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Colour = ColourInfo.GradientVertical(
                        colourProvider.Background6.Opacity(0.7f),
                        colourProvider.Background6.Opacity(0.5f)),
                    RelativeSizeAxes = Axes.Both,
                    Height = 0,
                }.WithEffect(new BlurEffect
                {
                    PadExtent = true,
                    Sigma = new Vector2(20),
                }).With(postEffectDrawable =>
                {
                    blurContainer = postEffectDrawable;
                    postEffectDrawable.Scale = new Vector2(1.5f, 1);
                    postEffectDrawable.AutoSizeAxes = Axes.None;
                    postEffectDrawable.RelativeSizeAxes = Axes.X;
                })),
                toastFlow = new FillFlowContainer<Notification>
                {
                    LayoutDuration = 150,
                    LayoutEasing = Easing.OutQuart,
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                },
            };

            applyDisplayPosition();
        }

        private void applyDisplayPosition()
        {
            if (toastContentBackground == null)
                return;

            bool leftAligned = displayPosition is NotificationDisplayPosition.TopLeft or NotificationDisplayPosition.BottomLeft;

            toastContentBackground.Anchor = leftAligned ? Anchor.TopLeft : Anchor.TopRight;
            toastContentBackground.Origin = leftAligned ? Anchor.TopLeft : Anchor.TopRight;

            if (blurContainer != null)
                blurContainer.Position = new Vector2(leftAligned ? -70 : 70, -50);
        }

        public void MarkAllRead() => Notifications.ForEach(n => n.Read = true);

        public void FlushAllToasts(bool force = false)
        {
            foreach (var notification in toastFlow.ToArray())
            {
                if (!force && notification.IsCritical)
                    continue;

                forwardNotification(notification);
            }
        }

        public void Post(Notification notification)
        {
            ++runningDepth;

            notification.ForwardToOverlay = () => forwardNotification(notification);
            notification.DisplayPosition = displayPosition;

            int depth = notification.DisplayOnTop ? -runningDepth : runningDepth;

            toastFlow.Insert(depth, notification);

            scheduleDismissal();

            void scheduleDismissal() => Scheduler.AddDelayed(() =>
            {
                // Notification dismissed by user.
                if (notification.WasClosed)
                    return;

                // Notification forwarded away.
                if (notification.Parent != toastFlow)
                    return;

                // Notification hovered; delay dismissal.
                if (notification.IsHovered || notification.IsDragged)
                {
                    scheduleDismissal();
                    return;
                }

                // All looks good, forward away!
                forwardNotification(notification);
            }, notification.IsImportant ? 12000 : 2500);
        }

        private void forwardNotification(Notification notification)
        {
            if (!notification.IsInToastTray || !forwardingNotifications.Add(notification))
                return;

            Debug.Assert(notification.Parent == toastFlow);

            bool leftAligned = displayPosition is NotificationDisplayPosition.TopLeft or NotificationDisplayPosition.BottomLeft;
            float direction = leftAligned ? -1 : 1;

            if (transparency)
            {
                // fade away before swiping, so the swipe animation remains the standard one but only starts once the notification is gone.
                notification.FadeOut(NotificationOverlay.TRANSITION_LENGTH, Easing.OutQuint).OnComplete(_ => detachAndSwipe(true));
            }
            else
            {
                detachAndSwipe(false);
                notification.FadeOut(NotificationOverlay.TRANSITION_LENGTH, Easing.OutQuint).OnComplete(_ => completeForward());
            }

            void detachAndSwipe(bool completeAfterMove)
            {
                // temporarily remove from flow so we can animate the position towards the screen edge.
                toastFlow.Remove(notification, false);
                AddInternal(notification);

                var move = notification.MoveToOffset(new Vector2(direction * 400, 0), NotificationOverlay.TRANSITION_LENGTH, Easing.OutQuint);

                if (completeAfterMove)
                    move.OnComplete(_ => completeForward());
            }

            void completeForward()
            {
                if (!forwardingNotifications.Remove(notification))
                    return;

                if (notification.Transient)
                {
                    notification.IsInToastTray = false;
                    notification.Close(false);
                    return;
                }

                RemoveInternal(notification, false);
                ForwardNotificationToPermanentStore(notification);

                notification.FadeIn(300, Easing.OutQuint);
            }
        }

        protected override void Update()
        {
            base.Update();

            float height = 0;
            float alpha = 0;

            if (toastFlow.Count > 0)
            {
                float maxNotificationAlpha = 0;

                foreach (var t in toastFlow)
                {
                    if (t.Alpha > maxNotificationAlpha)
                        maxNotificationAlpha = t.Alpha;
                }

                height = toastFlow.DrawHeight + 120;
                alpha = Math.Clamp(toastFlow.DrawHeight / 41, 0, 1) * maxNotificationAlpha;
            }

            toastContentBackground.Height = (float)Interpolation.DampContinuously(toastContentBackground.Height, height, 10, Clock.ElapsedFrameTime);
            toastContentBackground.Alpha = (float)Interpolation.DampContinuously(toastContentBackground.Alpha, alpha, 10, Clock.ElapsedFrameTime);
        }
    }
}
