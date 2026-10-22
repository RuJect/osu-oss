// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Jint;
using Jint.Native;

namespace osu.Game.API.Scripting.Api
{
    /// <summary>
    /// JSON helpers backed by the engine's own JSON implementation, so script values round-trip consistently.
    /// </summary>
    public static class ScriptJson
    {
        public static JsValue Parse(Engine engine, string text)
        {
            JsValue json = engine.GetValue("JSON");
            return engine.Invoke(json.Get("parse"), new object[] { text });
        }

        public static string Stringify(Engine engine, JsValue value)
        {
            JsValue json = engine.GetValue("JSON");
            return engine.Invoke(json.Get("stringify"), new object[] { value, JsValue.Null, 2 }).AsString();
        }
    }
}
