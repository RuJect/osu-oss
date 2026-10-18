// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Jint;
using Jint.Native;
using osu.Framework.Platform;

namespace osu.Game.API.Scripting.Api.Providers
{
    /// <summary>
    /// Provides sandboxed file access via <c>osu.storage</c>. All paths are resolved relative to the
    /// dedicated data directory (<c>osu-oss/scripts/data</c>) and may not escape it.
    /// </summary>
    public class StorageApiProvider : IScriptApiProvider
    {
        public void Register(ScriptApiContext context)
        {
            JsObject storage = context.CreateObject();

            context.AddFunction(storage, "read", (_, args) =>
            {
                Storage? store = context.Environment.DataStorage;
                string? path = pathFrom(args, 0);

                if (store != null && path != null && store.Exists(path))
                    return JsValue.FromObject(context.Engine, read(store, path));

                return JsValue.Undefined;
            });

            context.AddFunction(storage, "write", (_, args) =>
            {
                Storage? store = context.Environment.DataStorage;
                string? path = pathFrom(args, 0);

                if (store != null && path != null && args.Length >= 2)
                    write(store, path, args[1].ToString());

                return JsValue.Undefined;
            });

            context.AddFunction(storage, "exists", (_, args) =>
            {
                Storage? store = context.Environment.DataStorage;
                string? path = pathFrom(args, 0);

                return JsValue.FromObject(context.Engine, store != null && path != null && store.Exists(path));
            });

            context.AddFunction(storage, "list", (_, args) =>
            {
                Storage? store = context.Environment.DataStorage;

                if (store == null)
                    return new JsArray(context.Engine);

                string prefix = args.Length >= 1 && args[0].IsString() ? sanitise(args[0].AsString()) ?? string.Empty : string.Empty;
                string[] files = enumerate(store, prefix).ToArray();
                return new JsArray(context.Engine, files.Select(f => (JsValue)f).ToArray());
            });

            context.AddFunction(storage, "delete", (_, args) =>
            {
                Storage? store = context.Environment.DataStorage;
                string? path = pathFrom(args, 0);

                if (store != null && path != null && store.Exists(path))
                    store.Delete(path);

                return JsValue.Undefined;
            });

            context.AddProperty(storage, "directory", context.Environment.DataStorage?.GetFullPath(string.Empty, true) ?? string.Empty);
            context.Api.Set("storage", storage);
        }

        private static string read(Storage store, string path)
        {
            using (Stream stream = store.GetStream(path, FileAccess.Read, FileMode.Open))
            using (var reader = new StreamReader(stream))
                return reader.ReadToEnd();
        }

        private static void write(Storage store, string path, string content)
        {
            store.GetFullPath(path, true);

            using (Stream stream = store.CreateFileSafely(path))
            using (var writer = new StreamWriter(stream))
                writer.Write(content);
        }

        private static string? pathFrom(JsValue[] args, int index)
        {
            if (args.Length <= index || !args[index].IsString())
                return null;

            return sanitise(args[index].AsString());
        }

        /// <summary>
        /// Normalises a script-supplied path and rejects absolute paths or directory traversal.
        /// </summary>
        private static string? sanitise(string path)
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

        private static IEnumerable<string> enumerate(Storage store, string prefix)
        {
            string root = string.IsNullOrEmpty(prefix) ? string.Empty : prefix;

            if (root.Length > 0 && !store.Exists(root))
                yield break;

            foreach (string file in store.GetFiles(root, "*"))
                yield return file;

            foreach (string dir in store.GetDirectories(root))
            {
                foreach (string file in enumerate(store, dir))
                    yield return file;
            }
        }
    }
}
