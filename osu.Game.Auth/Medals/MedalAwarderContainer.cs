// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Auth.Medals
{
    /// <summary>
    /// Awards client-side-detectable medals by submitting unlock requests.
    /// Clean-room port of the proprietary container.
    /// </summary>
    public partial class MedalAwarderContainer : Component
    {
        private readonly List<IMedalAwarder> awarders = new List<IMedalAwarder>();

        private OsuGame? game;
        private IAPIProvider? api;

        private IBindable<APIUser>? localUser;
        private IBindable<APIState>? apiState;

        private readonly Bindable<int> failureCount = new Bindable<int>(0);

        private readonly Logger logger;

        [Resolved]
        private OsuGame Game
        {
            get => game!;
            set => game = value;
        }

        [Resolved]
        private IAPIProvider Api
        {
            get => api!;
            set => api = value;
        }

        public MedalAwarderContainer()
        {
            logger = Logger.GetLogger("medals");
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            awarders.AddRange(new IMedalAwarder[]
            {
                new CourierCatapultAwarder(),
                new CycloneAwarder(),
                new HamsterWheelAwarder(),
                new HotshotAwarder(),
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            localUser = api!.LocalUser.GetBoundCopy();
            apiState = api.State.GetBoundCopy();

            localUser.BindValueChanged(_ => refreshEnabled());
            apiState.BindValueChanged(_ => refreshEnabled(), true);
            failureCount.BindValueChanged(_ => disableOnTooManyFailures());
        }

        private void refreshEnabled()
        {
            failureCount.Value = 0;

            if (apiState?.Value == APIState.Offline || (localUser?.Value.OnlineID ?? 0) < 2)
            {
                disableAll();
                return;
            }

            var unlocked = (localUser!.Value.Achievements ?? Array.Empty<APIUserAchievement>())
                .Select(a => a.ID)
                .ToHashSet();

            foreach (var awarder in awarders)
                awarder.Enabled = !unlocked.Contains(awarder.MedalId);
        }

        private void disableOnTooManyFailures()
        {
            if (failureCount.Value >= 5)
            {
                Logger.Log("Disabling client-side medal awarding due to failure count", LoggingTarget.Network);
                disableAll();
            }
        }

        private void disableAll()
        {
            foreach (var awarder in awarders)
                awarder.Enabled = false;
        }

        protected override void Update()
        {
            base.Update();

            foreach (var awarder in awarders)
            {
                try
                {
                    if (awarder.Enabled && awarder.CheckMedalCriteria(game!))
                        unlock(awarder);
                }
                catch (Exception e)
                {
                    logger.Add($"Disabling {awarder.GetType().Name} due to unexpected error", LogLevel.Error, e);
                    awarder.Enabled = false;
                }
            }
        }

        private void unlock(IMedalAwarder awarder)
        {
            awarder.Enabled = false;

            var request = new UnlockMedalRequest(awarder.MedalId);
            request.Success += () => failureCount.Value = 0;
            request.Failure += e =>
            {
                if (e is not APIException)
                {
                    failureCount.Value++;
                    Scheduler.AddDelayed(() => awarder.Enabled = true, 5000);
                }
            };

            api!.Queue(request);
        }
    }
}
