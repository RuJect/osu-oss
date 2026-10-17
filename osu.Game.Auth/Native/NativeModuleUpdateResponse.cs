// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Auth
{
    internal sealed class NativeModuleUpdateResponse
    {
        public string? Url { get; set; }

        public string? Checksum { get; set; }

        public bool Restart { get; set; }
    }
}
