// events.js
// Event bus: react to game events and other scripts.

// Subscribe to screen changes.
osu.on("screen", name => osu.log("Now on screen: " + name));

// One-shot: only the first screen change.
osu.once("screen", name => osu.log("First screen: " + name));

// React to other scripts' lifecycle.
osu.on("scriptStarted", n => osu.log("A script started: " + n));
osu.on("scriptStopped", n => osu.log("A script stopped: " + n));
osu.on("scriptError", (n, msg) => osu.log("A script errored: " + n + " (" + msg + ")"));

// Unsubscribe.
let handler = () => osu.log("this never fires");
osu.on("screen", handler);
osu.off("screen", handler);

// Note: a script can't hear its own events emitted during startup (the instance
// isn't running yet), but it can subscribe to events from other scripts.
