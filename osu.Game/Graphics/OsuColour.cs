// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Reflection;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Game.Beatmaps;
using osu.Game.Online.Rooms;
using osu.Game.Overlays;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Theming;
using osu.Game.Utils;
using osuTK.Graphics;

namespace osu.Game.Graphics
{
    public class OsuColour
    {
        public static Color4 Gray(float amt) => new Color4(amt, amt, amt, 1f);
        public static Color4 Gray(byte amt) => new Color4(amt, amt, amt, 255);

        /// <summary>
        /// The maximum star rating colour which can be distinguished against a black background.
        /// </summary>
        public const float STAR_DIFFICULTY_DEFINED_COLOUR_CUTOFF = 6.5f;

        /// <summary>
        /// Star rating at which display text switches from static colours to a gradient.
        /// </summary>
        public const float STAR_DIFFICULTY_TEXT_GRADIENT_CUTOFF = 9.0f;

        public static readonly (float, Color4)[] STAR_DIFFICULTY_SPECTRUM =
        {
            (0.1f, Color4Extensions.FromHex("aaaaaa")),
            (0.1f, Color4Extensions.FromHex("4290fb")),
            (1.25f, Color4Extensions.FromHex("4fc0ff")),
            (2.0f, Color4Extensions.FromHex("4fffd5")),
            (2.5f, Color4Extensions.FromHex("7cff4f")),
            (3.3f, Color4Extensions.FromHex("f6f05c")),
            (4.2f, Color4Extensions.FromHex("ff8068")),
            (4.9f, Color4Extensions.FromHex("ff4e6f")),
            (5.8f, Color4Extensions.FromHex("c645b8")),
            (6.7f, Color4Extensions.FromHex("6563de")),
            (7.7f, Color4Extensions.FromHex("18158e")),
            (9.0f, Color4.Black),
            (10.0f, Color4.Black),
        };

        public static readonly (float, Color4)[] STAR_DIFFICULTY_TEXT_SPECTRUM =
        {
            (9.0f, Color4Extensions.FromHex("f6f05c")),
            (9.9f, Color4Extensions.FromHex("ff8068")),
            (10.6f, Color4Extensions.FromHex("ff4e6f")),
            (11.5f, Color4Extensions.FromHex("c645b8")),
            (12.4f, Color4Extensions.FromHex("6563de")),
        };

        public OsuColour()
        {
            ThemeSnapshot theme = ThemeManager.Current;

            if (theme.Id == "vanilla")
                return;

            foreach (FieldInfo field in typeof(OsuColour).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.FieldType == typeof(Color4) && theme.TryGetColour(field.Name, out Colour4 themed))
                    field.SetValue(this, (Color4)themed);
            }

            if (ThemeManager.AggressiveTinting)
            {
                foreach (FieldInfo field in typeof(OsuColour).GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (field.FieldType == typeof(Color4))
                        field.SetValue(this, (Color4)theme.ApplyAggressiveTint((Color4)field.GetValue(this)!));
                }
            }
        }

        /// <summary>
        /// Retrieves the colour for a given point in the star range.
        /// </summary>
        public Color4 ForStarDifficulty(double starDifficulty) => ColourUtils.SampleFromLinearGradient(STAR_DIFFICULTY_SPECTRUM, (float)Math.Round(starDifficulty, 2, MidpointRounding.AwayFromZero));

        /// <summary>
        /// Retrieves the colour for the text inside the star rating display.
        /// </summary>
        public Color4 ForStarDifficultyText(double starDifficulty)
        {
            if (starDifficulty < STAR_DIFFICULTY_DEFINED_COLOUR_CUTOFF)
                return Color4.Black.Opacity(0.75f);

            if (starDifficulty < STAR_DIFFICULTY_TEXT_GRADIENT_CUTOFF)
                return Orange1;

            return ColourUtils.SampleFromLinearGradient(STAR_DIFFICULTY_TEXT_SPECTRUM, (float)Math.Round(starDifficulty, 2, MidpointRounding.AwayFromZero));
        }

        /// <summary>
        /// Retrieves the colour for a <see cref="ScoreRank"/>.
        /// </summary>
        public static Color4 ForRank(ScoreRank rank)
        {
            switch (rank)
            {
                case ScoreRank.XH:
                case ScoreRank.X:
                    return Color4Extensions.FromHex(@"de31ae");

                case ScoreRank.SH:
                case ScoreRank.S:
                    return Color4Extensions.FromHex(@"02b5c3");

                case ScoreRank.A:
                    return Color4Extensions.FromHex(@"88da20");

                case ScoreRank.B:
                    return Color4Extensions.FromHex(@"e3b130");

                case ScoreRank.C:
                    return Color4Extensions.FromHex(@"ff8e5d");

                case ScoreRank.D:
                    return Color4Extensions.FromHex(@"ff5a5a");

                case ScoreRank.F:
                default:
                    return Color4Extensions.FromHex(@"3f3f3f");
            }
        }

