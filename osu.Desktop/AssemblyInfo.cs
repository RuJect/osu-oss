// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Utils;

// Marks this build as official, suppressing the "not an official build" notification
// (osu.Game/Updater/UpdateManager.cs). Does not affect signing or server verification.
[assembly: OfficialBuildAttribute]
