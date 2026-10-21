// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json.Linq;
using osu.Framework.Graphics;
using osu.Game.Overlays;

namespace osu.Game.Theming
{
    /// <summary>
    /// An immutable, fully resolved theme, ready to be consumed by the game.
    /// </summary>
    public sealed class ThemeSnapshot
    {
        public const int DEFAULT_CORNER_RADIUS = 10;
        public const int DEFAULT_GLASS_BLUR = 10;

        public string Id { get; }
        public string Name { get; }
        public string Author { get; }
        public string Description { get; }

        public bool IsBuiltIn { get; }

        public IReadOnlyDictionary<string, Colour4> Colours { get; }
        public IReadOnlyList<Colour4> PreviewColours { get; }

        public float CornerRadius { get; }
        public bool FlatLayout { get; }
        public bool GlassEnabled { get; }
        public int GlassBlur { get; }

        private readonly Dictionary<string, string> colourExpressions;
        private readonly Dictionary<string, float> surfaceAlpha;
        private readonly float surfaceAlphaMultiplier;

        private readonly Dictionary<OverlayColourScheme, int> overlaySchemeHues = new Dictionary<OverlayColourScheme, int>();
        private readonly Dictionary<string, Colour4> overlaySlots = new Dictionary<string, Colour4>();
        private readonly Dictionary<OverlayColourScheme, Dictionary<string, Colour4>> overlaySchemeSlots = new Dictionary<OverlayColourScheme, Dictionary<string, Colour4>>();
        private int? overlayHue;
        private float overlaySaturationMultiplier = 1;
        private float overlayLightnessMultiplier = 1;

        internal ThemeSnapshot(string id, JObject json, bool isBuiltIn)
        {
            Id = id;
            IsBuiltIn = isBuiltIn;

            Name = json.Value<string>("name") ?? id;
            Author = json.Value<string>("author") ?? string.Empty;
            Description = json.Value<string>("description") ?? string.Empty;

            colourExpressions = ThemeJsonExtensions.readStringDictionary(json["colors"] as JObject);

            var resolvedColours = new Dictionary<string, Colour4>(StringComparer.OrdinalIgnoreCase);

            foreach (var pair in colourExpressions)
                resolvedColours[pair.Key] = ThemeColourExpression.Resolve(pair.Value, colourExpressions);

            var preview = json["preview"] as JArray;
            var previewColours = preview?.Values<string>()
                                        .Where(s => !string.IsNullOrWhiteSpace(s))
                                        .Select(s => ThemeColourExpression.Resolve(s!, colourExpressions))
                                        .ToArray()
                                 ?? resolvedColours.Values.Take(8).ToArray();

            var structure = json["structure"] as JObject;
            CornerRadius = structure?.Value<float?>("cornerRadius") ?? DEFAULT_CORNER_RADIUS;
            FlatLayout = structure?.Value<bool?>("flatLayout") ?? false;

            var glass = json["glass"] as JObject;
            GlassEnabled = glass?.Value<bool?>("enabled") ?? false;
            GlassBlur = glass?.Value<int?>("blur") ?? DEFAULT_GLASS_BLUR;

            var surfaces = json["surfaces"] as JObject;
            surfaceAlpha = ThemeJsonExtensions.readFloatDictionary(surfaces);
            surfaceAlphaMultiplier = surfaces?.Value<float?>("alphaMultiplier") ?? 1;

            readOverlay(json["overlay"] as JObject);

            Colours = resolvedColours;
            PreviewColours = previewColours;
        }

        /// <summary>
        /// Tints <paramref name="colour"/> with the theme's main overlay hue.
        /// Greys and near-greys remain visually grey because their saturation is unchanged.
        /// </summary>
        public Colour4 ApplyAggressiveTint(Colour4 colour)
        {
            if (!overlayHue.HasValue)
                return colour;

            Vector4 hsl = colour.ToHSL();
            return Colour4.FromHSL(overlayHue.Value / 360f, hsl.Y, hsl.Z).Opacity(colour.A);
        }