        /// <summary>
        /// Retrieves the colour for a <see cref="HitResult"/>.
        /// </summary>
        public Color4 ForHitResult(HitResult result)
        {
            switch (result)
            {
                case HitResult.IgnoreMiss:
                case HitResult.SmallTickMiss:
                    return Color4.Gray;

                case HitResult.Miss:
                case HitResult.LargeTickMiss:
                case HitResult.ComboBreak:
                    return Red;

                case HitResult.Meh:
                    return Yellow;

                case HitResult.Ok:
                    return Green;

                case HitResult.Good:
                    return GreenLight;

                case HitResult.SmallTickHit:
                case HitResult.LargeTickHit:
                case HitResult.SliderTailHit:
                case HitResult.Great:
                    return Blue;

                default:
                    return BlueLight;
            }
        }

        /// <summary>
        /// Retrieves a colour for the given <see cref="BeatmapOnlineStatus"/>.
        /// A <see langword="null"/> value indicates that a "background" shade from the local <see cref="OverlayColourProvider"/>
        /// (or another fallback colour) should be used.
        /// </summary>
        /// <remarks>
        /// Sourced from web: https://github.com/ppy/osu-web/blob/007eebb1916ed5cb6a7866d82d8011b1060a945e/resources/assets/less/layout.less#L36-L50
        /// </remarks>
        public static Color4? ForBeatmapSetOnlineStatus(BeatmapOnlineStatus status)
        {
            switch (status)
            {
                case BeatmapOnlineStatus.None:
                    return Color4.RosyBrown;

                case BeatmapOnlineStatus.LocallyModified:
                    return Color4.OrangeRed;

                case BeatmapOnlineStatus.Ranked:
                case BeatmapOnlineStatus.Approved:
                    return Color4Extensions.FromHex(@"b3ff66");

                case BeatmapOnlineStatus.Loved:
                    return Color4Extensions.FromHex(@"ff66ab");

                case BeatmapOnlineStatus.Qualified:
                    return Color4Extensions.FromHex(@"66ccff");

                case BeatmapOnlineStatus.Pending:
                    return Color4Extensions.FromHex(@"ffd966");

                case BeatmapOnlineStatus.WIP:
                    return Color4Extensions.FromHex(@"ff9966");

                case BeatmapOnlineStatus.Graveyard:
                    return Color4.Black;

                default:
                    return null;
            }
        }

        /// <summary>
        /// Retrieves the main accent colour for a <see cref="ModType"/>.
        /// </summary>
        public Color4 ForModType(ModType modType)
        {
            switch (modType)
            {
                case ModType.Automation:
                    return Blue1;

                case ModType.DifficultyIncrease:
                    return Red1;

                case ModType.DifficultyReduction:
                    return Lime1;

                case ModType.Conversion:
                    return Purple1;

                case ModType.Fun:
                    return Pink1;

                case ModType.System:
                    return Yellow;

                default:
                    throw new ArgumentOutOfRangeException(nameof(modType), modType, "Unknown mod type");
            }
        }

        /// <summary>
        /// Retrieves the main accent colour for a <see cref="RoomCategory"/>.
        /// </summary>
        public Color4? ForRoomCategory(RoomCategory roomCategory)
        {
            switch (roomCategory)
            {
                case RoomCategory.Spotlight:
                    return SpotlightColour;

                case RoomCategory.FeaturedArtist:
                    return FeaturedArtistColour;

                default:
                    return null;
            }
        }

        /// <summary>
        /// Retrieves the accent colour representing a <see cref="Room"/>'s current status.
        /// </summary>
        public Color4 ForRoomStatus(Room room)
        {
            if (room.HasEnded)
                return YellowDarker;

            switch (room.Status)
            {
                case RoomStatus.Playing:
                    return Purple;

                default:
                    if (room.HasPassword)
                        return GreenDark;

                    return GreenLight;
            }
        }

