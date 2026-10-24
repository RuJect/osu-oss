// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Platform;
using osu.Game.API.Services;

namespace osu.Game.API.Scripting
{
    /// <summary>
    /// Storage abstraction over the user's <c>osu-oss/scripts</c> directory.
    /// </summary>
    public class ScriptStorage : FileStore
    {
        /// <summary>Script executed once the game has finished loading.</summary>
        public const string LAUNCH_SCRIPT = "on_launch.js";

        /// <summary>Script executed after the game has been exited.</summary>
        public const string EXIT_SCRIPT = "on_exit.js";

        public ScriptStorage(Storage storage)
            : base(storage)
        {
        }

        /// <summary>Recursively lists every <c>.js</c> script, relative to the scripts directory.</summary>
        public IEnumerable<string> EnumerateScripts()
            => List(string.Empty)
               .Where(f => f.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
               .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Ensures the scripts directory is populated with the magic scripts.
        /// Existing files are never overwritten.
        /// </summary>
        public void EnsureInitialised()
        {
            writeDefault(LAUNCH_SCRIPT, default_launch);
            writeDefault(EXIT_SCRIPT, default_exit);
        }

        private void writeDefault(string fileName, string content)
        {
            if (!Exists(fileName))
                Write(fileName, content);
        }

        private const string default_launch = @"// on_launch.js
// Executed once, after the game has finished loading.
// The global `osu` and `console` objects, and the raw game/host under `internal`, are available.

osu.log(""osu!oss scripting online"");

// osu.onUpdate(() =>
// {
//     // runs every frame
// });
";

        private const string default_exit = @"// on_exit.js
// Executed after the game has been exited. The UI is no longer available,
// so only headless work (writing files, cleanup) should be performed here.

osu.log(""osu!oss scripting shut down"");
";
    }
}
