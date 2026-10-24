// music.js
// Control global music playback.

osu.log("playing: " + osu.music.isPlaying());

// Start playback (scheduled on the update thread).
osu.music.play();

// Metadata of the current track.
let track = osu.music.currentTrack();
if (track)
    osu.log("track: " + track.artist + " - " + track.title + " (" + track.beatmapId + ")");

// Other controls.
// osu.music.seek(30000);  // seek to 30 s
// osu.music.next();
// osu.music.previous();
// osu.music.pause();
// osu.music.stop();
