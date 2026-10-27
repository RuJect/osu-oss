// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Jint;
using Jint.Native;
using osu.Game.API.Services;
using osuTK.Input;

namespace osu.Game.API.Scripting.Api.Providers
{
    /// <summary>
    /// Provides synthetic keyboard input and key listeners via <c>osu.keyboard</c>.
    /// </summary>
    public class KeyboardApiProvider : ScriptApiProvider
    {
        private KeyboardService? keyboard;

        private KeyboardService keyboardService => keyboard ??= new KeyboardService(Context.Plugin.Services, Context.Instance.Scheduler);

        /// <summary>Whether every key in <paramref name="keys"/> is currently pressed.</summary>
        [ScriptApi(Target = "keyboard")]
        public bool IsDown(JsValue keys) => keyboardService.IsDown(parseKeys(keys));

        /// <summary>Holds the given key or key combination down until <c>osu.keyboard.up</c>.</summary>
        [ScriptApi(Target = "keyboard")]
        public void Down(JsValue keys) => keyboardService.Down(parseKeys(keys));

        /// <summary>Releases the given key or key combination.</summary>
        [ScriptApi(Target = "keyboard")]
        public void Up(JsValue keys) => keyboardService.Up(parseKeys(keys));

        /// <summary>
        /// Presses and releases the given key or combination after <paramref name="duration"/> milliseconds
        /// (50ms by default). Awaitable; completes when the key is released, including by an early
        /// <c>osu.keyboard.up</c>.
        /// </summary>
        [ScriptApi(Target = "keyboard")]
        public Task Press(JsValue keys, JsValue duration)
            => keyboardService.Press(parseKeys(keys), duration.IsNumber() ? duration.AsNumber() : null);

        /// <summary>Registers a callback invoked with the key name when a key is pressed.</summary>
        [ScriptApi(Target = "keyboard")]
        public void OnPress(JsValue handler)
        {
            if (handler.IsCallable())
                keyboardService.OnPress(key => Context.Engine.Invoke(handler, key.ToString()));
        }

        /// <summary>Registers a callback invoked with the key name when a key is released.</summary>
        [ScriptApi(Target = "keyboard")]
        public void OnRelease(JsValue handler)
        {
            if (handler.IsCallable())
                keyboardService.OnRelease(key => Context.Engine.Invoke(handler, key.ToString()));
        }

        protected override void OnRegistered(ScriptApiContext context)
        {
            context.Instance.OnTearDown(() => keyboard?.ReleaseOwned());
        }

        private static Key[] parseKeys(JsValue value)
        {
            if (value.IsString())
                return KeyParser.Parse(value.AsString());

            if (value.IsArray())
            {
                var names = new List<string>();
                var array = value.AsArray();

                for (uint i = 0; i < array.Length; i++)
                {
                    JsValue item = value.Get(i.ToString());

                    if (!item.IsString())
                        throw new ArgumentException("key array must contain strings");

                    names.Add(item.AsString());
                }

                return KeyParser.Parse(names);
            }

            throw new ArgumentException("key must be a string or an array of strings");
        }
    }
}