        /// <summary>
        /// Retrieves colour for a <see cref="RankingTier"/>.
        /// See https://www.figma.com/file/YHWhp9wZ089YXgB7pe6L1k/Tier-Colours
        /// </summary>
        public static ColourInfo ForRankingTier(RankingTier tier)
        {
            switch (tier)
            {
                default:
                case RankingTier.Iron:
                    return Color4Extensions.FromHex(@"BAB3AB");

                case RankingTier.Bronze:
                    return ColourInfo.GradientVertical(Color4Extensions.FromHex(@"B88F7A"), Color4Extensions.FromHex(@"855C47"));

                case RankingTier.Silver:
                    return ColourInfo.GradientVertical(Color4Extensions.FromHex(@"E0E0EB"), Color4Extensions.FromHex(@"A3A3C2"));

                case RankingTier.Gold:
                    return ColourInfo.GradientVertical(Color4Extensions.FromHex(@"F0E4A8"), Color4Extensions.FromHex(@"E0C952"));

                case RankingTier.Platinum:
                    return ColourInfo.GradientVertical(Color4Extensions.FromHex(@"A8F0EF"), Color4Extensions.FromHex(@"52E0DF"));

                case RankingTier.Rhodium:
                    return ColourInfo.GradientVertical(Color4Extensions.FromHex(@"D9F8D3"), Color4Extensions.FromHex(@"A0CF96"));

                case RankingTier.Radiant:
                    return ColourInfo.GradientVertical(Color4Extensions.FromHex(@"97DCFF"), Color4Extensions.FromHex(@"ED82FF"));

                case RankingTier.Lustrous:
                    return ColourInfo.GradientVertical(Color4Extensions.FromHex(@"FFE600"), Color4Extensions.FromHex(@"ED82FF"));
            }
        }

        /// <summary>
        /// Returns a foreground text colour that is supposed to contrast well with
        /// the supplied <paramref name="backgroundColour"/>.
        /// </summary>
        public static Color4 ForegroundTextColourFor(Color4 backgroundColour)
        {
            // formula taken from the RGB->YIQ conversions: https://en.wikipedia.org/wiki/YIQ
            // brightness here is equivalent to the Y component in the above colour model, which is a rough estimate of lightness.
            float brightness = 0.299f * backgroundColour.R + 0.587f * backgroundColour.G + 0.114f * backgroundColour.B;
            return Gray(brightness > 0.5f ? 0.2f : 0.9f);
        }

        public Color4 TeamColourRed = Color4Extensions.FromHex("#AA1414");
        public Color4 TeamColourBlue = Color4Extensions.FromHex("#1462AA");

        // See https://github.com/ppy/osu-web/blob/master/resources/assets/less/colors.less
        public Color4 PurpleLighter = Color4Extensions.FromHex(@"eeeeff");
        public Color4 PurpleLight = Color4Extensions.FromHex(@"aa88ff");
        public Color4 PurpleLightAlternative = Color4Extensions.FromHex(@"cba4da");
        public Color4 Purple = Color4Extensions.FromHex(@"8866ee");
        public Color4 PurpleDark = Color4Extensions.FromHex(@"6644cc");
        public Color4 PurpleDarkAlternative = Color4Extensions.FromHex(@"312436");
        public Color4 PurpleDarker = Color4Extensions.FromHex(@"441188");

        public Color4 PinkLighter = Color4Extensions.FromHex(@"ffddee");
        public Color4 PinkLight = Color4Extensions.FromHex(@"ff99cc");
        public Color4 Pink = Color4Extensions.FromHex(@"ff66aa");
        public Color4 PinkDark = Color4Extensions.FromHex(@"cc5288");
        public Color4 PinkDarker = Color4Extensions.FromHex(@"bb1177");

        public Color4 BlueLighter = Color4Extensions.FromHex(@"ddffff");
        public Color4 BlueLight = Color4Extensions.FromHex(@"99eeff");
        public Color4 Blue = Color4Extensions.FromHex(@"66ccff");
        public Color4 BlueDark = Color4Extensions.FromHex(@"44aadd");
        public Color4 BlueDarker = Color4Extensions.FromHex(@"2299bb");

        public Color4 YellowLighter = Color4Extensions.FromHex(@"ffffdd");
        public Color4 YellowLight = Color4Extensions.FromHex(@"ffdd55");
        public Color4 Yellow = Color4Extensions.FromHex(@"ffcc22");
        public Color4 YellowDark = Color4Extensions.FromHex(@"eeaa00");
        public Color4 YellowDarker = Color4Extensions.FromHex(@"cc6600");

        public Color4 GreenLighter = Color4Extensions.FromHex(@"eeffcc");
        public Color4 GreenLight = Color4Extensions.FromHex(@"b3d944");
        public Color4 Green = Color4Extensions.FromHex(@"88b300");
        public Color4 GreenDark = Color4Extensions.FromHex(@"668800");
        public Color4 GreenDarker = Color4Extensions.FromHex(@"445500");

