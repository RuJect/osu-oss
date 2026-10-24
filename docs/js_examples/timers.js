// timers.js
// Scheduling: frame callbacks, one-shot/repeating timers, and sleep.

let frames = 0;

// Runs every frame.
osu.onUpdate(() => frames++);

// Runs once after 1000 ms.
osu.after(1000, () => osu.log("after 1s (frames=" + frames + ")"));

// Repeats every 500 ms.
let tick = osu.every(500, () => osu.log("tick (frames=" + frames + ")"));

// JS-style aliases.
let once = osu.setTimeout(() => osu.log("setTimeout fired"), 1500);
osu.clearTimeout(once);

// Cancel the repeating timer after 2 s.
osu.setTimeout(() => { osu.cancel(tick); osu.log("cancelled"); }, 2000);

// Top-level await (scripts run as modules).
await osu.sleep(1200);
osu.log("woke up after 1.2 s");