        private void readOverlay(JObject? overlay)
        {
            if (overlay == null)
                return;

            overlayHue = overlay.Value<int?>("hue");
            overlaySaturationMultiplier = overlay.Value<float?>("saturationMultiplier") ?? 1;
            overlayLightnessMultiplier = overlay.Value<float?>("lightnessMultiplier") ?? 1;

            readOverlaySlots(overlay["slots"] as JObject, overlaySlots);

            var schemes = overlay["schemes"] as JObject;

            if (schemes == null)
                return;

            foreach (var schemeProperty in schemes.Properties())
            {
                if (!Enum.TryParse(schemeProperty.Name, true, out OverlayColourScheme scheme))
                    throw new ThemeLoadException($"Unknown overlay colour scheme '{schemeProperty.Name}'.");

                var schemeJson = schemeProperty.Value as JObject;

                if (schemeJson == null)
                    continue;

                int? hue = schemeJson.Value<int?>("hue");

                if (hue.HasValue)
                    overlaySchemeHues[scheme] = hue.Value;

                var slots = new Dictionary<string, Colour4>();
                readOverlaySlots(schemeJson["slots"] as JObject, slots);

                if (slots.Count > 0)
                    overlaySchemeSlots[scheme] = slots;
            }
        }

        private void readOverlaySlots(JObject? slots, Dictionary<string, Colour4> target)
        {
            if (slots == null)
                return;

            foreach (var property in slots.Properties())
            {
                if (property.Value.Type != JTokenType.String)
                    throw new ThemeLoadException($"Overlay slot '{property.Name}' must be a colour expression.");

                target[property.Name] = ThemeColourExpression.Resolve(property.Value.Value<string>()!, colourExpressions);
            }
        }

        public bool TryGetColour(string name, out Colour4 colour) => Colours.TryGetValue(name, out colour);

        /// <summary>
        /// Resolves an <see cref="OverlayColourProvider"/> slot for the given scheme.
        /// Falls back to the vanilla HSL generation when the theme does not define an override.
        /// </summary>
        public Colour4 GetOverlayColour(OverlayColourScheme? scheme, int fallbackHue, string slot, float saturation, float lightness)
        {
            if (scheme.HasValue && overlaySchemeSlots.TryGetValue(scheme.Value, out var schemeSlots) && schemeSlots.TryGetValue(slot, out var schemeColour))
                return schemeColour;

            if (overlaySlots.TryGetValue(slot, out var colour))
                return colour;

            int hue = scheme.HasValue && overlaySchemeHues.TryGetValue(scheme.Value, out int schemeHue)
                ? schemeHue
                : overlayHue ?? fallbackHue;

            return Colour4.FromHSL(hue / 360f, Math.Clamp(saturation * overlaySaturationMultiplier, 0, 1), Math.Clamp(lightness * overlayLightnessMultiplier, 0, 1));
        }

        public float GetSurfaceAlpha(string slot, float fallback)
        {
            float value = surfaceAlpha.TryGetValue(slot, out float themed) ? themed : fallback;
            return Math.Clamp(value * surfaceAlphaMultiplier, 0, 1);
        }

        public Colour4 ApplySurfaceAlpha(string slot, Colour4 colour, float fallback = 1)
            => colour.Opacity(colour.A * GetSurfaceAlpha(slot, fallback));

        public bool HasSurface(string slot) => surfaceAlpha.ContainsKey(slot);

        public override string ToString() => Name;
    }

    public static class ThemeJsonExtensions
    {
        internal static Dictionary<string, string> readStringDictionary(JObject? json)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (json == null)
                return result;

            foreach (var property in json.Properties())
            {
                if (property.Value.Type != JTokenType.String)
                    throw new ThemeLoadException($"Theme colour '{property.Name}' must be a string expression.");

                result[property.Name] = property.Value.Value<string>()!;
            }

            return result;
        }

        internal static Dictionary<string, float> readFloatDictionary(JObject? json)
        {
            var result = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

            if (json == null)
                return result;

            foreach (var property in json.Properties())
            {
                if (property.Value.Type != JTokenType.Float && property.Value.Type != JTokenType.Integer)
                    throw new ThemeLoadException($"Theme surface '{property.Name}' must be a number.");

                result[property.Name] = property.Value.Value<float>();
            }

            return result;
        }
    }
}
