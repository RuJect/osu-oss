// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;

namespace osu.Game.Graphics.Backdrops
{
    /// <summary>
    /// Provides synchronised views of the captured game scene, allowing glass surfaces to blur
    /// the content actually sitting behind them rather than just the wallpaper.
    /// </summary>
    public interface IBackdropProvider
    {
        /// <summary>
        /// Whether a captured scene buffer is available to sample from.
        /// </summary>
        bool Available { get; }

        /// <summary>
        /// Creates a view of the captured scene, aligned to its original on-screen position.
        /// The caller is responsible for blurring and tinting the returned view.
        /// </summary>
        BufferedContainerView<Drawable> CreateSceneView();
    }
}
