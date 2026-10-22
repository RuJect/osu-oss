// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Jint;
using Jint.Native;

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
                ScriptDataStorage? data = context.Services.DataStorage;
                string? path = pathFrom(args, 0);

                if (data == null || path == null || !data.Exists(path))
                    return JsValue.Undefined;

                return JsValue.FromObject(context.Engine, data.Read(path));
            });

            context.AddFunction(storage, "write", (_, args) =>
            {
                ScriptDataStorage? data = context.Services.DataStorage;
                string? path = pathFrom(args, 0);

                if (data != null && path != null && args.Length >= 2)
                    data.Write(path, args[1].ToString());

                return JsValue.Undefined;
            });

            context.AddFunction(storage, "append", (_, args) =>
            {
                ScriptDataStorage? data = context.Services.DataStorage;
                string? path = pathFrom(args, 0);

                if (data != null && path != null && args.Length >= 2)
                    data.Append(path, args[1].ToString());

                return JsValue.Undefined;
            });

            context.AddFunction(storage, "exists", (_, args) =>
            {
                ScriptDataStorage? data = context.Services.DataStorage;
                string? path = pathFrom(args, 0);

                return JsValue.FromObject(context.Engine, data != null && path != null && data.Exists(path));
            });

            context.AddFunction(storage, "list", (_, args) =>
            {
                ScriptDataStorage? data = context.Services.DataStorage;

                if (data == null)
                    return new JsArray(context.Engine);

                string prefix = args.Length >= 1 && args[0].IsString() ? ScriptFileStore.SanitizePath(args[0].AsString()) ?? string.Empty : string.Empty;
                string[] files = data.List(prefix).ToArray();
                return new JsArray(context.Engine, files.Select(f => (JsValue)f).ToArray());
            });

            context.AddFunction(storage, "delete", (_, args) =>
            {
                ScriptDataStorage? data = context.Services.DataStorage;
                string? path = pathFrom(args, 0);

                if (data != null && path != null)
                    data.Delete(path);

                return JsValue.Undefined;
            });

            context.AddProperty(storage, "directory", context.Services.DataStorage?.Directory ?? string.Empty);
            context.Api.Set("storage", storage);
        }

        private static string? pathFrom(JsValue[] args, int index)
        {
            if (args.Length <= index || !args[index].IsString())
                return null;

            return ScriptFileStore.SanitizePath(args[index].AsString());
        }
    }
}
