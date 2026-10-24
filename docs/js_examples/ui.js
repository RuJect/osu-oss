// ui.js
// A script-owned overlay with interactive elements.
// Buttons update a shared label, so you can see the actions being tracked.

let clicks = 0;

let overlay = osu.ui.overlay("Script counter");
let status = overlay.addLabel("Clicks: 0");

overlay.addButton("Click me", () =>
{
    clicks++;
    status.setText("Clicks: " + clicks);
});

overlay.addButton("Reset", () =>
{
    clicks = 0;
    status.setText("Clicks: 0");
});

overlay.addButton("Hide", () => overlay.hide());

overlay.show();

// Keep the script running so the button callbacks stay alive.
// A script with no timers, update callbacks or subscriptions is torn down.
osu.onUpdate(() => {});
