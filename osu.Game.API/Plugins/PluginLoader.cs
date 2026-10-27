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
    /// Instantiates <see cref="PluginBase"/>s from plugin manifests. Entry points are resolved from the
    /// manifest directory here; each manifest yields at most one result.
    /// </summary>
    public class PluginLoader
    {
        private readonly OutputLog output;

        public PluginLoader(OutputLog output)
        {
            this.output = output;
        }

        /// <summary>Loads every plugin from the given repository.</summary>
        public IEnumerable<PluginLoadResult> LoadFrom(PluginRepository repository)
            => repository.EnumeratePlugins().SelectMany(LoadFromManifest);

        /// <summary>Loads the entry points of <paramref name="manifest"/>. Failures are logged.</summary>
        public IEnumerable<PluginLoadResult> LoadFromManifest(PluginManifest manifest)
        {
            var results = new List<PluginLoadResult>();

            resolveEntryPoints(manifest);

            if (!string.IsNullOrEmpty(manifest.CSharpEntryPoint))
            {
                var plugin = loadCSharpPlugin(manifest.CSharpEntryPoint, manifest.Id);

                if (plugin != null)
                    results.Add(new PluginLoadResult(manifest, plugin));
            }

            if (!string.IsNullOrEmpty(manifest.JavaScriptEntryPoint))
                output.Log(OutputLevel.Warn, manifest.Id, "javascript entry point ignored: JS plugins are not supported yet");

            if (results.Count == 0)
                output.Log(OutputLevel.Warn, manifest.Id, "no entry point found (expected bin/<id>.dll)");

            return results;
        }

        private static void resolveEntryPoints(PluginManifest manifest)
        {
            if (!string.IsNullOrEmpty(manifest.DirectoryPath))
            {
                if (string.IsNullOrEmpty(manifest.CSharpEntryPoint))
                {
                    string csharpPath = Path.Combine(manifest.DirectoryPath, "bin", $"{manifest.Id}.dll");

                    if (File.Exists(csharpPath))
                        manifest.CSharpEntryPoint = csharpPath;
                }

                if (string.IsNullOrEmpty(manifest.JavaScriptEntryPoint))
                {
                    string scriptPath = Path.Combine(manifest.DirectoryPath, "scripts", $"{manifest.Id}.js");

                    if (File.Exists(scriptPath))
                        manifest.JavaScriptEntryPoint = scriptPath;
                }
            }
        }

        private PluginBase? loadCSharpPlugin(string path, string pluginId)
        {
            Assembly assembly;

            try
            {
                assembly = Assembly.LoadFrom(path);
            }
            catch (Exception e)
            {
                output.Log(OutputLevel.Error, pluginId, $"failed to load assembly: {e.Message}");
                return null;
            }

            var candidates = new List<Type>();

            foreach (Type type in getLoadableTypes(assembly))
            {
                if (!typeof(PluginBase).IsAssignableFrom(type) || type.IsAbstract || type.IsInterface || !type.IsPublic)
                    continue;

                if (type.GetConstructor(Type.EmptyTypes) == null)
                {
                    output.Log(OutputLevel.Warn, type.Name, "skipped: no public parameterless constructor");
                    continue;
                }

                candidates.Add(type);
            }

            if (candidates.Count > 1)
            {
                string ignored = string.Join(", ", candidates.Skip(1).Select(t => t.FullName));
                output.Log(OutputLevel.Warn, pluginId, $"multiple PluginBase subclasses found; loading {candidates[0].FullName} and ignoring: {ignored}");
            }

            foreach (Type type in candidates)
            {
                try
                {
                    return (PluginBase)Activator.CreateInstance(type)!;
                }
                catch (Exception e)
                {
                    output.Log(OutputLevel.Error, type.Name, $"failed to instantiate: {e.Message}");
                }
            }

            output.Log(OutputLevel.Warn, pluginId, "no PluginBase subclass found in assembly");
            return null;
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

    /// <summary>A ready-to-load plugin: its manifest plus instantiated C# entry point.</summary>
    public sealed class PluginLoadResult
    {
        public PluginManifest Manifest { get; }

        public PluginBase CSharpPlugin { get; }

        public PluginLoadResult(PluginManifest manifest, PluginBase csharpPlugin)
        {
            Manifest = manifest;
            CSharpPlugin = csharpPlugin;
        }
    }
}
