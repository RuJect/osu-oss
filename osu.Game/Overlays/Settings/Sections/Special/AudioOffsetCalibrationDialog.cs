// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Dialog;
using osuTK;

namespace osu.Game.Overlays.Settings.Sections.Special
{
    public partial class AudioOffsetCalibrationDialog : PopupDialog
    {
        private const double beat_length = 500;
        private const int beat_count = 8;
        private const int minimum_taps = 3;
        private const double offset_limit = 500;

        private readonly Bindable<double> offset;
        private readonly RoundedButton tapButton;
        private readonly OsuTextFlowContainer statusText;
        private readonly PopupDialogOkButton applyButton;
        private readonly Stopwatch stopwatch = new Stopwatch();
        private readonly List<double> tapErrors = new List<double>();

        private Sample? tickSample;

        private double startTime;
        private double baseOffset;
        private double? suggestedOffset;
        private int nextBeat;
        private bool calibrating;

        public AudioOffsetCalibrationDialog(Bindable<double> offset)
        {
            this.offset = offset;

            Icon = FontAwesome.Solid.Clock;

            HeaderText = "Audio offset calibration";
            BodyText = "Tap the button on each metronome beat. Tap at least three times to receive a suggested offset. "
                       + "Applying the suggestion will replace your current global audio offset.";

            MainContent.Child = new FillFlowContainer
            {
                Margin = new MarginPadding { Top = 20 },
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Width = 400,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(10),
                Children = new Drawable[]
                {
                    tapButton = new CalibrationButton
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 100,
                        Text = "Start",
                        Action = onTapButtonPressed,
                    },
                    statusText = new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 16))
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        TextAnchor = Anchor.TopCentre,
                        Text = "Press start, then tap along to the beat.",
                    },
                }
            };

            applyButton = new PopupDialogOkButton
            {
                Text = "Apply suggested offset",
                Alpha = 0,
                Enabled = { Value = false },
                Action = applySuggestion,
            };

            Buttons = new PopupDialogButton[]
            {
                applyButton,
                new PopupDialogCancelButton
                {
                    Text = "Cancel",
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load(AudioManager audio)
        {
            tickSample = audio.Samples.Get(@"UI/metronome-tick");
        }

        protected override void Update()
        {
            base.Update();

            if (!calibrating)
                return;

            double now = stopwatch.Elapsed.TotalMilliseconds;

            while (calibrating && now >= startTime + (nextBeat + 1) * beat_length)
            {
                if (nextBeat >= beat_count)
                {
                    finishCalibration();
                    break;
                }

                tickSample?.Play();
                nextBeat++;
            }
        }

        protected override void PopOut()
        {
            calibrating = false;
            base.PopOut();
        }

        private void onTapButtonPressed()
        {
            if (calibrating)
                recordTap();
            else
                startCalibration();
        }

        private void startCalibration()
        {
            tapErrors.Clear();
            suggestedOffset = null;
            baseOffset = offset.Value;
            nextBeat = 0;
            calibrating = true;

            applyButton.Enabled.Value = false;
            applyButton.FadeOut(100);

            tapButton.Text = "Tap!";
            statusText.Text = "Tap on each beat. 0 taps.";

            stopwatch.Restart();
            startTime = 0;
        }

        private void recordTap()
        {
            double now = stopwatch.Elapsed.TotalMilliseconds;

            if (now < startTime + beat_length / 2)
                return;

            double beatIndex = Math.Round((now - startTime) / beat_length);
            double beatTime = startTime + beatIndex * beat_length;
            tapErrors.Add(now - beatTime);

            if (tapErrors.Count >= minimum_taps)
            {
                updateSuggestion();
                applyButton.Enabled.Value = true;
                applyButton.FadeIn(200);
            }
            else
                statusText.Text = $"Tap on each beat. {tapErrors.Count} taps.";
        }

        private void finishCalibration()
        {
            calibrating = false;
            tapButton.Text = "Start again";

            if (tapErrors.Count >= minimum_taps)
                updateSuggestion();
            else
                statusText.Text = $"Only {tapErrors.Count} taps. Tap at least {minimum_taps} times and try again.";
        }

        private void updateSuggestion()
        {
            suggestedOffset = CalculateSuggestedOffset(baseOffset, tapErrors, -offset_limit, offset_limit);
            statusText.Text = $"Tap on each beat. {tapErrors.Count} taps. Suggested offset: {suggestedOffset:0} ms.";
        }

        private void applySuggestion()
        {
            if (suggestedOffset.HasValue)
                offset.Value = suggestedOffset.Value;
        }

        internal static double CalculateSuggestedOffset(double currentOffset, IReadOnlyList<double> tapErrors, double minValue, double maxValue)
        {
            if (tapErrors.Count == 0)
                return currentOffset;

            double[] ordered = tapErrors.OrderBy(e => e).ToArray();
            int centre = ordered.Length / 2;

            double median = ordered.Length % 2 == 0
                ? (ordered[centre - 1] + ordered[centre]) / 2
                : ordered[centre];

            return Math.Clamp(Math.Round(currentOffset - median), minValue, maxValue);
        }

        private partial class CalibrationButton : RoundedButton
        {
            protected override void LoadComplete()
            {
                base.LoadComplete();

                foreach (var sounds in InternalChildren.OfType<HoverClickSounds>().ToArray())
                    RemoveInternal(sounds, true);
            }
        }
    }
}
