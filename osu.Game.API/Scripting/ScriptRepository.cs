// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Game.API.Services;

namespace osu.Game.API.Scripting
{
    /// <summary>
    /// Owns the known <see cref="ScriptDefinition"/>s and their persistence under the scripts directory.
    /// </summary>
    public class ScriptRepository
    {
        private readonly OutputLog output;
        private readonly List<ScriptDefinition> definitions = new List<ScriptDefinition>();

        private int anonymousCounter;

        public event Action? DefinitionsChanged;

        /// <summary>Storage scoped to the scripts directory, once the game has loaded.</summary>
        public ScriptStorage? Storage { get; set; }

        public IReadOnlyList<ScriptDefinition> Definitions => definitions;

        public ScriptRepository(OutputLog output)
        {
            this.output = output;
        }

        /// <summary>
        /// Re-reads the definitions from disk, preserving any anonymous in-memory scripts.
        /// </summary>
        public void Refresh()
        {
            if (Storage == null)
                return;

            List<ScriptDefinition> anonymous = definitions.Where(d => !d.IsFile).ToList();

            definitions.Clear();

            foreach (string file in Storage.EnumerateScripts())
            {
                string? source = Storage.Read(file);

                if (source == null)
                    continue;

                string name = Path.GetFileNameWithoutExtension(file);
                definitions.Add(new ScriptDefinition(name, file, source));
            }

            definitions.AddRange(anonymous);
            DefinitionsChanged?.Invoke();
        }

        public ScriptDefinition CreateAnonymous(string source = "")
        {
            var definition = new ScriptDefinition($"anonymous-{++anonymousCounter}", null, source);
            definitions.Add(definition);
            DefinitionsChanged?.Invoke();
            return definition;
        }

        public void Remove(ScriptDefinition definition)
        {
            definitions.Remove(definition);
            DefinitionsChanged?.Invoke();
        }

        public void Save(ScriptDefinition definition)
        {
            if (Storage == null || definition.FileName == null)
                return;

            Storage.Write(definition.FileName, definition.Source);
            output.Log(OutputLevel.Info, definition.Name, "saved");
        }

        /// <summary>
        /// Persists an anonymous definition to disk under a generated file name.
        /// Returns the file name written, or <c>null</c> if nothing was saved.
        /// </summary>
        public string? SaveAsFile(ScriptDefinition definition)
        {
            if (Storage == null || definition.FileName != null)
                return null;

            string fileName = makeUniqueFileName(definition.Name);

            if (!fileName.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
                fileName += ".js";

            Storage.Write(fileName, definition.Source);
            output.Log(OutputLevel.Info, definition.Name, $"saved as {fileName}");
            Refresh();
            return fileName;
        }

        public void Reload(ScriptDefinition definition)
        {
            if (Storage == null || definition.FileName == null || !Storage.Exists(definition.FileName))
                return;

            string? source = Storage.Read(definition.FileName);

            if (source == null)
                return;

            definition.Source = source;
            DefinitionsChanged?.Invoke();
        }

        public string[] GetNames() => definitions.Select(d => d.Name).ToArray();

        private string makeUniqueFileName(string baseName)
        {
            var used = new HashSet<string>(Storage!.EnumerateScripts(), StringComparer.OrdinalIgnoreCase);
            string name = baseName;
            int suffix = 1;

            while (used.Contains(name + ".js"))
                name = $"{baseName}-{suffix++}";

            return name;
        }
    }
}
