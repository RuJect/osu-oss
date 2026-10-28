// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Framework.Utils;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.Multiplayer;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Updater;
using osuTK;
using osuTK.Input;

namespace osu.Game.Tests.Visual.UserInterface
{
    [TestFixture]
    public partial class TestSceneNotificationOverlay : OsuManualInputManagerTestScene
    {
        private NotificationOverlay notificationOverlay = null!;

        private readonly List<ProgressNotification> progressingNotifications = new List<ProgressNotification>();

        private SpriteText displayedCount = null!;

        public double TimeToCompleteProgress { get; set; } = 2000;

        private readonly UserLookupCache userLookupCache = new TestUserLookupCache();

        [Cached]
        private readonly SpecialConfigManager specialConfig = new SpecialConfigManager(new TemporaryNativeStorage("osu-oss-notifications-test"));

        private Bindable<NotificationPanelPosition> panelPosition => specialConfig.GetBindable<NotificationPanelPosition>(SpecialSetting.NotificationPanelPosition);

        private Bindable<NotificationDisplayPosition> displayPosition => specialConfig.GetBindable<NotificationDisplayPosition>(SpecialSetting.NotificationDisplayPosition);

        private Bindable<bool> transparency => specialConfig.GetBindable<bool>(SpecialSetting.NotificationTransparency);

        private Bindable<bool> muteToastNotifications => specialConfig.GetBindable<bool>(SpecialSetting.MuteToastNotifications);

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            InputManager.MoveMouseTo(Vector2.Zero);

            TimeToCompleteProgress = 2000;
            progressingNotifications.Clear();

            panelPosition.Value = NotificationPanelPosition.Right;
            displayPosition.Value = NotificationDisplayPosition.TopRight;
            transparency.Value = false;
            muteToastNotifications.Value = false;

            Children = new Drawable[]
            {
                notificationOverlay = new NotificationOverlay
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight
                },
                displayedCount = new OsuSpriteText()
            };

