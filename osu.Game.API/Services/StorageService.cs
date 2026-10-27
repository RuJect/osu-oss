// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;

namespace osu.Game.API.Services
{
    /// <summary>
    /// Sandboxed file access over the scripts data directory. All paths are resolved relative to
    /// <see cref="Directory"/> and may not escape it. Also hosts the JSON key-value helpers under
    /// <c>kv/</c> used by <c>osu.kv</c>.
    /// </summary>
    public class StorageService
    {
        private const string kv_prefix = "kv/";

        private readonly FileStore storage;

        public StorageService(FileStore storage)
        {
            this.storage = storage;
        }

        public string Directory => storage.Directory;

        public bool Exists(string path) => storage.Exists(path);

        public string? Read(string path) => storage.Read(path);

        public void Write(string path, string content) => storage.Write(path, content);

        public void Append(string path, string content) => storage.Append(path, content);

        public void Delete(string path) => storage.Delete(path);

        public void CreateDirectory(string path) => storage.CreateDirectory(path);

        public IEnumerable<string> List(string path = "") => storage.List(path);

        /// <summary>Reads the raw JSON stored under the key, or <c>null</c> when missing.</summary>
        public string? KvRead(string key)
        {
            string? path = keyPath(key);
            return path != null && storage.Exists(path) ? storage.Read(path) : null;
        }

        /// <summary>Stores raw JSON under the key.</summary>
        public void KvWrite(string key, string json)
        {
            string? path = keyPath(key);

            if (path != null)
                storage.Write(path, json);
        }

        public void KvDelete(string key)
        {
            string? path = keyPath(key);

            if (path != null)
                storage.Delete(path);
        }

        public string[] KvKeys()
            => storage.List("kv").Select(f => f[kv_prefix.Length..]).ToArray();

        private static string? keyPath(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;

            string? path = FileStore.SanitizePath(key);
            return path == null ? null : kv_prefix + path;
        }
    }
}
