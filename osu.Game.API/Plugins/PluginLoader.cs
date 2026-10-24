// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using osu.Game.API.Services;

namespace osu.Game.API.Plugins
{
    /// <summary>
    /// Instantiates <see cref="IPlugin"/>s from assemblies on disk. Each concrete, public, parameterless
    /// type implementing <see cref="IPlugin"/> in an assembly yields one instance.
    /// </summary>
    public class PluginLoader
    {
        private readonly OutputLog output;

        public PluginLoader(OutputLog output)
        {
            this.output = output;
        }

        /// <summary>Loads every plugin type from the given repository.</summary>
        public IEnumerable<IPlugin> LoadFrom(PluginRepository repository)
            => repository.EnumeratePlugins().SelectMany(definition => LoadFromFile(definition.Path));

        /// <summary>Loads every plugin type in the assembly at <paramref name="path"/>. Failures are logged.</summary>
        public IEnumerable<IPlugin> LoadFromFile(string path)
        {
            Assembly assembly;

            try
            {
                assembly = Assembly.LoadFrom(path);
            }
            catch (Exception e)
            {
                output.Log(OutputLevel.Error, Path.GetFileName(path), $"failed to load assembly: {e.Message}");
                return Array.Empty<IPlugin>();
            }

            var plugins = new List<IPlugin>();

            foreach (Type type in getLoadableTypes(assembly))
            {
                if (!typeof(IPlugin).IsAssignableFrom(type) || type.IsAbstract || type.IsInterface)
                    continue;

                if (type.GetConstructor(Type.EmptyTypes) == null)
                {
                    output.Log(OutputLevel.Warn, type.Name, "skipped: no public parameterless constructor");
                    continue;
                }

                try
                {
                    plugins.Add((IPlugin)Activator.CreateInstance(type)!);
                }
                catch (Exception e)
                {
                    output.Log(OutputLevel.Error, type.Name, $"failed to instantiate: {e.Message}");
                }
            }

            return plugins;
        }

        private static IEnumerable<Type> getLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t != null)!;
            }
        }
    }
}
