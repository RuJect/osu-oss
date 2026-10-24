// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Threading.Tasks;
using Jint;
using Jint.Native;
using Jint.Runtime.Interop;

namespace osu.Game.API.Scripting.Api.Providers
{
    /// <summary>
    /// Provides small convenience utilities on top of the other providers: <c>osu.sleep</c>, <c>osu.each</c>
    /// and a persistent key-value store under <c>osu.kv</c>.
    /// </summary>
    public class UtilityApiProvider : ScriptApiProvider
    {
        private const string kv_prefix = "kv/";

        /// <summary>
        /// Resolves after <paramref name="ms"/> game-time. Awaitable, e.g. <c>await osu.sleep(1000)</c>.
        /// </summary>
        [ScriptApi]
        public Task Sleep(double ms)
        {
            var completion = new TaskCompletionSource<bool>();

            JsValue handler = new ClrFunction(Context.Engine, "sleep", (_, _) =>
            {
                completion.TrySetResult(true);
                return JsValue.Undefined;
            });

            Context.Instance.Scheduler.AddTimer(handler, ms, false);
            return completion.Task;
        }

        /// <summary>
        /// Invokes <paramref name="callback"/> for each element of an array-like <paramref name="collection"/>.
        /// </summary>
        [ScriptApi]
        public void Each(JsValue collection, JsValue callback)
        {
            if (!callback.IsCallable())
                return;

            JsValue forEach = collection.Get("forEach");

            if (forEach.IsCallable())
                Context.Engine.Invoke(forEach, collection, new object[] { callback });
        }

        [ScriptApi(Target = "kv")]
        public JsValue Get(string? key)
        {
            ScriptDataStorage? data = Context.Services.DataStorage;
            string? path = keyPath(key);

            if (data == null || path == null || !data.Exists(path))
                return JsValue.Undefined;

            string? content = data.Read(path);
            return content == null ? JsValue.Undefined : ScriptJson.Parse(Context.Engine, content);
        }

        [ScriptApi(Target = "kv")]
        public void Set(string? key, JsValue value)
        {
            ScriptDataStorage? data = Context.Services.DataStorage;
            string? path = keyPath(key);

            if (data == null || path == null || value.IsUndefined())
                return;

            data.Write(path, ScriptJson.Stringify(Context.Engine, value));
        }

        [ScriptApi(Target = "kv")]
        public void Delete(string? key)
        {
            ScriptDataStorage? data = Context.Services.DataStorage;
            string? path = keyPath(key);

            if (data != null && path != null)
                data.Delete(path);
        }

        [ScriptApi(Target = "kv")]
        public string[] Keys()
        {
            ScriptDataStorage? data = Context.Services.DataStorage;

            if (data == null)
                return Array.Empty<string>();

            return data.List("kv").Select(f => f[kv_prefix.Length..]).ToArray();
        }

        private static string? keyPath(string? key)
        {
            if (string.IsNullOrEmpty(key))
                return null;

            string? path = ScriptFileStore.SanitizePath(key);
            return path == null ? null : kv_prefix + path;
        }
    }
}
