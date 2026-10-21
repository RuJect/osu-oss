// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;

namespace osu.Game.Graphics.Backdrops
{
    /// <summary>
    /// An <see cref="IBackdropProvider"/> backed by the <see cref="BufferedContainer"/> wrapping the scene.
    /// The buffer draws the scene sharp to the screen while retaining its texture for glass surfaces.
    /// </summary>
    public class SceneBufferBackdropProvider : IBackdropProvider
    {
        private readonly BufferedContainer sceneBuffer;

        public SceneBufferBackdropProvider(BufferedContainer sceneBuffer)
        {
            this.sceneBuffer = sceneBuffer;
        }

        public bool Available => true;

        public BufferedContainerView<Drawable> CreateSceneView()
        {
            var view = sceneBuffer.CreateView();
            view.SynchronisedDrawQuad = true;
            return view;
        }
    }
}
