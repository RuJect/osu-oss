// notifications.js
// Notifications: simple and progress.

// Simple notification.
osu.notify.post("Hello from a script!");

// With a level.
osu.notify.post("Something went wrong", "error");

// Ongoing progress with a handle.
let p = osu.notify.progress("Downloading...");
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
