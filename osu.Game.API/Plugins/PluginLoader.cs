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
        private readonly Func<PluginManifest, PluginLogger> loggerFactory;

        public PluginLoader(OutputLog output, Func<PluginManifest, PluginLogger>? loggerFactory = null)
        {
            this.output = output;
            this.loggerFactory = loggerFactory ?? (manifest => new PluginLogger(manifest.Id));
        }

        /// <summary>Loads every plugin from the given repository.</summary>
        public IEnumerable<PluginLoadResult> LoadFrom(PluginRepository repository)
            => repository.EnumeratePlugins().SelectMany(LoadFromManifest);

        /// <summary>Loads the entry points of <paramref name="manifest"/>. Failures are logged.</summary>
        public IEnumerable<PluginLoadResult> LoadFromManifest(PluginManifest manifest)
        {
            PluginLogger logger = loggerFactory(manifest);
            var results = new List<PluginLoadResult>();

            try
            {
                resolveEntryPoints(manifest);

                PluginBase? plugin = null;

                if (!string.IsNullOrEmpty(manifest.CSharpEntryPoint))
                    plugin = loadCSharpPlugin(manifest.CSharpEntryPoint, manifest.Id, logger);

                bool hasScript = !string.IsNullOrEmpty(manifest.JavaScriptEntryPoint);

                if (plugin != null || hasScript)
                    results.Add(new PluginLoadResult(manifest, plugin, manifest.JavaScriptEntryPoint));

                if (results.Count == 0)
                    log(logger, OutputLevel.Warn, manifest.Id, "no entry point found (expected bin/<id>.dll or scripts/<id>.js)");
            }
            catch (Exception e)
            {
                log(logger, OutputLevel.Error, manifest.Id, $"failed to load plugin: {e.Message}");
            }

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

        private PluginBase? loadCSharpPlugin(string path, string pluginId, PluginLogger logger)
        {
            Assembly assembly;

            try
            {
                assembly = Assembly.LoadFrom(path);
            }
            catch (Exception e)
            {
                log(logger, OutputLevel.Error, pluginId, $"failed to load assembly: {e.Message}");
                return null;
            }

            var candidates = new List<Type>();

            foreach (Type type in getLoadableTypes(assembly))
            {
                if (!typeof(PluginBase).IsAssignableFrom(type) || type.IsAbstract || type.IsInterface || !type.IsPublic)
                    continue;

                if (type.GetConstructor(Type.EmptyTypes) == null)
                {
                    log(logger, OutputLevel.Warn, type.Name, "skipped: no public parameterless constructor");
                    continue;
                }

                candidates.Add(type);
            }

            if (candidates.Count > 1)
            {
                string ignored = string.Join(", ", candidates.Skip(1).Select(t => t.FullName));
                log(logger, OutputLevel.Warn, pluginId, $"multiple PluginBase subclasses found; loading {candidates[0].FullName} and ignoring: {ignored}");
            }

            foreach (Type type in candidates)
            {
                try
                {
                    return (PluginBase)Activator.CreateInstance(type)!;
                }
                catch (Exception e)
                {
                    log(logger, OutputLevel.Error, type.Name, $"failed to instantiate: {e.Message}");
                }
            }

            log(logger, OutputLevel.Warn, pluginId, "no PluginBase subclass found in assembly");
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

        private void log(PluginLogger logger, OutputLevel level, string source, string message)
        {
            output.Log(level, source, message);

            switch (level)
            {
                case OutputLevel.Error:
                    logger.Error(message);
                    break;

                case OutputLevel.Warn:
                    logger.Warn(message);
                    break;

                default:
                    logger.Info(message);
                    break;
            }
        }
    }

    /// <summary>A ready-to-load plugin: its manifest plus any instantiated C# and JavaScript entry points.</summary>
    public sealed class PluginLoadResult
    {
        public PluginManifest Manifest { get; }

        public PluginBase? CSharpPlugin { get; }

        public string? JavaScriptEntryPoint { get; }

        public bool HasCSharp => CSharpPlugin != null;

        public bool HasJavaScript => !string.IsNullOrEmpty(JavaScriptEntryPoint);

        public PluginLoadResult(PluginManifest manifest, PluginBase? csharpPlugin, string? javascriptEntryPoint)
        {
            Manifest = manifest;
            CSharpPlugin = csharpPlugin;
            JavaScriptEntryPoint = javascriptEntryPoint;
        }
    }
}
