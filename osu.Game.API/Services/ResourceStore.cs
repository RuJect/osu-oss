// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.IO;

namespace osu.Game.API.Services
{
    /// <summary>
    /// Read-only access to a plugin's resources directory (<c>plugins/&lt;id&gt;/res</c>).
    /// </summary>
    public class ResourceStore
    {
        private readonly FileStore storage;

        public ResourceStore(FileStore storage)
        {
            this.storage = storage;
        }

        /// <summary>The absolute path of the store's root directory.</summary>
        public string Directory => storage.Directory;

        public bool Exists(string path) => storage.Exists(path);

        public string? Read(string path) => storage.Read(path);

        public byte[]? ReadBytes(string path) => storage.ReadBytes(path);

        public Stream? OpenRead(string path) => storage.OpenRead(path);

        /// <summary>Recursively lists all files under <paramref name="prefix"/>, which may be empty to list everything.</summary>
        public IEnumerable<string> List(string prefix = "") => storage.List(prefix);
    }
}
