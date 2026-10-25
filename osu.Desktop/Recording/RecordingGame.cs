// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Drawing;
using osu.Framework.Configuration;
using osu.Game;
using osu.Game.Recording;

namespace osu.Desktop.Recording
{
    /// <summary>
    /// Game instance used for recording. Renders at the requested size but keeps its window hidden.
    /// </summary>
    internal partial class RecordingGame : OsuGameDesktop
    {
        private readonly RecordingOptions options;
        private readonly Action loaded;

        private double hideWindowUntil;

        public RecordingGame(RecordingOptions options, Action loaded)
            : base(Array.Empty<string>())
        {
            this.options = options;
            this.loaded = loaded;
        }

        protected override IDictionary<FrameworkSetting, object> GetFrameworkConfigDefaults()
        {
            IDictionary<FrameworkSetting, object> defaults = base.GetFrameworkConfigDefaults();

            defaults[FrameworkSetting.WindowMode] = WindowMode.Windowed;
            defaults[FrameworkSetting.WindowedSize] = new Size(options.Width, options.Height);
            defaults[FrameworkSetting.AudioDevice] = "No sound";

            return defaults;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // The framework shows the window on its first swap, which happens after this point.
            hideWindowUntil = Time.Current + 3000;

            loaded();
        }

        protected override void Update()
        {
            base.Update();

            if (Time.Current < hideWindowUntil)
                Host.Window?.Hide();
        }
    }
}
