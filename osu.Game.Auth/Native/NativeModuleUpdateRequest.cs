// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Auth
{
    internal sealed class NativeModuleUpdateRequest
    {
        public required string FileName { get; init; }

        public string? CurrentHash { get; set; }

        public required int PlatformId { get; init; }

        public bool IsTachyon { get; init; }
    }
}
