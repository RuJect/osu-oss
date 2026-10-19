// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using osu.Game.API.Commands.Syntax;

namespace osu.Game.API.Commands
{
    /// <summary>
    /// Converts the syntax-only value graph produced by <see cref="CommandValueParser"/> into the CLR types declared
    /// by command arguments. The converter is intentionally independent from command definitions so it can be reused
    /// and tested in isolation.
    /// </summary>
    public static class CommandValueConverter
    {
        private static readonly ConcurrentDictionary<Type, IReadOnlyList<CommandObjectMember>> object_members = new ConcurrentDictionary<Type, IReadOnlyList<CommandObjectMember>>();

        public static bool TryConvert(CommandValueNode node, Type targetType, out object? value, out string? error)
        {
            value = null;
            error = null;

            if (!tryConvert(node, targetType, out value, out error))
                return false;

            return true;
        }

        private static bool tryConvert(CommandValueNode node, Type targetType, out object? value, out string? error)
        {
            value = null;
            error = null;

            if (node is CommandScalarNode scalar && scalar.Kind == CommandScalarKind.Null)
            {
                if (targetType == typeof(object) || !targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null)
                    return true;

                error = "a value is required";
                return false;
            }

            Type? nullable = Nullable.GetUnderlyingType(targetType);

            if (nullable != null)
                return tryConvert(node, nullable, out value, out error);

            if (targetType == typeof(object))
            {
                value = toPlainValue(node);
                return true;
            }

            if (targetType == typeof(string))
            {
                if (node is not CommandScalarNode text)
                {
                    error = "expected a string";
                    return false;
                }

                value = text.Text;
                return true;
            }

            if (targetType == typeof(char))
            {
                if (node is not CommandScalarNode text || text.Text.Length != 1)
                {
                    error = "expected a single character";
                    return false;
                }

                value = text.Text[0];
                return true;
            }

            if (targetType == typeof(bool))
            {
                if (node is not CommandScalarNode text)
                {
                    error = "expected a bool";
                    return false;
                }

                switch (text.Text.ToLowerInvariant())
                {
                    case "true":
                    case "t":
                    case "+":
                        value = true;
                        return true;

                    case "false":
                    case "f":
                    case "-":
                        value = false;
                        return true;

                    default:
                        error = $"'{text.Text}' is not a bool";
                        return false;
                }
            }

            if (targetType.IsEnum)
            {
                if (node is not CommandScalarNode text)
                {
                    error = "expected an enum value";
                    return false;
                }

                foreach (string name in Enum.GetNames(targetType))
                {
                    if (string.Equals(name, text.Text, StringComparison.OrdinalIgnoreCase))
                    {
                        value = Enum.Parse(targetType, name);
                        return true;
                    }
                }

                error = $"'{text.Text}' is not one of {string.Join(", ", Enum.GetNames(targetType))}";
                return false;
            }

            if (targetType.IsArray)
            {
                if (!tryConvertItems(node, targetType.GetElementType()!, out List<object?> items, out error))
                    return false;

                Array array = Array.CreateInstance(targetType.GetElementType()!, items.Count);

                for (int i = 0; i < items.Count; i++)
                    array.SetValue(items[i], i);

                value = array;
                return true;
            }

            if (tryGetCollectionElementType(targetType, out Type? elementType))
            {
                if (!tryConvertItems(node, elementType!, out List<object?> items, out error))
                    return false;

                Type listType = typeof(List<>).MakeGenericType(elementType!);
                IList list = (IList)Activator.CreateInstance(listType)!;

                foreach (object? item in items)
                    list.Add(item);

                if (targetType.IsAssignableFrom(listType))
                {
                    value = list;
                    return true;
                }

                Type setType = typeof(HashSet<>).MakeGenericType(elementType!);
                value = Activator.CreateInstance(setType, list)!;
                return true;
            }

            if (tryGetDictionaryValueType(targetType, out Type? dictionaryValueType, out _))
            {
                if (node is not CommandObjectNode obj)
                {
                    error = "expected an object";
                    return false;
                }

                Type dictionaryType = typeof(Dictionary<,>).MakeGenericType(typeof(string), dictionaryValueType!);
                IDictionary dictionary = (IDictionary)Activator.CreateInstance(dictionaryType)!;

                foreach (var field in obj.Fields)
                {
                    if (!tryConvert(field.Value, dictionaryValueType!, out object? fieldValue, out string? fieldError))
                    {
                        error = $"{field.Key}: {fieldError}";
                        return false;
                    }

                    dictionary[field.Key] = fieldValue;
                }

                value = dictionary;
                return true;
            }

            if (targetType.IsClass || (targetType.IsValueType && !targetType.IsPrimitive))
                return tryConvertObject(node, targetType, out value, out error);

            if (node is not CommandScalarNode number || number.Kind != CommandScalarKind.Number)
            {
                error = $"expected {friendlyTypeName(targetType)}";
                return false;
            }

            try
            {
                value = Convert.ChangeType(number.Literal, targetType, CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception e) when (e is FormatException or InvalidCastException or OverflowException)
            {
                error = $"'{number.Text}' is not a valid {friendlyTypeName(targetType)}";
                return false;
            }
        }

        private static bool tryConvertItems(CommandValueNode node, Type elementType, out List<object?> items, out string? error)
        {
            items = new List<object?>();
            error = null;

            if (node is not CommandArrayNode array)
            {
                error = "expected an array";
                return false;
            }

            foreach (var item in array.Items)
            {
                if (!tryConvert(item, elementType, out object? converted, out string? itemError))
                {
                    error = itemError;
                    return false;
                }

                items.Add(converted);
            }

            return true;
        }

        private static bool tryConvertObject(CommandValueNode node, Type targetType, out object? value, out string? error)
        {
            value = null;
            error = null;

            if (node is not CommandObjectNode obj)
            {
                error = $"expected an object for {friendlyTypeName(targetType)}";
                return false;
            }

            var members = object_members.GetOrAdd(targetType, buildMembers);

            object? instance;

            try
            {
                instance = Activator.CreateInstance(targetType, true);
            }
            catch (Exception)
            {
                error = $"{friendlyTypeName(targetType)} has no parameterless constructor";
                return false;
            }

            if (instance == null)
            {
                error = $"could not create {friendlyTypeName(targetType)}";
                return false;
            }

            var usedFields = new HashSet<CommandObjectField>();

            for (int i = 0; i < members.Count; i++)
            {
                var member = members[i];

                var field = obj.Fields.FirstOrDefault(f => !usedFields.Contains(f) && member.Matches(f.Key));

                if (field != null)
                {
                    usedFields.Add(field);

                    if (!tryConvert(field.Value, member.Type, out object? memberValue, out string? memberError))
                    {
                        error = $"{member.Name}: {memberError}";
                        return false;
                    }

                    member.SetValue(instance, memberValue);
                }
                else if (!member.IsOptional)
                {
                    error = $"missing required field '{member.Name}'";
                    return false;
                }
                else
                    member.SetValue(instance, member.DefaultValue ?? defaultValue(member.Type));
            }

            var unknown = obj.Fields.FirstOrDefault(f => !usedFields.Contains(f));

            if (unknown != null)
            {
                error = $"unknown field '{unknown.Key}'";
                return false;
            }

            value = instance;
            return true;
        }

        private static object? defaultValue(Type type)
            => type.IsValueType ? Activator.CreateInstance(type) : null;

        private static object? toPlainValue(CommandValueNode node)
        {
            switch (node)
            {
                case CommandScalarNode scalar:
                    return scalar.Kind == CommandScalarKind.String ? scalar.Text : scalar.Literal;

                case CommandArrayNode array:
                    return array.Items.Select(toPlainValue).ToList();

                case CommandObjectNode obj:
                {
                    var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

                    foreach (var field in obj.Fields)
                        result[field.Key] = toPlainValue(field.Value);

                    return result;
                }

                default:
                    return null;
            }
        }

        private static bool tryGetCollectionElementType(Type type, out Type? elementType)
        {
            elementType = null;

            if (!type.IsGenericType)
                return false;

            Type definition = type.GetGenericTypeDefinition();

            if (definition != typeof(List<>)
                && definition != typeof(IList<>)
                && definition != typeof(IReadOnlyList<>)
                && definition != typeof(IEnumerable<>)
                && definition != typeof(ICollection<>)
                && definition != typeof(IReadOnlyCollection<>)
                && definition != typeof(HashSet<>)
                && definition != typeof(ISet<>)
                && definition != typeof(IReadOnlySet<>))
                return false;

            elementType = type.GetGenericArguments()[0];
            return true;
        }

        private static bool tryGetDictionaryValueType(Type type, out Type? valueType, out bool readOnly)
        {
            valueType = null;
            readOnly = false;

            if (!type.IsGenericType)
                return false;

            Type definition = type.GetGenericTypeDefinition();

            if (definition != typeof(Dictionary<,>) && definition != typeof(IDictionary<,>) && definition != typeof(IReadOnlyDictionary<,>))
                return false;

            Type[] arguments = type.GetGenericArguments();

            if (arguments[0] != typeof(string))
                return false;

            valueType = arguments[1];
            readOnly = definition == typeof(IReadOnlyDictionary<,>);
            return true;
        }

        /// <summary>Returns the fields of an object argument type which can be suggested as keys.</summary>
        internal static IReadOnlyList<CommandSuggestion> GetObjectFieldSuggestions(Type type)
        {
            if (!CommandBinder.isObjectLike(type) || tryGetDictionaryValueType(type, out _, out _))
                return Array.Empty<CommandSuggestion>();

            return object_members.GetOrAdd(type, buildMembers)
                                 .Select(member => new CommandSuggestion(member.Name, member.Name, member.Description))
                                 .ToList();
        }

        /// <summary>Returns the declared type of an object argument field, when known.</summary>
        internal static Type? GetObjectFieldType(Type type, string? name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            var member = object_members.GetOrAdd(type, buildMembers).FirstOrDefault(m => m.Matches(name));

            return member?.Type;
        }

        /// <summary>Returns the value type of a string-keyed dictionary argument, when applicable.</summary>
        internal static Type? GetDictionaryValueType(Type type)
            => tryGetDictionaryValueType(type, out Type? valueType, out _) ? valueType : null;

        private static IReadOnlyList<CommandObjectMember> buildMembers(Type type)
        {
            var result = new List<CommandObjectMember>();

            IEnumerable<MemberInfo> members = type
                                              .GetMembers(BindingFlags.Public | BindingFlags.Instance)
                                              .Where(m => m is PropertyInfo { CanRead: true, CanWrite: true } || m is FieldInfo { IsInitOnly: false })
                                              .OrderBy(m => m.MetadataToken);

            foreach (var member in members)
            {
                Type memberType = member switch
                {
                    PropertyInfo property => property.PropertyType,
                    FieldInfo field => field.FieldType,
                    _ => throw new InvalidOperationException(),
                };

                var attribute = member.GetCustomAttribute<CommandParameterAttribute>();

                result.Add(new CommandObjectMember(
                    member,
                    attribute?.Name ?? camelCase(member.Name),
                    attribute?.Description,
                    attribute?.Aliases ?? Array.Empty<string>(),
                    memberType,
                    attribute?.Optional == true || Nullable.GetUnderlyingType(memberType) != null,
                    attribute?.Default));
            }

            return result;
        }

        private static string camelCase(string name)
        {
            if (string.IsNullOrEmpty(name) || char.IsLower(name[0]))
                return name;

            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        private static string friendlyTypeName(Type type)
        {
            if (type == typeof(int) || type == typeof(long) || type == typeof(short))
                return "an integer";

            if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
                return "a number";

            return type.Name;
        }

        private sealed class CommandObjectMember
        {
            private readonly MemberInfo member;

            public string Name { get; }

            public string? Description { get; }

            public IReadOnlyList<string> Aliases { get; }

            public Type Type { get; }

            public bool IsOptional { get; }

            public object? DefaultValue { get; }

            public CommandObjectMember(MemberInfo member, string name, string? description, IReadOnlyList<string> aliases, Type type,
                                       bool isOptional, object? defaultValue)
            {
                this.member = member;
                Name = name;
                Description = description;
                Aliases = aliases;
                Type = type;
                IsOptional = isOptional;
                DefaultValue = defaultValue;
            }

            public bool Matches(string key)
                => string.Equals(Name, key, StringComparison.OrdinalIgnoreCase)
                   || Aliases.Any(a => string.Equals(a, key, StringComparison.OrdinalIgnoreCase));

            public void SetValue(object instance, object? value)
            {
                switch (member)
                {
                    case PropertyInfo property:
                        property.SetValue(instance, value);
                        break;

                    case FieldInfo field:
                        field.SetValue(instance, value);
                        break;
                }
            }
        }
    }
}