        public Color4 Sky = Color4Extensions.FromHex(@"6bb5ff");
        public Color4 GreySkyLighter = Color4Extensions.FromHex(@"c6e3f4");
        public Color4 GreySkyLight = Color4Extensions.FromHex(@"8ab3cc");
        public Color4 GreySky = Color4Extensions.FromHex(@"405461");
        public Color4 GreySkyDark = Color4Extensions.FromHex(@"303d47");
        public Color4 GreySkyDarker = Color4Extensions.FromHex(@"21272c");

        public Color4 SeaFoam = Color4Extensions.FromHex(@"05ffa2");
        public Color4 GreySeaFoamLighter = Color4Extensions.FromHex(@"9ebab1");
        public Color4 GreySeaFoamLight = Color4Extensions.FromHex(@"4d7365");
        public Color4 GreySeaFoam = Color4Extensions.FromHex(@"33413c");
        public Color4 GreySeaFoamDark = Color4Extensions.FromHex(@"2c3532");
        public Color4 GreySeaFoamDarker = Color4Extensions.FromHex(@"1e2422");

        public Color4 Cyan = Color4Extensions.FromHex(@"05f4fd");
        public Color4 GreyCyanLighter = Color4Extensions.FromHex(@"77b1b3");
        public Color4 GreyCyanLight = Color4Extensions.FromHex(@"436d6f");
        public Color4 GreyCyan = Color4Extensions.FromHex(@"293d3e");
        public Color4 GreyCyanDark = Color4Extensions.FromHex(@"243536");
        public Color4 GreyCyanDarker = Color4Extensions.FromHex(@"1e2929");

        public Color4 Lime = Color4Extensions.FromHex(@"82ff05");
        public Color4 GreyLimeLighter = Color4Extensions.FromHex(@"deff87");
        public Color4 GreyLimeLight = Color4Extensions.FromHex(@"657259");
        public Color4 GreyLime = Color4Extensions.FromHex(@"3f443a");
        public Color4 GreyLimeDark = Color4Extensions.FromHex(@"32352e");
        public Color4 GreyLimeDarker = Color4Extensions.FromHex(@"2e302b");

        public Color4 Violet = Color4Extensions.FromHex(@"bf04ff");
        public Color4 GreyVioletLighter = Color4Extensions.FromHex(@"ebb8fe");
        public Color4 GreyVioletLight = Color4Extensions.FromHex(@"685370");
        public Color4 GreyViolet = Color4Extensions.FromHex(@"46334d");
        public Color4 GreyVioletDark = Color4Extensions.FromHex(@"2c2230");
        public Color4 GreyVioletDarker = Color4Extensions.FromHex(@"201823");

        public Color4 Carmine = Color4Extensions.FromHex(@"ff0542");
        public Color4 GreyCarmineLighter = Color4Extensions.FromHex(@"deaab4");
        public Color4 GreyCarmineLight = Color4Extensions.FromHex(@"644f53");
        public Color4 GreyCarmine = Color4Extensions.FromHex(@"342b2d");
        public Color4 GreyCarmineDark = Color4Extensions.FromHex(@"302a2b");
        public Color4 GreyCarmineDarker = Color4Extensions.FromHex(@"241d1e");

        public Color4 Gray0 = Color4Extensions.FromHex(@"000");
        public Color4 Gray1 = Color4Extensions.FromHex(@"111");
        public Color4 Gray2 = Color4Extensions.FromHex(@"222");
        public Color4 Gray3 = Color4Extensions.FromHex(@"333");
        public Color4 Gray4 = Color4Extensions.FromHex(@"444");
        public Color4 Gray5 = Color4Extensions.FromHex(@"555");
        public Color4 Gray6 = Color4Extensions.FromHex(@"666");
        public Color4 Gray7 = Color4Extensions.FromHex(@"777");
        public Color4 Gray8 = Color4Extensions.FromHex(@"888");
        public Color4 Gray9 = Color4Extensions.FromHex(@"999");
        public Color4 GrayA = Color4Extensions.FromHex(@"aaa");
        public Color4 GrayB = Color4Extensions.FromHex(@"bbb");
        public Color4 GrayC = Color4Extensions.FromHex(@"ccc");
        public Color4 GrayD = Color4Extensions.FromHex(@"ddd");
        public Color4 GrayE = Color4Extensions.FromHex(@"eee");
        public Color4 GrayF = Color4Extensions.FromHex(@"fff");

        #region "Basic" colour theme

        // Reference: https://www.figma.com/file/VIkXMYNPMtQem2RJg9k2iQ/Asset%2FColours?node-id=1838%3A3

