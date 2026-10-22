// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Reflection;
using Jint.Native;

namespace osu.Game.API.Scripting.Api
{
    /// <summary>
    /// Base for <see cref="IScriptApiProvider"/>s that exports <see cref="ScriptApiAttribute"/>-marked methods
    /// and properties automatically, removing the need for manual <see cref="ScriptApiContext.AddFunction"/> wiring.
    /// </summary>
    public abstract class ScriptApiProvider : IScriptApiProvider
    {
        private const BindingFlags member_flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        /// <summary>The context for the script instance currently being registered.</summary>
        protected ScriptApiContext Context { get; private set; } = null!;

        public void Register(ScriptApiContext context)
        {
            Context = context;

            Type type = GetType();

            foreach (MethodInfo method in type.GetMethods(member_flags))
            {
                ScriptApiAttribute? attribute = method.GetCustomAttribute<ScriptApiAttribute>();
                if (attribute == null)
                    continue;

                JsObject target = context.GetObject(attribute.Target, attribute.Global);
                context.AddFunction(target, attribute.Name ?? toCamelCase(method.Name), (_, args) => invoke(method, args));
            }

            foreach (PropertyInfo property in type.GetProperties(member_flags))
            {
                ScriptApiAttribute? attribute = property.GetCustomAttribute<ScriptApiAttribute>();
                if (attribute == null)
                    continue;

                JsObject target = context.GetObject(attribute.Target, attribute.Global);
                context.AddProperty(target, attribute.Name ?? toCamelCase(property.Name), property.GetValue(this));
            }

            OnRegistered(context);
        }

        /// <summary>
        /// Invoked after automatic registration, allowing a provider to attach extra wiring such as aliases.
        /// </summary>
        protected virtual void OnRegistered(ScriptApiContext context)
        {
        }

        private JsValue invoke(MethodInfo method, JsValue[] args)
        {
            object?[] converted = ScriptApiBinder.ConvertArguments(method, args);

            try
            {
                object? result = method.Invoke(this, converted);
                return ScriptApiBinder.ConvertResult(Context.Engine, method.ReturnType, result);
            }
            catch (TargetInvocationException e) when (e.InnerException != null)
            {
                // Unwrap so the caller's error handler reports the real message.
                throw e.InnerException;
            }
        }

        private static string toCamelCase(string name)
            => name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];
    }
}
