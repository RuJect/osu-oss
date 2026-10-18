// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework.Platform;

namespace osu.Game.API.Scripting
{
    /// <summary>
    /// Storage abstraction over the user's <c>osu-oss/scripts</c> directory.
    /// </summary>
    public class ScriptStorage
    {
        /// <summary>Script executed once the game has finished loading.</summary>
        public const string LAUNCH_SCRIPT = "on_launch.js";

        /// <summary>Script executed after the game has been exited.</summary>
        public const string EXIT_SCRIPT = "on_exit.js";

        /// <summary>Example script created on first run to document the API.</summary>
        public const string EXAMPLE_SCRIPT = "example.js";

        private const string script_search_pattern = "*.js";

        private readonly Storage storage;

        public ScriptStorage(Storage storage)
        {
            this.storage = storage;
        }

        /// <summary>The absolute path of the scripts directory.</summary>
        public string Directory => storage.GetFullPath(string.Empty);

        public bool Exists(string fileName) => storage.Exists(fileName);

        public IEnumerable<string> EnumerateScripts()
            => storage.GetFiles(string.Empty, script_search_pattern)
                      .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);

        public string Read(string fileName)
        {
            using (Stream stream = storage.GetStream(fileName, FileAccess.Read, FileMode.Open))
            using (var reader = new StreamReader(stream))
                return reader.ReadToEnd();
        }

        public void Write(string fileName, string content)
        {
            using (Stream stream = storage.CreateFileSafely(fileName))
            using (var writer = new StreamWriter(stream))
                writer.Write(content);
        }

        public void Delete(string fileName)
        {
            if (storage.Exists(fileName))
                storage.Delete(fileName);
        }

        /// <summary>
        /// Ensures the scripts directory is populated with the magic scripts and an example.
        /// Existing files are never overwritten.
        /// </summary>
        public void EnsureInitialised()
        {
            writeDefault(LAUNCH_SCRIPT, default_launch);
            writeDefault(EXIT_SCRIPT, default_exit);
            writeDefault(EXAMPLE_SCRIPT, default_example);
        }

        private void writeDefault(string fileName, string content)
        {
            if (!storage.Exists(fileName))
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

        private const string default_example = @"// example.js
// A small tour of the osu!oss scripting API. Run it from the scripting console (Ctrl+F4).

osu.log(""Hello from example.js!"");

// Print to the console output.
console.info(""console.info works too"");

// React to incoming events.
osu.on(""screen"", name => osu.log(""Now on screen: "" + name));

// Run something every frame...
let frames = 0;
osu.onUpdate(() =>
{
    frames++;
});

// ...or on a fixed interval.
osu.every(1000, () => osu.log(""alive for "" + frames + "" frames""));

// Persist data under osu-oss/scripts/data (sandboxed; paths cannot escape it).
osu.storage.write(""runs.txt"", ""ran at "" + Date.now());

// The raw game/host instances live under `internal` for advanced scripts.
// osu.notify(""Hello from a script!"");
// Full access to the CLR is available too.
// let colour = System.Console.ForegroundColor;
";
    }
}
