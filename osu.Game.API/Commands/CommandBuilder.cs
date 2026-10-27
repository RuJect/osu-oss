// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.API.Commands
{
    /// <summary>
    /// Declarative, strongly-typed command registration.
    /// </summary>
    public sealed class CommandBuilder
    {
        private readonly CommandRegistry registry;
        private readonly string name;
        private readonly List<CommandArgument> arguments = new List<CommandArgument>();
        private readonly List<string> aliases = new List<string>();

        private string? description;

        internal CommandBuilder(CommandRegistry registry, string name)
        {
            this.registry = registry;
            this.name = name;
        }

        public CommandBuilder Description(string description)
        {
            this.description = description;
            return this;
        }

        public CommandBuilder Aliases(params string[] aliases)
        {
            this.aliases.AddRange(aliases);
            return this;
        }

        public CommandBuilder Argument<T>(string name, Action<CommandArgumentBuilder<T>>? configure = null)
        {
            var builder = new CommandArgumentBuilder<T>(name);
            configure?.Invoke(builder);
            arguments.Add(builder.Build());
            return this;
        }

        public IDisposable Handler(Action handler)
        {
            if (arguments.Count != 0)
                throw new InvalidOperationException($"Command '{name}' declares {arguments.Count} argument(s) but its handler accepts none.");

            return register(_ => handler());
        }

        public IDisposable Handler<T1>(Action<T1> handler)
        {
            validateHandler(typeof(T1));
            return register(invocation => handler(invocation.Get<T1>(arguments[0].Name)));
        }

        public IDisposable Handler<T1, T2>(Action<T1, T2> handler)
        {
            validateHandler(typeof(T1), typeof(T2));
            return register(invocation => handler(invocation.Get<T1>(arguments[0].Name), invocation.Get<T2>(arguments[1].Name)));
        }

        public IDisposable Handler<T1, T2, T3>(Action<T1, T2, T3> handler)
        {
            validateHandler(typeof(T1), typeof(T2), typeof(T3));
            return register(invocation => handler(invocation.Get<T1>(arguments[0].Name), invocation.Get<T2>(arguments[1].Name),
                invocation.Get<T3>(arguments[2].Name)));
        }

        public IDisposable Handler<T1, T2, T3, T4>(Action<T1, T2, T3, T4> handler)
        {
            validateHandler(typeof(T1), typeof(T2), typeof(T3), typeof(T4));
            return register(invocation => handler(invocation.Get<T1>(arguments[0].Name), invocation.Get<T2>(arguments[1].Name),
                invocation.Get<T3>(arguments[2].Name), invocation.Get<T4>(arguments[3].Name)));
        }

        /// <summary>
        /// Registers a handler which inspects the <see cref="CommandInvocation"/> directly. Named separately from the
        /// typed overloads so that lambda parameters are never inferred ambiguously.
        /// </summary>
        public IDisposable HandlerRaw(Action<CommandInvocation> handler) => register(handler);

        public CommandDefinition Build(Action<CommandInvocation> handler)
            => new CommandDefinition(name, handler, arguments, description, aliases.ToList());

        private IDisposable register(Action<CommandInvocation> handler)
            => registry.Register(Build(handler));

        private void validateHandler(params Type[] parameterTypes)
        {
            if (parameterTypes.Length != arguments.Count)
                throw new InvalidOperationException($"Command '{name}' declares {arguments.Count} argument(s) but its handler accepts {parameterTypes.Length}.");

            for (int i = 0; i < parameterTypes.Length; i++)
            {
                Type parameter = parameterTypes[i];
                Type argument = arguments[i].ValueType;
                Type? underlying = Nullable.GetUnderlyingType(argument);

                if (!parameter.IsAssignableFrom(argument)
                    && (underlying == null || (!parameter.IsAssignableFrom(underlying) && parameter != underlying)))
                    throw new InvalidOperationException($"Handler parameter {i + 1} of command '{name}' is a {parameter.Name}, but argument '{arguments[i].Name}' is a {argument.Name}.");
            }
        }
    }
}
