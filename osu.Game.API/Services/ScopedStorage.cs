// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Platform;

namespace osu.Game.API.Services
{
    /// <summary>
    /// Storage abstraction over the scripts data directory (<c>osu-oss/scripts/data</c>), where scripts and
    /// plugins may freely read and write files.
    /// </summary>
    public class ScopedStorage : FileStore
    {
        public ScopedStorage(Storage storage)
            : base(storage)
        {
        }
    }
}
