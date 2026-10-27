// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.API.Services;

namespace osu.Game.API.Scripting.Api
{
    /// <summary>
    /// Bundle of cross-cutting services exposed to <see cref="IScriptApiProvider"/>s while building the
    /// global API objects for a script instance. Extends the plugin-scoped <see cref="ApiServices"/> with
    /// the script-specific repository and event bus.
    /// </summary>
    public class ScriptServices : ApiServices
    {
        public ScriptRepository Repository { get; }

        public ScriptEventBus Events { get; }

        public ScriptServices(ApiServices services, ScriptRepository repository, ScriptEventBus events)
            : base(services)
        {
            Repository = repository;
            Events = events;
        }
    }
}
