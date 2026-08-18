# GameAction unique Ids — Full spec, patch, migration, tests and instructions

Author: GoatAssim (for private fork)
Date: 2026-08-18

Purpose
- Provide stable unique identifiers for individual GameAction objects so playnite:// URIs can launch specific actions (actionId) for a specific game (gameId).
- Ensure old installations (saved libraries created before this change) automatically receive unique action IDs on load and persist them back to disk so URIs work reliably.
- Keep change minimal and low risk for existing behavior and third-party plugins.

Problem statement (what you told me)
- You want to be able to address a single game action via URI (e.g. playnite://game/{gameId}/action/{actionId}).
- GameAction objects currently have no stable id; this makes multiple actions indistinguishable by reference.
- Existing saved data (old installs) must receive an id after upgrading so URIs will work on existing libraries.

High-level solution
1. Add a Guid Id property to GameAction, initialized to Guid.NewGuid() for new objects.
2. Add an [OnDeserialized] handler and a getter fallback so deserialized legacy objects without Id (Id == Guid.Empty) get a new GUID before first use.
3. Change GetCopy() to create a distinct Id for copies (so duplicates are separate addressable actions).
4. Keep Equals(...) unchanged (content-based equality) to avoid changing equality semantics across the codebase and avoid introducing subtle bugs.
5. Provide a one-time migration snippet to scan all games after DB load and persist generated IDs back to database files.
6. Provide verification steps, sample URI handler code, tests, and operational guidance for maintaining a fork in sync with upstream.

Why this design
- Adding a property is backwards-compatible for serializers that write out public properties; the ID will be persisted for new objects.
- [OnDeserialized] + getter fallback covers different serializers and ensures runtime safety.
- Not changing Equals keeps current semantics for sets, dictionaries, and routines that rely on content-based equality; changing equality semantics later is a breaking behavioral change.
- Generating new Ids for copies makes copies uniquely addressable and avoids ambiguous URIs.

Patch (full file replacement)
- Replace `source/PlayniteSDK/Models/GameAction.cs` with the file below.

```csharp name=source/PlayniteSDK/Models/GameAction.cs
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;

namespace Playnite.SDK.Models
{
    /// <summary>
    ///
    /// </summary>
    public enum TrackingMode : int
    {
        /// <summary>
        /// Default tracking mode. Playnite will try to use the best one automatically.
        /// </summary>
        [Description("LOCActionTrackingModeDefault")]
        Default = 0,
        /// <summary>
        /// Origin process and all started child processes are tracked.
        /// </summary>
        [Description("LOCActionTrackingModeProcess")]
        Process = 1,
        /// <summary>
        /// Any process from specified directory is tracked.
        /// </summary>
        [Description("LOCActionTrackingModeDirectory")]
        Directory = 2,
        /// <summary>
        /// Only originally started process is being tracked.
        /// </summary>
        [Description("LOCActionTrackingOriginalProcess")]
        OriginalProcess = 3,
        /// <summary>
        /// Any process by given process name is being tracked.
        /// </summary>
        [Description("LOCActionTrackingProcessName")]
        ProcessName = 4
    }

    /// <summary>
    /// Represents game action type.
    /// </summary>
    public enum GameActionType : int
    {
        /// <summary>
        /// Game action executes a file.
        /// </summary>
        [Description("LOCGameActionTypeFile")]
        File = 0,
        /// <summary>
        /// Game action navigates to a web based URL.
        /// </summary>
        [Description("LOCGameActionTypeLink")]
        URL = 1,
        /// <summary>
        /// Game action starts an emulator.
        /// </summary>
        [Description("LOCGameActionTypeEmulator")]
        Emulator = 2,
        /// <summary>
        /// Game action startup is handled by a script.
        /// </summary>
        [Description("LOCGameActionTypeScript")]
        Script = 3
    }

    /// <summary>
    /// Represents executable game action.
    /// </summary>
    public class GameAction : ObservableObject, IEquatable<GameAction>
    {
        // New: stable unique identifier for this action.
        private Guid id = Guid.NewGuid();
        /// <summary>
        /// Unique identifier for this action (used for URIs).
        /// Guaranteed to be non-empty after construction or deserialization.
        /// </summary>
        public Guid Id
        {
            get
            {
                // Ensure legacy deserialized objects get assigned an id if someone accesses the property
                if (id == Guid.Empty)
                {
                    id = Guid.NewGuid();
                }
                return id;
            }
            set
            {
                id = value;
                OnPropertyChanged();
            }
        }

        // Ensures old installations that deserialize objects without Id get a new Guid assigned.
        [OnDeserialized]
        internal void OnDeserialized(StreamingContext context)
        {
            if (id == Guid.Empty)
            {
                id = Guid.NewGuid();
            }
        }

        private GameActionType type;
        /// <summary>
        /// Gets or sets task type.
        /// </summary>
        public GameActionType Type
        {
            get => type;
            set
            {
                type = value;
                OnPropertyChanged();
            }
        }

        private string arguments;
        /// <summary>
        /// Gets or sets executable arguments for File type tasks.
        /// </summary>
        public string Arguments
        {
            get => arguments;
            set
            {
                arguments = value;
                OnPropertyChanged();
            }
        }

        private string additionalArguments;
        /// <summary>
        /// Gets or sets additional executable arguments used for Emulator action type.
        /// </summary>
        public string AdditionalArguments
        {
            get => additionalArguments;
            set
            {
                additionalArguments = value;
                OnPropertyChanged();
            }
        }

        private bool overrideDefaultArgs;
        /// <summary>
        /// Gets or sets value indicating whether emulator arguments should be completely overwritten with action arguments.
        /// Applies only to Emulator action type.
        /// </summary>
        public bool OverrideDefaultArgs
        {
            get => overrideDefaultArgs;
            set
            {
                overrideDefaultArgs = value;
                OnPropertyChanged();
            }
        }

        private string path;
        /// <summary>
        /// Gets or sets executable path for File action type or URL for URL action type.
        /// </summary>
        public string Path
        {
            get => path;
            set
            {
                path = value;
                OnPropertyChanged();
            }
        }

        private string workingDir;
        /// <summary>
        /// Gets or sets working directory for File action type executable.
        /// </summary>
        public string WorkingDir
        {
            get => workingDir;
            set
            {
                workingDir = value;
                OnPropertyChanged();
            }
        }

        private string name;
        /// <summary>
        /// Gets or sets action name.
        /// </summary>
        public string Name
        {
            get => name;
            set
            {
                name = value;
                OnPropertyChanged();
            }
        }

        private bool isPlayAction;
        /// <summary>
        /// Gets or sets value indicating whether an action is play action.
        /// </summary>
        public bool IsPlayAction
        {
            get => isPlayAction;
            set
            {
                isPlayAction = value;
                OnPropertyChanged();
            }
        }

        private Guid emulatorId;
        /// <summary>
        /// Gets or sets emulator id for Emulator action type execution.
        /// </summary>
        public Guid EmulatorId
        {
            get => emulatorId;
            set
            {
                emulatorId = value;
                OnPropertyChanged();
            }
        }

        private string emulatorProfileId;
        /// <summary>
        /// Gets or sets emulator profile id for Emulator action type execution.
        /// </summary>
        public string EmulatorProfileId
        {
            get => emulatorProfileId;
            set
            {
                emulatorProfileId = value;
                OnPropertyChanged();
            }
        }

        private TrackingMode trackingMode = TrackingMode.Default;
        /// <summary>
        /// Gets or sets executable arguments for File type tasks.
        /// </summary>
        public TrackingMode TrackingMode
        {
            get => trackingMode;
            set
            {
                trackingMode = value;
                OnPropertyChanged();
            }
        }

        private string trackingPath;
        /// <summary>
        /// Gets or sets executable arguments for File type tasks.
        /// </summary>
        public string TrackingPath
        {
            get => trackingPath;
            set
            {
                trackingPath = value;
                OnPropertyChanged();
            }
        }

        private string script;
        /// <summary>
        /// Gets or sets startup script.
        /// </summary>
        public string Script
        {
            get => script;
            set
            {
                script = value;
                OnPropertyChanged();
            }
        }

        private int initialTrackingDelay = 0;
        /// <summary>
        /// Gets or sets delay in milliseconds before tracking actually starts.
        /// </summary>
        public int InitialTrackingDelay
        {
            get => initialTrackingDelay;
            set
            {
                initialTrackingDelay = value;
                OnPropertyChanged();
            }
        }

        private int trackingFrequency = 2000;
        /// <summary>
        /// Gets or sets delay in milliseconds before tracking actually starts.
        /// </summary>
        public int TrackingFrequency
        {
            get => trackingFrequency;
            set
            {
                trackingFrequency = value;
                OnPropertyChanged();
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            switch (Type)
            {
                case GameActionType.File:
                    return $"File: {Path}, {Arguments}, {WorkingDir}";
                case GameActionType.URL:
                    return $"Url: {Path}";
                case GameActionType.Emulator:
                    return $"Emulator: {EmulatorId}, {EmulatorProfileId}, {OverrideDefaultArgs}, {AdditionalArguments}";
                case GameActionType.Script:
                    return "Script";
                default:
                    return Path;
            }
        }

        /// <summary>
        /// Compares two <see cref="GameAction"/> objects for equality.
        /// </summary>
        /// <param name="obj1"></param>
        /// <param name="obj2"></param>
        /// <returns></returns>
        public static bool Equals(GameAction obj1, GameAction obj2)
        {
            if (obj1 == null && obj2 == null)
            {
                return true;
            }
            else
            {
                return obj1?.Equals(obj2) == true;
            }
        }

        /// <inheritdoc/>
        public bool Equals(GameAction other)
        {
            if (other is null)
            {
                return false;
            }

            if (Type != other.Type)
            {
                return false;
            }

            if (!string.Equals(Arguments, other.Arguments, StringComparison.Ordinal))
            {
                return false;
            }

            if (!string.Equals(AdditionalArguments, other.AdditionalArguments, StringComparison.Ordinal))
            {
                return false;
            }

            if (!string.Equals(Path, other.Path, StringComparison.Ordinal))
            {
                return false;
            }

            if (!string.Equals(WorkingDir, other.WorkingDir, StringComparison.Ordinal))
            {
                return false;
            }

            if (!string.Equals(Name, other.Name, StringComparison.Ordinal))
            {
                return false;
            }

            if (IsPlayAction != other.IsPlayAction)
            {
                return false;
            }

            if (EmulatorId != other.EmulatorId)
            {
                return false;
            }

            if (!string.Equals(EmulatorProfileId, other.EmulatorProfileId, StringComparison.Ordinal))
            {
                return false;
            }

            if (OverrideDefaultArgs != other.OverrideDefaultArgs)
            {
                return false;
            }

            if (!string.Equals(TrackingPath, other.TrackingPath, StringComparison.Ordinal))
            {
                return false;
            }

            if (TrackingMode != other.TrackingMode)
            {
                return false;
            }

            if (!string.Equals(Script, other.Script, StringComparison.Ordinal))
            {
                return false;
            }

            if (InitialTrackingDelay != other.InitialTrackingDelay)
            {
                return false;
            }

            if (TrackingFrequency != other.TrackingFrequency)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public GameAction GetCopy()
        {
            // New: give the copy a fresh Id so it is a distinct action/addressable by its own URI.
            return new GameAction
            {
                Id = Guid.NewGuid(),
                AdditionalArguments = AdditionalArguments,
                Arguments = Arguments,
                EmulatorId = EmulatorId,
                EmulatorProfileId = EmulatorProfileId,
                IsPlayAction = IsPlayAction,
                Name = Name,
                OverrideDefaultArgs = OverrideDefaultArgs,
                Path = Path,
                Script = Script,
                TrackingMode = TrackingMode,
                TrackingPath = TrackingPath,
                Type = Type,
                WorkingDir = WorkingDir,
                InitialTrackingDelay = InitialTrackingDelay,
                TrackingFrequency = TrackingFrequency
            };
        }
    }
}
```


One-time migration snippet (persist IDs)

    Run this after Playnite's database is fully loaded and you can write changes back. The snippet assigns new GUIDs to any action with Guid.Empty and saves the game record.
    Important: adapt the DB save method to the actual Playnite API in the codebase (PlayniteApi.Database.* call names may differ). Gate the migration so it runs once (persist a flag in settings) or remove it after one successful run.

```C#
// Example migration — run once after DB load
bool anySaved = false;
foreach (var game in PlayniteApi.Database.Games)
{
    if (game.Actions == null)
    {
        continue;
    }

    bool changed = false;
    foreach (var action in game.Actions)
    {
        if (action.Id == Guid.Empty)
        {
            action.Id = Guid.NewGuid();
            changed = true;
        }
    }

    if (changed)
    {
        // Replace with the actual save/update API from Playnite
        PlayniteApi.Database.Games.Update(game); // placeholder; update to real API
        anySaved = true;
    }
}

if (anySaved)
{
    // If DB has commit/sync API, call it. Replace below with real call if needed.
    // PlayniteApi.Database.Commit();
}
```
Where to put migration

    Search for the Database load / app startup area (Desktop app or bootstrap code). Use these grep lines locally:
        grep -R "PlayniteApi.Database" -n
        grep -R "Database.Games" -n
        Search for the app startup files (Playnite.DesktopApp.* or Program.cs, App.xaml.cs)
    Put the snippet after the code that finishes loading the database and before any UI code that relies on the actions or before the app opens the library UI.
    Make the migration idempotent (guard by checking a settings flag, then set the flag to "migration-done").

URI design and example handler

    Recommended URI forms:
        playnite://game/{gameId}/action/{actionId}
        or playnite://run?gameId=<gameId>&actionId=<actionId>
    Handler pseudocode (C#) using Playnite API:

```C#
void OnUri(PlayniteUriEventArgs args)
{
    // Parse URI components; here we assume args contains path or query you can parse.
    var q = System.Web.HttpUtility.ParseQueryString(args.Query ?? "");
    Guid gameId, actionId;
    if (!Guid.TryParse(q["gameId"], out gameId) || !Guid.TryParse(q["actionId"], out actionId))
    {
        // Alternatively parse from path segments if using /game/{id}/action/{id}
        return;
    }

    var game = PlayniteApi.Database.Games.FirstOrDefault(g => g.Id == gameId);
    if (game == null)
    {
        return;
    }

    var action = game.Actions?.FirstOrDefault(a => a.Id == actionId);
    if (action == null)
    {
        return;
    }

    // Launch the action using existing play/launch logic
    // Example: PlayniteApi.MainView?.PlayGameAction(game, action);
}
``` 

Testing and verification

    Unit tests to add (xUnit/NUnit):
        New GameAction default Id is non-empty:
            Create new GameAction(); Assert.True(action.Id != Guid.Empty)
        Deserialization assigns Id:
            Serialize (old-schema-like; omit Id), deserialize into GameAction and assert action.Id != Guid.Empty
            If unable to simulate old serializer, call OnDeserialized(new StreamingContext()) and ensure Id != Guid.Empty
        GetCopy yields different Id:
            var copy = original.GetCopy(); Assert.NotEqual(original.Id, copy.Id)
        Migration assigns Id to persistent data:
            Create in-memory Game object with action.Id == Guid.Empty, run migration snippet, assert action.Id != Guid.Empty and that the save API was called.

    Manual verification:
        Run Playnite from your branch.
        Open an existing old DB (backup first).
        Inspect (debug/log) a few games: Game.Actions[].Id must be non-empty.
        Run migration and then check the saved DB (or saved JSON/XML) contains "Id" values for actions.
        Test a playnite:// URI using the chosen handler and ensure it locates the correct action and launches it.

Equality and behavior notes

    Equals(GameAction other) remains content-based. That means two actions with identical fields but different Ids are still considered equal by the existing Equals method.
    Rationale: preserving Equals avoids unexpected behavior changes in Lists, HashSets, LINQ Distinct, or other code relying on content-based equality.
    If you later decide identity semantics are preferred, update Equals to include Id and implement GetHashCode to include Id; audit all uses of equality before changing.

GetCopy semantics

    Current patch makes GetCopy create a new Id for the returned action. This makes the copy a distinct actionable item (recommended if you want copies to be individually addressable).
    If you prefer copy to retain identity (so URIs still point to both original and copy), change GetCopy to Id = this.Id instead.

Backwards compatibility

    New property is additive; existing code should not break.
    Legacy saved actions that lack the Id property will deserialize with the default value (Guid.Empty); [OnDeserialized] and the getter fallback ensure they receive a GUID at runtime.
    Make sure you persist newly generated IDs back to the DB (one-time migration) so future starts don't reassign different GUIDs every time.

Migration safety & best practices

    Always backup the user's database before running a migration.
    Run the migration in a safe context (main thread after DB load) and persist changes in a controlled manner.
    Mark migration completion in settings or metadata so you don't run it repeatedly.
    Test migration on copies of real DBs (especially with large libraries) to time how long it takes.

How to apply patch locally (exact commands)

    Using shell/git in your repository clone:

```
# fetch and ensure you are on your fork
git checkout -b add/gameaction-ids

# Replace the file (use your editor or the here-doc method)
# Example using here-doc on Unix-like shell (adapt for Windows PowerShell):
cat > source/PlayniteSDK/Models/GameAction.cs <<'EOF'
[PASTE THE FILE CONTENTS FROM THE "Patch" BLOCK ABOVE HERE]
EOF

git add source/PlayniteSDK/Models/GameAction.cs
git commit -m "Add stable Id to GameAction, deserialization fallback, and getcopy creates new Id"
git push origin add/gameaction-ids
```


    If you prefer a .patch: use git diff and git apply style patches — I can produce one if you ask.
