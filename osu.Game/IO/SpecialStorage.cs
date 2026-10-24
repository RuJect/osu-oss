// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Platform;

namespace osu.Game.IO
{
    /// <summary>
    /// Resolves the scoped <c>osu-oss</c> storage directory which holds all files specific to
    /// this fork (configuration, downloaded blobs, caches, ...). The directory lives inside the
    /// main osu! storage root, e.g. <c>&lt;user storage&gt;/osu/osu-oss</c>.
    /// </summary>
    public static class SpecialStorage
    {
        /// <summary>Name of the scoped directory inside the osu! storage.</summary>
        public const string ROOT_FOLDER = "osu-oss";

        /// <summary>Where proprietary authentication blobs are cached.</summary>
        public const string AUTH_BLOBS_FOLDER = "auth-blobs";

        /// <summary>Where user scripts live.</summary>
        public const string SCRIPTS_FOLDER = "scripts";

        /// <summary>Where user plugins (C# assemblies) live.</summary>
        public const string PLUGINS_FOLDER = "plugins";

        /// <summary>Where scripts may persist arbitrary data files (relative to <see cref="SCRIPTS_FOLDER"/>).</summary>
        public const string SCRIPT_DATA_FOLDER = "data";

        /// <summary>Config file name (relative to <see cref="ROOT_FOLDER"/>).</summary>
        public const string CONFIG_FILENAME = "config.ini";

        /// <summary>Returns the storage scoped to the <c>osu-oss</c> directory, creating it if required.</summary>
        public static Storage GetRoot(Storage osuStorage) => osuStorage.GetStorageForDirectory(ROOT_FOLDER);

        /// <summary>Returns the storage scoped to the <c>osu-oss/auth-blobs</c> directory, creating it if required.</summary>
        public static Storage GetAuthBlobs(Storage osuStorage) => GetRoot(osuStorage).GetStorageForDirectory(AUTH_BLOBS_FOLDER);

        /// <summary>Returns the storage scoped to the <c>osu-oss/scripts</c> directory, creating it if required.</summary>
        public static Storage GetScripts(Storage osuStorage) => GetRoot(osuStorage).GetStorageForDirectory(SCRIPTS_FOLDER);

        /// <summary>Returns the storage scoped to the <c>osu-oss/scripts/data</c> directory, creating it if required.</summary>
        public static Storage GetScriptData(Storage osuStorage) => GetScripts(osuStorage).GetStorageForDirectory(SCRIPT_DATA_FOLDER);

        /// <summary>Returns the storage scoped to the <c>osu-oss/plugins</c> directory, creating it if required.</summary>
        public static Storage GetPlugins(Storage osuStorage) => GetRoot(osuStorage).GetStorageForDirectory(PLUGINS_FOLDER);

        /// <summary>Absolute path of the <c>osu-oss</c> directory.</summary>
        public static string GetRootPath(Storage osuStorage) => osuStorage.GetFullPath(ROOT_FOLDER);
    }
}
