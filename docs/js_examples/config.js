// config.js
// Read and write osu! settings (keys are OsuSetting member names, case-insensitive).

// Read a value.
osu.log("username: " + osu.config.get("Username"));

// Write a value.
osu.config.set("ShowConvertedBeatmaps", false);
osu.log("showConverted: " + osu.config.get("ShowConvertedBeatmaps"));

// The full list of available keys.
osu.log("keys: " + osu.config.keys().join(", "));
