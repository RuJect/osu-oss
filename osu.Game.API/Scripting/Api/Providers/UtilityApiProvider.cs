// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading.Tasks;
using Jint;
using Jint.Native;
using osu.Game.API.Services;

namespace osu.Game.API.Scripting.Api.Providers
{
    /// <summary>
    /// Provides small convenience utilities on top of the other providers: <c>osu.sleep</c>, <c>osu.each</c>
    /// and a persistent key-value store under <c>osu.kv</c>.
    /// </summary>
    public class UtilityApiProvider : ScriptApiProvider
    {
        /// <summary>
        /// Resolves after <paramref name="ms"/> game-time. Awaitable, e.g. <c>await osu.sleep(1000)</c>.
        /// </summary>
        [ScriptApi]
        public Task Sleep(double ms)
        {
            var completion = new TaskCompletionSource<bool>();
            Context.Instance.Scheduler.AddTimer(() => completion.TrySetResult(true), ms, false);
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
            StorageService? data = Context.Plugin.Services.Storage;

            if (data == null || key == null)
                return JsValue.Undefined;

            string? content = data.KvRead(key);
            return content == null ? JsValue.Undefined : ScriptJson.Parse(Context.Engine, content);
        }

        [ScriptApi(Target = "kv")]
        public void Set(string? key, JsValue value)
        {
            StorageService? data = Context.Plugin.Services.Storage;

            if (data == null || key == null || value.IsUndefined())
                return;

            data.KvWrite(key, ScriptJson.Stringify(Context.Engine, value));
        }

        [ScriptApi(Target = "kv")]
        public void Delete(string? key)
        {
            StorageService? data = Context.Plugin.Services.Storage;

            if (data != null && key != null)
                data.KvDelete(key);
        }

        [ScriptApi(Target = "kv")]
        public string[] Keys() => Context.Plugin.Services.Storage?.KvKeys() ?? Array.Empty<string>();
    }
}
