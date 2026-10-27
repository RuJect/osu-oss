// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.API.Plugins
{
    /// <summary>Non-generic access to a plugin's typed configuration.</summary>
    public interface IPluginConfig : IDisposable
    {
        /// <summary>The enum type used as the configuration lookup.</summary>
        Type SettingType { get; }

        /// <summary>The distinct sections in enum declaration order.</summary>
        IReadOnlyList<string> Sections { get; }

        /// <summary>Returns the setting names in <paramref name="section"/>, or all settings when <c>null</c>.</summary>
        IReadOnlyList<string> Keys(string? section = null);

        /// <summary>Returns the current value for <paramref name="key"/>, or <c>null</c> when unknown.</summary>
        object? GetValue(string key);

        /// <summary>Sets <paramref name="key"/> from an invariant-formatted value. A <c>null</c> value resets it.</summary>
        void SetValue(string key, object? value);

        /// <summary>Resets <paramref name="key"/> to its registered default.</summary>
        void Reset(string key);
    }
}
