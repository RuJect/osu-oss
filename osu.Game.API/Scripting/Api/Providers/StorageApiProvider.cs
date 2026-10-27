// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using Jint;
using Jint.Native;
using osu.Game.API.Services;

namespace osu.Game.API.Scripting.Api.Providers
{
    /// <summary>
    /// Provides sandboxed file access via <c>osu.storage</c>. All paths are resolved relative to the
    /// dedicated data directory (<c>osu-oss/scripts/data</c>) and may not escape it.
    /// </summary>
    public class StorageApiProvider : ScriptApiProvider
    {
        [ScriptApi(Target = "storage")]
        public string? Read(string? path)
        {
            StorageService? data = Context.Plugin.Services.Storage;
            return data != null && path != null && data.Exists(path) ? data.Read(path) : null;
        }

        [ScriptApi(Target = "storage")]
        public void Write(string? path, string? content)
        {
            StorageService? data = Context.Plugin.Services.Storage;

            if (data != null && path != null && content != null)
                data.Write(path, content);
        }

        [ScriptApi(Target = "storage")]
        public void Append(string? path, string? content)
        {
            StorageService? data = Context.Plugin.Services.Storage;

            if (data != null && path != null && content != null)
                data.Append(path, content);
        }

        [ScriptApi(Target = "storage")]
        public bool Exists(string? path)
        {
            StorageService? data = Context.Plugin.Services.Storage;
            return data != null && path != null && data.Exists(path);
        }

        [ScriptApi(Target = "storage")]
        public string[] List(string? path)
        {
            StorageService? data = Context.Plugin.Services.Storage;

            if (data == null)
                return Array.Empty<string>();

            return data.List(path ?? string.Empty).ToArray();
        }

        [ScriptApi(Target = "storage")]
        public void Delete(string? path)
        {
            StorageService? data = Context.Plugin.Services.Storage;

            if (data != null && path != null)
                data.Delete(path);
        }

        [ScriptApi(Target = "storage")]
        public void Mkdir(string? path)
        {
            StorageService? data = Context.Plugin.Services.Storage;

            if (data != null && path != null)
                data.CreateDirectory(path);
        }

        [ScriptApi(Target = "storage")]
        public JsValue ReadJson(string? path)
        {
            StorageService? data = Context.Plugin.Services.Storage;

            if (data == null || path == null || !data.Exists(path))
                return JsValue.Undefined;

            string? content = data.Read(path);
            return content == null ? JsValue.Undefined : ScriptJson.Parse(Context.Engine, content);
        }

        [ScriptApi(Target = "storage")]
        public void WriteJson(string? path, JsValue value)
        {
            StorageService? data = Context.Plugin.Services.Storage;

            if (data == null || path == null || value.IsUndefined())
                return;

            data.Write(path, ScriptJson.Stringify(Context.Engine, value));
        }

        [ScriptApi(Target = "storage")]
        public string Directory => Context.Plugin.Services.Storage?.Directory ?? string.Empty;
    }
}
