// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Jint;
using Jint.Native;

namespace osu.Game.API.Scripting.Api.Providers
{
    /// <summary>
    /// Converts between CLR byte arrays and the plain JavaScript arrays exposed to scripts.
    /// </summary>
    internal static class ScriptBinary
    {
        public static JsValue ToJs(Engine engine, byte[] data)
        {
            var values = new JsValue[data.Length];

            for (int i = 0; i < data.Length; i++)
                values[i] = JsValue.FromObject(engine, (double)data[i]);

            return new JsArray(engine, values);
        }

        public static byte[]? FromJs(JsValue value)
        {
            if (!value.IsArray())
                return null;

            JsArray array = value.AsArray();
            byte[] result = new byte[array.Length];

            for (uint i = 0; i < array.Length; i++)
            {
                JsValue item = value.Get(i.ToString());

                if (!item.IsNumber())
                    return null;

                double number = item.AsNumber();

                if (double.IsNaN(number))
                    return null;

                result[i] = (byte)Math.Clamp(number, 0d, 255d);
            }

            return result;
        }
    }
}
