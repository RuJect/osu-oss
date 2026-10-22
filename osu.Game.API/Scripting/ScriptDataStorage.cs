// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Platform;

namespace osu.Game.API.Scripting
{
    /// <summary>
    /// Storage abstraction over the scripts data directory (<c>osu-oss/scripts/data</c>), where scripts may
    /// freely read and write files.
    /// </summary>
    public class ScriptDataStorage : ScriptFileStore
    {
        public ScriptDataStorage(Storage storage)
            : base(storage)
        {
        }
    }
}
