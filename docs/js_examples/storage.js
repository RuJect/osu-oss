// storage.js
// Sandboxed file storage (osu.storage) and the key-value store (osu.kv).
// Files live under osu-oss/scripts/data and cannot escape it.

// Write and read a plain text file.
osu.storage.write("hello.txt", "Hello, world!");
osu.log("read: " + osu.storage.read("hello.txt"));

// Append to it.
osu.storage.append("hello.txt", "\nSecond line");
osu.log("after append: " + osu.storage.read("hello.txt"));

// JSON round-trip.
osu.storage.writeJson("data.json", { score: 1234, combo: 777 });
let data = osu.storage.readJson("data.json");
osu.log("json: score=" + data.score + ", combo=" + data.combo);

// Directories, existence and listing.
osu.storage.mkdir("saves");
osu.log("exists: " + osu.storage.exists("hello.txt"));
osu.log("files: " + osu.storage.list("").join(", "));

// Persistent key-value store (JSON under osu-oss/scripts/data/kv).
osu.kv.set("launchCount", (osu.kv.get("launchCount") || 0) + 1);
osu.log("launchCount=" + osu.kv.get("launchCount"));
osu.log("kv keys: " + osu.kv.keys().join(", "));
