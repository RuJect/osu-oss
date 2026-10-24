// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using osu.Framework.Platform;

namespace osu.Game.API.Scripting
{
    /// <summary>
    /// Shared file operations over a scoped <see cref="Storage"/>. Used by both the script store and the
    /// script data store so read/write/list logic lives in a single place.
    /// </summary>
    public abstract class ScriptFileStore
    {
        private readonly Storage storage;

        protected ScriptFileStore(Storage storage)
        {
            this.storage = storage;
        }

        /// <summary>The absolute path of the store's root directory.</summary>
        public string Directory => storage.GetFullPath(string.Empty);

        public bool Exists(string path)
            => tryResolve(path, out string safe) && storage.Exists(safe);

        public string? Read(string path)
        {
            if (!tryResolve(path, out string safe))
                return null;

            using Stream stream = storage.GetStream(safe, FileAccess.Read, FileMode.Open);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        public void Write(string path, string content)
        {
            if (!tryResolve(path, out string safe))
                return;

            using Stream stream = storage.CreateFileSafely(safe);
            using var writer = new StreamWriter(stream);
            writer.Write(content);
        }

        public void Append(string path, string content)
        {
            if (!tryResolve(path, out string safe))
                return;

            storage.GetFullPath(safe, true);

            using Stream stream = storage.GetStream(safe, FileAccess.Write, FileMode.Append);
            using var writer = new StreamWriter(stream);
            writer.Write(content);
        }

        public void Delete(string path)
        {
            if (!tryResolve(path, out string safe))
                return;

            if (storage.Exists(safe))
                storage.Delete(safe);
            else if (storage.ExistsDirectory(safe))
                storage.DeleteDirectory(safe);
        }

        /// <summary>Creates the directory at <paramref name="path"/>, including any missing parents.</summary>
        public void CreateDirectory(string path)
        {
            if (tryResolve(path, out string safe))
                storage.GetFullPath(safe, true);
        }

        /// <summary>Lists the files immediately under <paramref name="path"/>, optionally filtered by a wildcard pattern.</summary>
        public IEnumerable<string> ListFiles(string path = "", string pattern = "*")
            => tryResolveDirectory(path, out string safe) ? storage.GetFiles(safe, pattern) : Array.Empty<string>();

        /// <summary>Recursively lists all files under <paramref name="prefix"/>, which may be empty to list everything.</summary>
        public IEnumerable<string> List(string prefix = "")
        {
            if (!tryResolveDirectory(prefix, out string safe))
                yield break;

            foreach (string file in enumerate(safe))
                yield return file;
        }

        /// <summary>
        /// Normalises a script-supplied path and rejects absolute paths or directory traversal.
        /// Returns <c>null</c> when the path is empty or would escape the store.
        /// </summary>
        public static string? SanitizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            if (Path.IsPathRooted(path))
                return null;

            string[] parts = path.Replace('\\', '/').Split('/');
            var kept = new List<string>();

            foreach (string part in parts)
            {
                if (part.Length == 0 || part == ".")
                    continue;

                if (part == "..")
                    return null;

                kept.Add(part);
            }

            return kept.Count == 0 ? null : string.Join("/", kept);
        }

        private static bool tryResolve(string path, out string safe)
        {
            safe = SanitizePath(path) ?? string.Empty;
            return safe.Length > 0;
        }

        private static bool tryResolveDirectory(string path, out string safe)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                safe = string.Empty;
                return true;
            }

            return tryResolve(path, out safe);
        }

        private IEnumerable<string> enumerate(string prefix)
        {
            if (prefix.Length > 0 && !storage.Exists(prefix) && !storage.ExistsDirectory(prefix))
                yield break;

            foreach (string file in storage.GetFiles(prefix, "*"))
                yield return file;

            foreach (string dir in storage.GetDirectories(prefix))
            {
                foreach (string file in enumerate(dir))
                    yield return file;
            }
        }
    }
}
