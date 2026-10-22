// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Reflection;
using Jint;
using Jint.Native;

namespace osu.Game.API.Scripting.Api
{
    /// <summary>
    /// Converts between script arguments/results and CLR values for <see cref="ScriptApiProvider"/>.
    /// </summary>
    public static class ScriptApiBinder
    {
        /// <summary>Converts raw script arguments to the parameter types expected by <paramref name="method"/>.</summary>
        public static object?[] ConvertArguments(MethodInfo method, JsValue[] args)
        {
            ParameterInfo[] parameters = method.GetParameters();
            object?[] converted = new object?[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo parameter = parameters[i];

                // A JsValue[] parameter captures the raw argument array (callables, rest arguments, ...).
                if (parameter.ParameterType == typeof(JsValue[]))
                {
                    // The runtime may hand us the arguments as an object[] (array covariance), so reify
                    // a concrete JsValue[] to keep MethodInfo.Invoke's parameter binding happy.
                    JsValue[] values = new JsValue[args.Length];
                    for (int j = 0; j < args.Length; j++)
                        values[j] = args[j];

                    converted[i] = values;
                    continue;
                }

                JsValue value = i < args.Length ? args[i] : JsValue.Undefined;
                converted[i] = ConvertArgument(value, parameter.ParameterType);
            }

            return converted;
        }

        /// <summary>Converts a single script argument to the given CLR <paramref name="type"/>.</summary>
        public static object? ConvertArgument(JsValue value, Type type)
        {
            if (type == typeof(JsValue))
                return value;

            if (type == typeof(string))
                return value.IsString() ? value.AsString() : null;

            if (type == typeof(bool))
                return value.IsBoolean() ? value.AsBoolean() : false;

            if (type == typeof(double))
                return value.IsNumber() ? value.AsNumber() : 0d;

            if (type == typeof(int))
                return value.IsNumber() ? (int)value.AsNumber() : 0;

            if (type == typeof(long))
                return value.IsNumber() ? (long)value.AsNumber() : 0L;

            return value.ToObject();
        }

        /// <summary>Converts a CLR return value back into a <see cref="JsValue"/>.</summary>
        public static JsValue ConvertResult(Engine engine, Type returnType, object? result)
        {
            if (returnType == typeof(void))
                return JsValue.Undefined;

            if (result is JsValue jsValue)
                return jsValue;

            if (result == null)
                return JsValue.Undefined;

            return JsValue.FromObject(engine, result);
        }
    }
}
