// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using System.Linq;

namespace osu.Game.API.Scripting
{
    /// <summary>
    /// Resolves <see cref="ScriptDefinition"/>s by name or from files on disk.
    /// </summary>
    public class ScriptLocator
    {
        private readonly ScriptRepository repository;

        public ScriptLocator(ScriptRepository repository)
        {
            this.repository = repository;
        }

        public ScriptDefinition? FindByName(string name)
            => repository.Definitions.FirstOrDefault(d => d.Name == name);

        public ScriptDefinition? LoadFromFile(string fileName)
        {
            ScriptStorage? storage = repository.Storage;

            if (storage == null || !storage.Exists(fileName))
                return null;

            string? source = storage.Read(fileName);

            if (source == null)
                return null;

            return new ScriptDefinition(Path.GetFileNameWithoutExtension(fileName), fileName, source);
        }
    }
}
