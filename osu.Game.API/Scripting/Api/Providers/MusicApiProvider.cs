// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Reflection;
using Jint.Native;
using osu.Game.Beatmaps;
using osu.Game.Overlays;

namespace osu.Game.API.Scripting.Api.Providers
{
    /// <summary>
    /// Provides control of the global music playback via <c>osu.music</c>.
    /// </summary>
    public class MusicApiProvider : ScriptApiProvider
    {
        private static readonly FieldInfo current_field =
            typeof(MusicController).GetField("current", BindingFlags.NonPublic | BindingFlags.Instance)!;

        [ScriptApi(Target = "music")]
        public bool IsPlaying() => Context.Services.Music?.IsPlaying ?? false;

        [ScriptApi(Target = "music")]
        public void Play()
        {
            MusicController? music = Context.Services.Music;

            if (music != null)
                schedule(() => music.Play(requestedByUser: true));
        }

        [ScriptApi(Target = "music")]
        public void Pause()
        {
            MusicController? music = Context.Services.Music;

            if (music != null)
            {
                schedule(() =>
                {
                    if (music.IsPlaying)
                        music.Stop(requestedByUser: true);
                });
            }
        }

        [ScriptApi(Target = "music")]
        public void TogglePause()
        {
            MusicController? music = Context.Services.Music;

            if (music != null)
                schedule(() => music.TogglePause());
        }

        [ScriptApi(Target = "music")]
        public void Stop()
        {
            MusicController? music = Context.Services.Music;

            if (music != null)
                schedule(() => music.Stop(requestedByUser: true));
        }

        [ScriptApi(Target = "music")]
        public void Next() => Context.Services.Music?.NextTrack();

        [ScriptApi(Target = "music")]
        public void Previous() => Context.Services.Music?.PreviousTrack();

        [ScriptApi(Target = "music")]
        public void Seek(double position) => Context.Services.Music?.SeekTo(position);

        /// <summary>Returns metadata for the currently loaded track, or <c>undefined</c> when none.</summary>
        [ScriptApi(Target = "music")]
        public JsValue CurrentTrack()
        {
            MusicController? music = Context.Services.Music;
            WorkingBeatmap? beatmap = music == null ? null : current_field.GetValue(music) as WorkingBeatmap;

            if (beatmap == null)
                return JsValue.Undefined;

            IBeatmapInfo info = beatmap.BeatmapInfo;
            IBeatmapSetInfo set = beatmap.BeatmapSetInfo;

            JsObject track = Context.CreateObject();
            track.Set("title", JsValue.FromObject(Context.Engine, info.Metadata.Title));
            track.Set("artist", JsValue.FromObject(Context.Engine, info.Metadata.Artist));
            track.Set("beatmapId", JsValue.FromObject(Context.Engine, info.OnlineID));
            track.Set("setId", JsValue.FromObject(Context.Engine, set.OnlineID));
            return track;
        }

        private void schedule(Action action) => Context.Services.Host.UpdateThread.Scheduler.Add(action);
    }
}
