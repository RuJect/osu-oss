// hello.js
// Basics: logging, the global `osu` object, and running other scripts.

osu.log("Hello from examples/hello.js!");

// Print to the console output (same channel as osu.log).
console.info("console.info works too");

// The running client version.
osu.log("osu version: " + osu.version);

// List every known script (including these examples).
osu.log("scripts: " + osu.scripts().join(", "));

// Iterate a collection.
osu.each([1, 2, 3], n => osu.log("count: " + n));

// Run another script by name and stop it again.
// let handle = osu.run("storage");
// handle.stop();