        // Note that the colours in this region are also defined in `OverlayColourProvider` as `Colour{0,1,2,3,4}`.
        // The difference as to which should be used where comes down to context.
        // If the colour in question is supposed to always match the view in which it is displayed theme-wise, use `OverlayColourProvider`.
        // If the colour usage is special and in general differs from the surrounding view in choice of hue, use the `OsuColour` constants.

        public Color4 Pink0 = Color4Extensions.FromHex(@"ff99c7");
        public Color4 Pink1 = Color4Extensions.FromHex(@"ff66ab");
        public Color4 Pink2 = Color4Extensions.FromHex(@"eb4791");
        public Color4 Pink3 = Color4Extensions.FromHex(@"cc3378");
        public Color4 Pink4 = Color4Extensions.FromHex(@"6b2e49");

        public Color4 Purple0 = Color4Extensions.FromHex(@"b299ff");
        public Color4 Purple1 = Color4Extensions.FromHex(@"8c66ff");
        public Color4 Purple2 = Color4Extensions.FromHex(@"7047eb");
        public Color4 Purple3 = Color4Extensions.FromHex(@"5933cc");
        public Color4 Purple4 = Color4Extensions.FromHex(@"3d2e6b");

        public Color4 Blue0 = Color4Extensions.FromHex(@"99ddff");
        public Color4 Blue1 = Color4Extensions.FromHex(@"66ccff");
        public Color4 Blue2 = Color4Extensions.FromHex(@"47b4eb");
        public Color4 Blue3 = Color4Extensions.FromHex(@"3399cc");
        public Color4 Blue4 = Color4Extensions.FromHex(@"2e576b");

        public Color4 Green0 = Color4Extensions.FromHex(@"99ffa2");
        public Color4 Green1 = Color4Extensions.FromHex(@"66ff73");
        public Color4 Green2 = Color4Extensions.FromHex(@"47eb55");
        public Color4 Green3 = Color4Extensions.FromHex(@"33cc40");
        public Color4 Green4 = Color4Extensions.FromHex(@"2e6b33");

        public Color4 Lime0 = Color4Extensions.FromHex(@"ccff99");
        public Color4 Lime1 = Color4Extensions.FromHex(@"b2ff66");
        public Color4 Lime2 = Color4Extensions.FromHex(@"99eb47");
        public Color4 Lime3 = Color4Extensions.FromHex(@"7fcc33");
        public Color4 Lime4 = Color4Extensions.FromHex(@"4c6b2e");

        public Color4 Orange0 = Color4Extensions.FromHex(@"ffe699");
        public Color4 Orange1 = Color4Extensions.FromHex(@"ffd966");
        public Color4 Orange2 = Color4Extensions.FromHex(@"ebc247");
        public Color4 Orange3 = Color4Extensions.FromHex(@"cca633");
        public Color4 Orange4 = Color4Extensions.FromHex(@"6b5c2e");

        public Color4 DarkOrange0 = Color4Extensions.FromHex(@"ffbb99");
        public Color4 DarkOrange1 = Color4Extensions.FromHex(@"ff9966");
        public Color4 DarkOrange2 = Color4Extensions.FromHex(@"eb7e47");
        public Color4 DarkOrange3 = Color4Extensions.FromHex(@"cc6633");
        public Color4 DarkOrange4 = Color4Extensions.FromHex(@"6b422e");

        public Color4 Red0 = Color4Extensions.FromHex(@"ff9b9b");
        public Color4 Red1 = Color4Extensions.FromHex(@"ff6666");
        public Color4 Red2 = Color4Extensions.FromHex(@"eb4747");
        public Color4 Red3 = Color4Extensions.FromHex(@"cc3333");
        public Color4 Red4 = Color4Extensions.FromHex(@"6b2e2e");

        #endregion

        // Content Background
        public Color4 B5 = Color4Extensions.FromHex(@"222a28");

        public Color4 RedLighter = Color4Extensions.FromHex(@"ffeded");
        public Color4 RedLight = Color4Extensions.FromHex(@"ed7787");
        public Color4 Red = Color4Extensions.FromHex(@"ed1121");
        public Color4 RedDark = Color4Extensions.FromHex(@"ba0011");
        public Color4 RedDarker = Color4Extensions.FromHex(@"870000");

        public Color4 ChatBlue = Color4Extensions.FromHex(@"17292e");

        public Color4 ContextMenuGray = Color4Extensions.FromHex(@"223034");

        public Color4 SpotlightColour => Green2;
        public Color4 FeaturedArtistColour => Blue2;

        public Color4 DangerousButtonColour => Pink3;
    }
}
