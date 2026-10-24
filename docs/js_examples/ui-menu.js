// ui-menu.js
// An overlay acting as a small menu, tracking every action it performs.

let overlay = osu.ui.overlay("Script menu");
let status = overlay.addLabel("Pick an action");

function track(message)
{
    status.setText(message);
    osu.log("[menu] " + message);
}

overlay.addButton("Say hello", () => track("said hello"));

overlay.addButton("Play music", () =>
{
    osu.music.play();
    track("told the music to play");
});

overlay.addButton("Notify", () =>
{
    osu.notify.post("Hello from the script menu");
    track("posted a notification");
});

overlay.addButton("Close", () => overlay.hide());

overlay.show();

// Keep the script alive for the button callbacks.
osu.onUpdate(() => {});