            notificationOverlay.UnreadCount.ValueChanged += count => { displayedCount.Text = $"unread count: {count.NewValue}"; };
        });

        [Test]
        public void TestBasicFlow()
        {
            setState(Visibility.Visible);
            AddStep(@"simple #1", sendHelloNotification);
            AddStep(@"simple #2", sendAmazingNotification);
            AddStep(@"progress #1", sendUploadProgress);
            AddStep(@"progress #2", sendDownloadProgress);
            AddStep("outage", () => notificationOverlay.Post(new OutageNotification("Things are on fire. Investigating...")));
            AddStep("failed submission", () => notificationOverlay.Post(new ScoreSubmissionFailureNotification("Score will not be submitted", "This beatmap does not match the online version. Please update or redownload it.")));

            checkProgressingCount(2);

            setState(Visibility.Hidden);

            AddRepeatStep(@"add many simple", sendManyNotifications, 3);

            waitForCompletion();

            AddStep(@"progress #3", sendUploadProgress);

            checkProgressingCount(1);

            checkDisplayedCount(33);

            waitForCompletion();
        }

        [Test]
        public void TestNormalDoesForwardToOverlay()
        {
            SimpleNotification notification = null!;

            AddStep(@"simple #1", () => notificationOverlay.Post(notification = new SimpleNotification
            {
                Text = @"This shouldn't annoy you too much",
                Transient = false,
            }));

            AddAssert("notification in toast tray", () => notification.IsInToastTray, () => Is.True);
            AddUntilStep("wait for dismissed", () => notification.IsInToastTray, () => Is.False);

            checkDisplayedCount(1);
        }

        [Test]
        public void TestTransientDoesNotForwardToOverlay()
        {
            SimpleNotification notification = null!;

            AddStep(@"simple #1", () => notificationOverlay.Post(notification = new SimpleNotification
            {
                Text = @"This shouldn't annoy you too much",
                Transient = true,
            }));

            AddAssert("notification in toast tray", () => notification.IsInToastTray, () => Is.True);
            AddUntilStep("wait for dismissed", () => notification.IsInToastTray, () => Is.False);

            checkDisplayedCount(0);
        }

        [Test]
        public void TestForwardWithFlingRight()
        {
            bool activated = false;
            SimpleNotification notification = null!;

            AddStep("post", () =>
            {
                activated = false;
                notificationOverlay.Post(notification = new SimpleNotification
                {
                    Text = @"Welcome to osu!. Enjoy your stay!",
                    Activated = () => activated = true,
                });
            });

            AddStep("start drag", () =>
            {
                InputManager.MoveMouseTo(notification.ChildrenOfType<Notification>().Single());
                InputManager.PressButton(MouseButton.Left);
                InputManager.MoveMouseTo(notification.ChildrenOfType<Notification>().Single().ScreenSpaceDrawQuad.Centre + new Vector2(500, 0));
            });

            AddStep("fling away", () =>
            {
                InputManager.ReleaseButton(MouseButton.Left);
            });

            AddAssert("was not closed", () => !notification.WasClosed);
            AddAssert("was not activated", () => !activated);
            AddAssert("is not read", () => !notification.Read);
            AddAssert("is not toast", () => !notification.IsInToastTray);

            AddStep("reset mouse position", () => InputManager.MoveMouseTo(Vector2.Zero));
            AddAssert("unread count one", () => notificationOverlay.UnreadCount.Value == 1);
        }

        [Test]
        public void TestDismissWithoutActivationFling()
        {
            bool activated = false;
            SimpleNotification notification = null!;

            AddStep("post", () =>
            {
                activated = false;
                notificationOverlay.Post(notification = new SimpleNotification
                {
                    Text = @"Welcome to osu!. Enjoy your stay!",
                    Activated = () => activated = true,
                });
            });

            AddStep("start drag", () =>
            {
                InputManager.MoveMouseTo(notificationOverlay.ChildrenOfType<Notification>().Single());
                InputManager.PressButton(MouseButton.Left);
                InputManager.MoveMouseTo(notificationOverlay.ChildrenOfType<Notification>().Single().ScreenSpaceDrawQuad.Centre + new Vector2(-500, 0));
            });

            AddStep("fling away", () =>
            {
                InputManager.ReleaseButton(MouseButton.Left);
            });

            AddUntilStep("wait for closed", () => notification.WasClosed);
            AddAssert("was not activated", () => !activated);
            AddStep("reset mouse position", () => InputManager.MoveMouseTo(Vector2.Zero));
            AddAssert("unread count zero", () => notificationOverlay.UnreadCount.Value == 0);
        }

        [Test]
        public void TestProgressNotificationCantBeFlung()
        {
            bool activated = false;
            ProgressNotification notification = null!;

            AddStep("post", () =>
            {
                activated = false;
                notificationOverlay.Post(notification = new ProgressNotification
                {
                    Text = @"Uploading to BSS...",
                    CompletionText = "Uploaded to BSS!",
                    Activated = () => activated = true,
                });

                progressingNotifications.Add(notification);
            });

            AddStep("start drag", () =>
            {
                InputManager.MoveMouseTo(notificationOverlay.ChildrenOfType<Notification>().Single());
                InputManager.PressButton(MouseButton.Left);
                InputManager.MoveMouseTo(notificationOverlay.ChildrenOfType<Notification>().Single().ScreenSpaceDrawQuad.Centre + new Vector2(-500, 0));
            });

            AddStep("attempt fling", () =>
            {
                InputManager.ReleaseButton(MouseButton.Left);
            });

            AddUntilStep("was not closed", () => !notification.WasClosed);
            AddUntilStep("was not cancelled", () => notification.State == ProgressNotificationState.Active);
            AddAssert("was not activated", () => !activated);
            AddStep("reset mouse position", () => InputManager.MoveMouseTo(Vector2.Zero));

            AddUntilStep("was completed", () => notification.State == ProgressNotificationState.Completed);
        }

        [Test]
        public void TestDismissWithoutActivationCloseButton()
        {
            bool activated = false;
            SimpleNotification notification = null!;

            AddStep("post", () =>
            {
                activated = false;
                notificationOverlay.Post(notification = new SimpleNotification
                {
                    Text = @"Welcome to osu!. Enjoy your stay!",
                    Activated = () => activated = true,
                });
            });

            AddStep("click to activate", () =>
            {
                InputManager.MoveMouseTo(notificationOverlay
                                         .ChildrenOfType<Notification>().Single()
                                         .ChildrenOfType<Notification.CloseButton>().Single());
                InputManager.Click(MouseButton.Left);
            });

            AddUntilStep("wait for closed", () => notification.WasClosed);
            AddAssert("was not activated", () => !activated);
            AddStep("reset mouse position", () => InputManager.MoveMouseTo(Vector2.Zero));
            AddAssert("unread count zero", () => notificationOverlay.UnreadCount.Value == 0);
        }

        [Test]
        public void TestDismissWithoutActivationRightClick()
        {
            bool activated = false;
            SimpleNotification notification = null!;

            AddStep("post", () =>
            {
                activated = false;
                notificationOverlay.Post(notification = new SimpleNotification
                {
                    Text = @"Welcome to osu!. Enjoy your stay!",
                    Activated = () => activated = true,
                });
            });

            AddStep("click to activate", () =>
            {
                InputManager.MoveMouseTo(notificationOverlay.ChildrenOfType<Notification>().Single());
                InputManager.Click(MouseButton.Right);
            });

            AddUntilStep("wait for closed", () => notification.WasClosed);
            AddAssert("was not activated", () => !activated);
            AddStep("reset mouse position", () => InputManager.MoveMouseTo(Vector2.Zero));
        }

        [Test]
        public void TestActivate()
        {
            bool activated = false;
            SimpleNotification notification = null!;

            AddStep("post", () =>
            {
                activated = false;
                notificationOverlay.Post(notification = new SimpleNotification
                {
                    Text = @"Welcome to osu!. Enjoy your stay!",
                    Activated = () => activated = true,
                });
            });

            AddStep("click to activate", () =>
            {
                InputManager.MoveMouseTo(notificationOverlay.ChildrenOfType<Notification>().Single());
                InputManager.Click(MouseButton.Left);
            });

            AddUntilStep("wait for closed", () => notification.WasClosed);
            AddAssert("was activated", () => activated);
            AddStep("reset mouse position", () => InputManager.MoveMouseTo(Vector2.Zero));
        }

        [Test]
        public void TestPresence()
        {
            AddAssert("tray not present", () => !notificationOverlay.ChildrenOfType<NotificationOverlayToastTray>().Single().IsPresent);
            AddAssert("overlay not present", () => !notificationOverlay.IsPresent);

            AddStep(@"post notification", sendBackgroundNotification);

            AddUntilStep("wait tray not present", () => !notificationOverlay.ChildrenOfType<NotificationOverlayToastTray>().Single().IsPresent);
            AddUntilStep("wait overlay not present", () => !notificationOverlay.IsPresent);
        }

        [Test]
        public void TestPresenceWithManualDismiss()
        {
            AddAssert("tray not present", () => !notificationOverlay.ChildrenOfType<NotificationOverlayToastTray>().Single().IsPresent);
            AddAssert("overlay not present", () => !notificationOverlay.IsPresent);

            AddStep(@"post notification", sendBackgroundNotification);
            AddStep("click notification", () => notificationOverlay.ChildrenOfType<Notification>().Single().TriggerClick());

            AddUntilStep("wait tray not present", () => !notificationOverlay.ChildrenOfType<NotificationOverlayToastTray>().Single().IsPresent);
            AddUntilStep("wait overlay not present", () => !notificationOverlay.IsPresent);
        }

        [Test]
        public void TestProgressSilentDismissal()
        {
            ProgressNotification notification = null!;

            AddStep("add progress notification", () =>
            {
                notification = new ProgressNotification
                {
                    Text = @"Uploading to BSS...",
                    CompletionText = "Uploaded to BSS!",
                };
                notificationOverlay.Post(notification);
                progressingNotifications.Add(notification);
            });

            AddStep("silently dismiss", () => notification.CompleteSilently());
            AddAssert("completed", () => notification.State == ProgressNotificationState.Completed);
            AddAssert("Completion toast not shown", () => notificationOverlay.ToastCount == 0);
        }

        [Test]
        public void TestProgressSilentDismissalImmediate()
        {
            ProgressNotification notification = null!;

            AddStep("add progress notification", () =>
            {
                notification = new ProgressNotification
                {
                    Text = @"Uploading to BSS...",
                    CompletionText = "Uploaded to BSS!",
                };

                notification.CompleteSilently();

                notificationOverlay.Post(notification);
                progressingNotifications.Add(notification);
            });

            AddAssert("completed", () => notification.State == ProgressNotificationState.Completed);
            AddAssert("Completion toast not shown", () => notificationOverlay.ToastCount == 0);
        }

        [Test]
        public void TestProgressClick()
        {
            ProgressNotification notification = null!;

            AddStep("add progress notification", () =>
            {
                notification = new ProgressNotification
                {
                    Text = @"Uploading to BSS...",
                    CompletionText = "Uploaded to BSS!",
                };
                notificationOverlay.Post(notification);
                progressingNotifications.Add(notification);
            });

            AddStep("hover over notification", () => InputManager.MoveMouseTo(notificationOverlay.ChildrenOfType<ProgressNotification>().Single()));

            AddStep("left click", () => InputManager.Click(MouseButton.Left));
            AddAssert("not cancelled", () => notification.State == ProgressNotificationState.Active);

            AddStep("right click", () => InputManager.Click(MouseButton.Right));
            AddAssert("cancelled", () => notification.State == ProgressNotificationState.Cancelled);
        }

        [Test]
        public void TestCompleteProgress()
        {
            ProgressNotification notification = null!;

            AddStep("add progress notification", () =>
            {
                notification = new ProgressNotification
                {
                    Text = @"Uploading to BSS...",
                    CompletionText = "Uploaded to BSS!",
                };
                notificationOverlay.Post(notification);
                progressingNotifications.Add(notification);
            });

            AddUntilStep("wait completion", () => notification.State == ProgressNotificationState.Completed);

            AddAssert("Completion toast shown", () => notificationOverlay.ToastCount == 1);
            AddUntilStep("wait forwarded", () => notificationOverlay.ToastCount == 0);
        }

        [Test]
        public void TestCompleteProgressSlow()
        {
            ProgressNotification notification = null!;

            AddStep("Set progress slow", () => TimeToCompleteProgress *= 2);
            AddStep("add progress notification", () =>
            {
                notification = new ProgressNotification
                {
                    Text = @"Uploading to BSS...",
                    CompletionText = "Uploaded to BSS!",
                };
                notificationOverlay.Post(notification);
                progressingNotifications.Add(notification);
            });

            AddUntilStep("wait completion", () => notification.State == ProgressNotificationState.Completed);

            AddAssert("Completion toast shown", () => notificationOverlay.ToastCount == 1);
            AddUntilStep("wait forwarded", () => notificationOverlay.ToastCount == 0);
            AddAssert("only one unread", () => notificationOverlay.UnreadCount.Value == 1);
        }

        [Test]
        public void TestCancelProgress()
        {
            ProgressNotification notification = null!;
            AddStep("add progress notification", () =>
            {
                notification = new ProgressNotification
                {
                    Text = @"Uploading to BSS...",
                    CompletionText = "Uploaded to BSS!",
                };
                notificationOverlay.Post(notification);
                progressingNotifications.Add(notification);
            });

            AddWaitStep("wait 3", 3);

            AddStep("cancel notification", () => notification.State = ProgressNotificationState.Cancelled);
        }

        [Test]
        public void TestReadState()
        {
            SimpleNotification notification = null!;
            AddStep(@"post", () => notificationOverlay.Post(notification = new BackgroundNotification { Text = @"Welcome to osu!. Enjoy your stay!" }));
            AddUntilStep("check is toast", () => notification.IsInToastTray);
            AddAssert("light is not visible", () => notification.ChildrenOfType<Notification.NotificationLight>().Single().Alpha == 0);

            AddUntilStep("wait for forward to overlay", () => !notification.IsInToastTray);

            setState(Visibility.Visible);
            AddAssert("state is not read", () => !notification.Read);
            AddUntilStep("light is visible", () => notification.ChildrenOfType<Notification.NotificationLight>().Single().Alpha == 1);

            setState(Visibility.Hidden);
            setState(Visibility.Visible);
            AddAssert("state is read", () => notification.Read);
            AddUntilStep("light is not visible", () => notification.ChildrenOfType<Notification.NotificationLight>().Single().Alpha == 0);
        }

        [Test]
        public void TestUpdateNotificationFlow()
        {
            bool applyUpdate = false;

            AddStep(@"post update", () =>
            {
                applyUpdate = false;

                var updateNotification = new UpdateManager.UpdateDownloadProgressNotification(CancellationToken.None)
                {
                    CompletionClickAction = () => applyUpdate = true
                };

                notificationOverlay.Post(updateNotification);
                progressingNotifications.Add(updateNotification);
            });

            checkProgressingCount(1);
            waitForCompletion();

            UpdateManager.UpdateReadyNotification? completionNotification = null;
            AddUntilStep("wait for completion notification",
                () => (completionNotification = notificationOverlay.ChildrenOfType<UpdateManager.UpdateReadyNotification>().SingleOrDefault()) != null);
            AddStep("click notification", () => completionNotification?.TriggerClick());

            AddUntilStep("wait for update applied", () => applyUpdate);
        }

        [Test]
        public void TestImportantWhileClosed()
        {
            AddStep(@"simple #1", sendHelloNotification);

            AddAssert("toast displayed", () => notificationOverlay.ToastCount == 1);
            AddAssert("is not visible", () => notificationOverlay.State.Value == Visibility.Hidden);

            checkDisplayedCount(1);

            AddStep(@"progress #1", sendUploadProgress);
            AddStep(@"progress #2", sendDownloadProgress);

            checkProgressingCount(2);
            checkDisplayedCount(3);
        }

        [Test]
        public void TestUnimportantWhileClosed()
        {
            AddStep(@"background #1", sendBackgroundNotification);

            AddAssert("Is not visible", () => notificationOverlay.State.Value == Visibility.Hidden);

            checkDisplayedCount(1);

            AddStep(@"background progress #1", sendBackgroundUploadProgress);

            checkProgressingCount(1);

            waitForCompletion();

            checkDisplayedCount(2);

            AddStep(@"simple #1", sendHelloNotification);

            checkDisplayedCount(3);
        }

        [Test]
        public void TestError()
        {
            setState(Visibility.Visible);
            AddStep(@"error #1", sendErrorNotification);
            AddAssert("Is visible", () => notificationOverlay.State.Value == Visibility.Visible);
            checkDisplayedCount(1);
        }

        [Test]
        public void TestSpam()
        {
            setState(Visibility.Visible);
            AddRepeatStep("send barrage", sendBarrage, 10);
        }

        [Test]
        public void TestServerShuttingDownNotification()
        {
            AddStep("post with 5 seconds", () => notificationOverlay.Post(new ServerShutdownNotification(TimeSpan.FromSeconds(5))));
            AddStep("post with 30 seconds", () => notificationOverlay.Post(new ServerShutdownNotification(TimeSpan.FromSeconds(30))));
            AddStep("post with 6 hours", () => notificationOverlay.Post(new ServerShutdownNotification(TimeSpan.FromHours(6))));
        }

        protected override void Update()
        {
            base.Update();

            progressingNotifications.RemoveAll(n => n.State == ProgressNotificationState.Completed && n.WasClosed);

            if (progressingNotifications.Count(n => n.State == ProgressNotificationState.Active) < 3)
            {
                var p = progressingNotifications.Find(n => n.State == ProgressNotificationState.Queued);

                p?.State = ProgressNotificationState.Active;
            }

            foreach (var n in progressingNotifications.FindAll(n => n.State == ProgressNotificationState.Active))
            {
                if (n.Progress < 1)
                    n.Progress += (float)(Time.Elapsed / TimeToCompleteProgress);
                else
                    n.State = ProgressNotificationState.Completed;
            }
        }

        private void checkDisplayedCount(int expected) =>
            AddUntilStep($"Displayed count is {expected}", () => notificationOverlay.UnreadCount.Value == expected);

        private void sendDownloadProgress()
        {
            var n = new ProgressNotification
            {
                Text = @"Downloading Haitai...",
                CompletionText = "Downloaded Haitai!",
            };
            notificationOverlay.Post(n);
            progressingNotifications.Add(n);
        }

        private void sendUploadProgress()
        {
            var n = new ProgressNotification
            {
                Text = @"Uploading to BSS...",
                CompletionText = "Uploaded to BSS!",
            };
            notificationOverlay.Post(n);
            progressingNotifications.Add(n);
        }

        private void sendBackgroundUploadProgress()
        {
            var n = new BackgroundProgressNotification
            {
                Text = @"Uploading to BSS...",
                CompletionText = "Uploaded to BSS!",
            };
            notificationOverlay.Post(n);
            progressingNotifications.Add(n);
        }

        [Test]
        public void TestPanelPosition()
        {
            setState(Visibility.Visible);
            AddUntilStep("panel is on the right", () => isPanelAligned(NotificationPanelPosition.Right));

            AddStep("move panel to left", () => panelPosition.Value = NotificationPanelPosition.Left);
            AddUntilStep("panel is on the left", () => isPanelAligned(NotificationPanelPosition.Left));

            AddStep("move panel to right", () => panelPosition.Value = NotificationPanelPosition.Right);
            AddUntilStep("panel is on the right", () => isPanelAligned(NotificationPanelPosition.Right));

            bool isPanelAligned(NotificationPanelPosition position)
            {
                var parentQuad = notificationOverlay.Parent!.ScreenSpaceDrawQuad;
                var quad = notificationOverlay.ScreenSpaceDrawQuad;

                return position == NotificationPanelPosition.Left
                    ? Precision.AlmostEquals(quad.TopLeft.X, parentQuad.TopLeft.X)
                    : Precision.AlmostEquals(quad.TopRight.X, parentQuad.TopRight.X);
            }
        }

        [TestCase(NotificationDisplayPosition.TopLeft)]
        [TestCase(NotificationDisplayPosition.TopRight)]
        [TestCase(NotificationDisplayPosition.BottomLeft)]
        [TestCase(NotificationDisplayPosition.BottomRight)]
        public void TestToastDisplayPosition(NotificationDisplayPosition position)
        {
            SimpleNotification notification = null!;

            AddStep($"set display position to {position}", () => displayPosition.Value = position);
            AddStep("post transient notification", () => notificationOverlay.Post(notification = new SimpleNotification
            {
                Text = @"Test toast",
                Transient = true,
                IsImportant = true,
            }));

            AddUntilStep("toast is in expected corner", () =>
            {
                if (notification == null || !notification.IsLoaded || !notification.IsPresent)
                    return false;

                var parentQuad = notificationOverlay.Parent!.ScreenSpaceDrawQuad;
                var quad = notification.ScreenSpaceDrawQuad;

                float scale = parentQuad.Width / notificationOverlay.Parent.DrawWidth;
                float margin = 20 * scale;

                bool left = position is NotificationDisplayPosition.TopLeft or NotificationDisplayPosition.BottomLeft;
                bool top = position is NotificationDisplayPosition.TopLeft or NotificationDisplayPosition.TopRight;

                return (left
                           ? Precision.AlmostEquals(quad.TopLeft.X, parentQuad.TopLeft.X + margin, 2 * scale)
                           : Precision.AlmostEquals(quad.TopRight.X, parentQuad.TopRight.X - margin, 2 * scale))
                       && (top
                           ? Precision.AlmostEquals(quad.TopLeft.Y, parentQuad.TopLeft.Y + margin, 2 * scale)
                           : Precision.AlmostEquals(quad.BottomLeft.Y, parentQuad.BottomLeft.Y - margin, 2 * scale));
            });
        }

        [Test]
        public void TestNotificationTransparencySetting()
        {
            SimpleNotification notification = null!;

            AddStep("enable transparency", () => transparency.Value = true);
            AddStep("show overlay", () => notificationOverlay.Show());
            AddUntilStep("overlay visible", () => notificationOverlay.State.Value == Visibility.Visible);

            AddStep("post notification", () => notificationOverlay.Post(notification = new SimpleNotification { Text = @"Fade test" }));
            AddUntilStep("notification visible", () => notification.IsLoaded && notification.IsPresent);

            AddStep("close notification", () => notification.Close(false));
            AddUntilStep("notification removed", () => notification.Parent == null);
            AddAssert("notification fully faded out", () => notification.Alpha <= 0.0001f);
        }

        [Test]
        public void TestToastForwardingWithTransparency()
        {
            SimpleNotification notification = null!;

            AddStep("enable transparency", () => transparency.Value = true);
            AddStep("post transient notification", () => notificationOverlay.Post(notification = new SimpleNotification
            {
                Text = @"Test toast",
                Transient = true,
                IsImportant = false,
            }));

            AddUntilStep("notification in toast tray", () => notification.IsInToastTray);
            AddUntilStep("notification fading", () => notification.IsInToastTray && notification.Alpha < 1);
            AddUntilStep("notification forwarded", () => !notification.IsInToastTray);
            AddUntilStep("notification removed", () => notification.Parent == null);
            AddAssert("stock fade completed", () => notification.Alpha <= 0.0001f);
        }

        [Test]
        public void TestToastForwardingWithoutTransparency()
        {
            SimpleNotification notification = null!;

            AddStep("disable transparency", () => transparency.Value = false);
            AddStep("post transient notification", () => notificationOverlay.Post(notification = new SimpleNotification
            {
                Text = @"Test toast",
                Transient = true,
                IsImportant = false,
            }));

            AddUntilStep("notification in toast tray", () => notification.IsInToastTray);
            AddUntilStep("notification forwarded", () => !notification.IsInToastTray);
            AddUntilStep("notification removed", () => notification.Parent == null);
            AddAssert("stock fade completed", () => notification.Alpha <= 0.0001f);
        }

        [Test]
        public void TestMuteToastNotifications()
        {
            SimpleNotification notification = null!;

            AddStep("mute toast notifications", () => muteToastNotifications.Value = true);
            AddStep("post transient notification", () => notificationOverlay.Post(notification = new SimpleNotification
            {
                Text = @"Test toast",
                Transient = true,
                IsImportant = false,
            }));

            AddUntilStep("notification added to panel", () => notificationOverlay.AllNotifications.Contains(notification));
            AddAssert("notification not a toast", () => !notification.IsInToastTray);
            AddAssert("no toasts displayed", () => notificationOverlay.ToastCount == 0);
        }

        [Test]
        public void TestMutingFlushesExistingToasts()
        {
            SimpleNotification notification = null!;

            AddStep("post transient notification", () => notificationOverlay.Post(notification = new SimpleNotification
            {
                Text = @"Test toast",
                IsImportant = false,
            }));

            AddUntilStep("notification in toast tray", () => notification.IsInToastTray);
            AddStep("mute toast notifications", () => muteToastNotifications.Value = true);
            AddUntilStep("notification forwarded", () => !notification.IsInToastTray);
            AddUntilStep("notification added to panel", () => notificationOverlay.AllNotifications.Contains(notification));
        }

        private void setState(Visibility state) => AddStep(state.ToString(), () => notificationOverlay.State.Value = state);

        private void checkProgressingCount(int expected) => AddAssert($"progressing count is {expected}", () => progressingNotifications.Count == expected);

        private void waitForCompletion() => AddUntilStep("wait for notification progress completion", () => progressingNotifications.Count == 0);

        private void sendBarrage()
        {
            switch (RNG.Next(0, 5))
            {
                case 0:
                    sendHelloNotification();
                    break;

                case 1:
                    sendAmazingNotification();
                    break;

                case 2:
                    sendUploadProgress();
                    break;

                case 3:
                    sendDownloadProgress();
                    break;

                case 4:
                    sendErrorNotification();
                    break;
            }
        }

        private void sendAmazingNotification()
        {
            notificationOverlay.Post(new SimpleNotification { Text = @"You are amazing" });
        }

        private void sendHelloNotification()
        {
            notificationOverlay.Post(new SimpleNotification { Text = @"Welcome to osu!. Enjoy your stay!" });
        }

        private void sendBackgroundNotification()
        {
            notificationOverlay.Post(new BackgroundNotification { Text = @"Welcome to osu!. Enjoy your stay!" });
        }

        private void sendErrorNotification()
        {
            notificationOverlay.Post(new SimpleErrorNotification { Text = @"Rut roh!. Something went wrong!" });
        }

        private void sendManyNotifications()
        {
            for (int i = 0; i < 10; i++)
                notificationOverlay.Post(new SimpleNotification { Text = @"Spam incoming!!" });
        }

        private partial class BackgroundNotification : SimpleNotification
        {
            public BackgroundNotification()
            {
                IsImportant = false;
            }
        }

        private partial class BackgroundProgressNotification : ProgressNotification
        {
            public BackgroundProgressNotification()
            {
                IsImportant = false;
            }
        }
    }
}
