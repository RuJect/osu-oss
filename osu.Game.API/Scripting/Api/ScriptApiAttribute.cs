// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.API.Scripting.Api
{
    /// <summary>
    /// Marks a provider member for automatic export to the script's global <c>osu</c> object (or a named
    /// sub-object). Methods become callable functions; properties become read-only values.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
    public sealed class ScriptApiAttribute : Attribute
    {
        /// <summary>The JavaScript name. Defaults to the member name lower-cased to camelCase.</summary>
        public string? Name { get; init; }

        /// <summary>
        /// The sub-object to attach to. When <see langword="null"/>, the member is attached to <c>osu</c>.
        /// Otherwise it is attached to a sub-object named <see cref="Target"/>, either nested under <c>osu</c>
        /// (e.g. <c>osu.storage</c>) or as a top-level global when <see cref="Global"/> is <see langword="true"/>
        /// (e.g. <c>console</c>).
        /// </summary>
        public string? Target { get; init; }

        /// <summary>Whether <see cref="Target"/> names a top-level global rather than an <c>osu</c> member.</summary>
        public bool Global { get; init; }
    }
}
