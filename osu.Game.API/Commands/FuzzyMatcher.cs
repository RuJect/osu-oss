// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.API.Commands
{
    /// <summary>
    /// The result of a successful <see cref="FuzzyMatcher.Match"/> call.
    /// </summary>
    public readonly struct FuzzyMatch
    {
        /// <summary>
        /// A relative score. Higher is a better match.
        /// </summary>
        public int Score { get; }

        /// <summary>
        /// The indices into the matched text of each character that was matched, in order.
        /// </summary>
        public IReadOnlyList<int> Indices { get; }

        public FuzzyMatch(int score, IReadOnlyList<int> indices)
        {
            Score = score;
            Indices = indices;
        }
    }

    /// <summary>
    /// A small, dependency-free fuzzy matcher in the style of fzf. A pattern matches a text when all of its
    /// characters appear in the text in order (case-insensitively); matched characters need not be contiguous.
    /// </summary>
    public static class FuzzyMatcher
    {
        /// <summary>
        /// Attempts to match <paramref name="pattern"/> against <paramref name="text"/>.
        /// </summary>
        /// <returns>A <see cref="FuzzyMatch"/> when the pattern is a subsequence of the text, otherwise <c>null</c>.</returns>
        public static FuzzyMatch? Match(string pattern, string text)
        {
            if (string.IsNullOrEmpty(pattern))
                return new FuzzyMatch(0, Array.Empty<int>());

            if (string.IsNullOrEmpty(text) || pattern.Length > text.Length)
                return null;

            var indices = new List<int>(pattern.Length);

            int score = 0;
            int patternIndex = 0;
            int consecutive = 0;
            int previousMatch = -2;

            for (int i = 0; i < text.Length && patternIndex < pattern.Length; i++)
            {
                if (!charEqual(pattern[patternIndex], text[i]))
                {
                    consecutive = 0;
                    continue;
                }

                bool isWordStart = i == 0 || !char.IsLetterOrDigit(text[i - 1]);
                bool isConsecutive = previousMatch == i - 1;

                score += 1;

                if (isConsecutive)
                {
                    consecutive++;
                    score += consecutive * 4;
                }
                else
                    consecutive = 0;

                if (isWordStart)
                    score += 12;

                if (char.IsUpper(text[i]) && char.IsLower(pattern[patternIndex]))
                    score += 1;

                indices.Add(i);
                previousMatch = i;
                patternIndex++;
            }

            if (patternIndex != pattern.Length)
                return null;

            // Prefer shorter targets when scores are otherwise close, and prefer contiguous matches.
            score -= text.Length;
            score -= indices.Count > 0 ? indices[0] : 0;

            return new FuzzyMatch(score, indices);
        }

        private static bool charEqual(char a, char b) => char.ToLowerInvariant(a) == char.ToLowerInvariant(b);
    }
}
