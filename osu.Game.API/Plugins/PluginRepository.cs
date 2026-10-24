// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework.Platform;

namespace osu.Game.API.Plugins
{
    /// <summary>Discovers plugin assemblies under a scoped <see cref="Storage"/> directory.</summary>
    public class PluginRepository
    {
        private readonly Storage storage;

        public PluginRepository(Storage storage)
        {
            this.storage = storage;
        }

        /// <summary>Lists every plugin assembly directly under the plugins directory.</summary>
        public IEnumerable<PluginDefinition> EnumeratePlugins()
            => storage.GetFiles(string.Empty, "*.dll")
                      .Select(file => new PluginDefinition(Path.GetFileNameWithoutExtension(file), storage.GetFullPath(file)));
    }
}
