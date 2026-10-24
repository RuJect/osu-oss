// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Theming;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Graphics.Backdrops
{
    /// <summary>
    /// A drop-in replacement for a panel's background box. When the active theme requests glass
    /// and a scene backdrop is available, it draws the content behind the panel, blurred and
    /// clipped to the panel's shape, with a translucent tint on top. Otherwise it falls back to
    /// a flat fill so that non-glass themes are unaffected.
    /// </summary>
    public partial class GlassBackdrop : CompositeDrawable
    {
        private const float frame_buffer_scale = 0.25f;

        private readonly Box tint;
        private Color4 tintColour;

        [Resolved(CanBeNull = true)]
        private IBackdropProvider? backdrop { get; set; }

        public GlassBackdrop()
        {
            RelativeSizeAxes = Axes.Both;

            AddInternal(tint = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Depth = -1,
            });
        }

        /// <summary>
        /// The translucent film drawn over the blurred backdrop, or the flat fill when glass is inactive.
        /// </summary>
        public Color4 TintColour
        {
            get => tintColour;
            set
            {
                tintColour = value;
                tint.Colour = displayTint;
            }
        }

        /// <summary>
        /// Gaussian blur strength derived from the active theme.
        /// </summary>
        public float BlurSigma { get; init; } = Math.Max(1, ThemeManager.Current.GlassBlur / 3f);

        /// <summary>
        /// Resolution scale of the per-panel blur buffer.
        /// </summary>
        public float FrameBufferScale { get; init; } = frame_buffer_scale;

        /// <summary>
        /// Per-axis blur sigma for surfaces with very different width and height, such as the toolbar.
        /// </summary>
        public Vector2? BlurSigmaVector { get; init; }

        /// <summary>
        /// Per-axis buffer scale for surfaces with very different width and height, such as the toolbar.
        /// </summary>
        public Vector2? FrameBufferScaleVector { get; init; }

        private bool isBlurAvailable => ThemeManager.GlassActive && backdrop?.Available == true;

        private Color4 displayTint
        {
            get
            {
                if (isBlurAvailable)
                    return tintColour;

                // 1f is required: Opacity(1) binds to the byte overload and sets alpha to 1/255.
                return tintColour.Opacity(1f);
            }
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            tint.Colour = displayTint;

            if (backdrop == null || !isBlurAvailable)
                return;

            var sceneView = backdrop.CreateSceneView();
            sceneView.RelativeSizeAxes = Axes.Both;

            AddInternal(new BufferedContainer(cachedFrameBuffer: false)
            {
                RelativeSizeAxes = Axes.Both,
                BlurSigma = BlurSigmaVector ?? new Vector2(BlurSigma),
                FrameBufferScale = FrameBufferScaleVector ?? new Vector2(FrameBufferScale),
                RedrawOnScale = false,
                Depth = 1,
                Child = sceneView,
            });
        }
    }
}
