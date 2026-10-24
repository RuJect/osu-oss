// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Platform;

namespace osu.Game.API.Scripting
{
    /// <summary>
    /// Storage abstraction over the user's <c>osu-oss/scripts</c> directory.
    /// </summary>
    public class ScriptStorage : ScriptFileStore
    {
        /// <summary>Script executed once the game has finished loading.</summary>
        public const string LAUNCH_SCRIPT = "on_launch.js";

        /// <summary>Script executed after the game has been exited.</summary>
        public const string EXIT_SCRIPT = "on_exit.js";

        /// <summary>Subdirectory holding the bundled example scripts.</summary>
        public const string EXAMPLES_FOLDER = "examples";

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
        /// Ensures the scripts directory is populated with the magic scripts and the bundled examples.
        /// Existing files are never overwritten.
        /// </summary>
        public void EnsureInitialised()
        {
            writeDefault(LAUNCH_SCRIPT, default_launch);
            writeDefault(EXIT_SCRIPT, default_exit);

            writeDefault(EXAMPLES_FOLDER + "/hello.js", example_hello);
            writeDefault(EXAMPLES_FOLDER + "/storage.js", example_storage);
            writeDefault(EXAMPLES_FOLDER + "/timers.js", example_timers);
            writeDefault(EXAMPLES_FOLDER + "/events.js", example_events);
            writeDefault(EXAMPLES_FOLDER + "/config.js", example_config);
            writeDefault(EXAMPLES_FOLDER + "/music.js", example_music);
            writeDefault(EXAMPLES_FOLDER + "/notifications.js", example_notifications);
            writeDefault(EXAMPLES_FOLDER + "/ui.js", example_ui);
            writeDefault(EXAMPLES_FOLDER + "/ui-menu.js", example_ui_menu);
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

        private const string example_hello = @"// examples/hello.js
// Basics: logging, the global `osu` object, and running other scripts.

osu.log(""Hello from examples/hello.js!"");

// Print to the console output (same channel as osu.log).
console.info(""console.info works too"");

// The running client version.
osu.log(""osu version: "" + osu.version);

// List every known script (including these examples).
osu.log(""scripts: "" + osu.scripts().join("", ""));

// Iterate a collection.
osu.each([1, 2, 3], n => osu.log(""count: "" + n));

// Run another script by name and stop it again.
// let handle = osu.run(""storage"");
// handle.stop();
";

        private const string example_storage = @"// examples/storage.js
// Sandboxed file storage (osu.storage) and the key-value store (osu.kv).
// Files live under osu-oss/scripts/data and cannot escape it.

// Write and read a plain text file.
osu.storage.write(""hello.txt"", ""Hello, world!"");
osu.log(""read: "" + osu.storage.read(""hello.txt""));

// Append to it.
osu.storage.append(""hello.txt"", ""\nSecond line"");
osu.log(""after append: "" + osu.storage.read(""hello.txt""));

// JSON round-trip.
osu.storage.writeJson(""data.json"", { score: 1234, combo: 777 });
let data = osu.storage.readJson(""data.json"");
osu.log(""json: score="" + data.score + "", combo="" + data.combo);

// Directories, existence and listing.
osu.storage.mkdir(""saves"");
osu.log(""exists: "" + osu.storage.exists(""hello.txt""));
osu.log(""files: "" + osu.storage.list("""").join("", ""));

// Persistent key-value store (JSON under osu-oss/scripts/data/kv).
osu.kv.set(""launchCount"", (osu.kv.get(""launchCount"") || 0) + 1);
osu.log(""launchCount="" + osu.kv.get(""launchCount""));
osu.log(""kv keys: "" + osu.kv.keys().join("", ""));
";

        private const string example_timers = @"// examples/timers.js
// Scheduling: frame callbacks, one-shot/repeating timers, and sleep.

let frames = 0;

// Runs every frame.
osu.onUpdate(() => frames++);

// Runs once after 1000 ms.
osu.after(1000, () => osu.log(""after 1s (frames="" + frames + "")""));

// Repeats every 500 ms.
let tick = osu.every(500, () => osu.log(""tick (frames="" + frames + "")""));

// JS-style aliases.
let once = osu.setTimeout(() => osu.log(""setTimeout fired""), 1500);
osu.clearTimeout(once);

// Cancel the repeating timer after 2 s.
osu.setTimeout(() => { osu.cancel(tick); osu.log(""cancelled""); }, 2000);

// Top-level await (scripts run as modules).
await osu.sleep(1200);
osu.log(""woke up after 1.2 s"");
";

        private const string example_events = @"// examples/events.js
// Event bus: react to game events and other scripts.

// Subscribe to screen changes.
osu.on(""screen"", name => osu.log(""Now on screen: "" + name));

// One-shot: only the first screen change.
osu.once(""screen"", name => osu.log(""First screen: "" + name));

// React to other scripts' lifecycle.
osu.on(""scriptStarted"", n => osu.log(""A script started: "" + n));
osu.on(""scriptStopped"", n => osu.log(""A script stopped: "" + n));
osu.on(""scriptError"", (n, msg) => osu.log(""A script errored: "" + n + "" ("" + msg + "")""));

// Unsubscribe.
let handler = () => osu.log(""this never fires"");
osu.on(""screen"", handler);
osu.off(""screen"", handler);

// Note: a script can't hear its own events emitted during startup (the instance
// isn't running yet), but it can subscribe to events from other scripts.
";

        private const string example_config = @"// examples/config.js
// Read and write osu! settings (keys are OsuSetting member names, case-insensitive).

// Read a value.
osu.log(""username: "" + osu.config.get(""Username""));

// Write a value.
osu.config.set(""ShowConvertedBeatmaps"", false);
osu.log(""showConverted: "" + osu.config.get(""ShowConvertedBeatmaps""));

// The full list of available keys.
osu.log(""keys: "" + osu.config.keys().join("", ""));
";

        private const string example_music = @"// examples/music.js
// Control global music playback.

osu.log(""playing: "" + osu.music.isPlaying());

// Start playback (scheduled on the update thread).
osu.music.play();

// Metadata of the current track.
let track = osu.music.currentTrack();
if (track)
    osu.log(""track: "" + track.artist + "" - "" + track.title + "" ("" + track.beatmapId + "")"");

// Other controls.
// osu.music.seek(30000);  // seek to 30 s
// osu.music.next();
// osu.music.previous();
// osu.music.pause();
// osu.music.stop();
";

        private const string example_notifications = @"// examples/notifications.js
// Notifications: simple and progress.

// Simple notification.
osu.notify.post(""Hello from a script!"");

// With a level.
osu.notify.post(""Something went wrong"", ""error"");

// Ongoing progress with a handle.
let p = osu.notify.progress(""Downloading..."");
let progress = 0;
let timer = osu.setInterval(() =>
{
    progress += 0.1;
    p.setProgress(progress);

    if (progress >= 1)
    {
        osu.clearInterval(timer);
        p.complete();
    }
}, 200);
";

        private const string example_ui = @"// examples/ui.js
// A script-owned overlay with interactive elements.
// Buttons update a shared label, so you can see the actions being tracked.

let clicks = 0;

let overlay = osu.ui.overlay(""Script counter"");
let status = overlay.addLabel(""Clicks: 0"");

overlay.addButton(""Click me"", () =>
{
    clicks++;
    status.setText(""Clicks: "" + clicks);
});

overlay.addButton(""Reset"", () =>
{
    clicks = 0;
    status.setText(""Clicks: 0"");
});

overlay.addButton(""Hide"", () => overlay.hide());

overlay.show();

// Keep the script running so the button callbacks stay alive.
// A script with no timers, update callbacks or subscriptions is torn down.
osu.onUpdate(() => {});
";

        private const string example_ui_menu = @"// examples/ui-menu.js
// An overlay acting as a small menu, tracking every action it performs.

let overlay = osu.ui.overlay(""Script menu"");
let status = overlay.addLabel(""Pick an action"");

function track(message)
{
    status.setText(message);
    osu.log(""[menu] "" + message);
}

overlay.addButton(""Say hello"", () => track(""said hello""));

overlay.addButton(""Play music"", () =>
{
    osu.music.play();
    track(""told the music to play"");
});

overlay.addButton(""Notify"", () =>
{
    osu.notify.post(""Hello from the script menu"");
    track(""posted a notification"");
});

overlay.addButton(""Close"", () => overlay.hide());

overlay.show();

// Keep the script alive for the button callbacks.
osu.onUpdate(() => {});
";
    }
}
