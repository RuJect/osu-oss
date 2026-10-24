// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Jint.Native;
using osu.Game.API.Services;

namespace osu.Game.API.Scripting.Api.Providers
{
    /// <summary>
    /// Provides control of the global music playback via <c>osu.music</c>.
    /// </summary>
    public class MusicApiProvider : ScriptApiProvider
    {
        [ScriptApi(Target = "music")]
        public bool IsPlaying() => Context.Services.Music?.IsPlaying ?? false;

        [ScriptApi(Target = "music")]
        public void Play() => Context.Services.Music?.Play();

        [ScriptApi(Target = "music")]
        public void Pause() => Context.Services.Music?.Pause();

        [ScriptApi(Target = "music")]
        public void TogglePause() => Context.Services.Music?.TogglePause();

        [ScriptApi(Target = "music")]
        public void Stop() => Context.Services.Music?.Stop();

        [ScriptApi(Target = "music")]
        public void Next() => Context.Services.Music?.Next();

        [ScriptApi(Target = "music")]
        public void Previous() => Context.Services.Music?.Previous();

        [ScriptApi(Target = "music")]
        public void Seek(double position) => Context.Services.Music?.Seek(position);

        /// <summary>Returns metadata for the currently loaded track, or <c>undefined</c> when none.</summary>
        [ScriptApi(Target = "music")]
        public JsValue CurrentTrack()
        {
            MusicTrack? track = Context.Services.Music?.CurrentTrack();

            if (track == null)
                return JsValue.Undefined;

            JsObject result = Context.CreateObject();
            result.Set("title", JsValue.FromObject(Context.Engine, track.Title));
            result.Set("artist", JsValue.FromObject(Context.Engine, track.Artist));
            result.Set("beatmapId", JsValue.FromObject(Context.Engine, track.BeatmapId));
            result.Set("setId", JsValue.FromObject(Context.Engine, track.SetId));
            return result;
        }
    }
}