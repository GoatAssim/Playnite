
# GameAction unique Ids — Update instructions (updated with startup/URI/CLI insertion points)

Author: GoatAssim (private fork)
Date: 2026-08-18 (updated)

Purpose
- Add a stable unique Id to each GameAction so URIs and CLI can address and launch a specific game action.
- Ensure legacy libraries without action Ids get one assigned and persisted (one-time migration).
- Wire URI and CLI so the app can launch a specific action by gameId + actionId using existing launch logic.

Summary of changes to apply
1. Add Id property and deserialization fallback to GameAction (already prepared).
2. Add a new URI command constant "startaction" to PlayniteUriHandler.
3. Add a StartAction case to PlayniteApplication.ProcessUriRequest that looks up the game and action by GUID and calls GamesEditor.ActivateAction(game, action).
4. (Optional) Add a small ActionLauncher helper to centralize the lookup+launch logic.
5. (Optional) Add CmdLineOptions.StartAction and ProcessArguments handling to accept a --startaction flag (recommended: use existing --uridata; optional flag is convenience only).
6. Insert the one-time migration that persists generated Ids after the GameDatabase is instantiated/loaded.
7. Add unit/integration tests described below and run the app to verify nothing breaks.

Files to edit & exact insertion points
(These file paths and approximate locations were inspected in your fork.)

A. Add URI command constant
- File: source/Playnite/PlayniteUriHandler.cs
- Insert location: inside the UriCommands class (near the existing constants, top of file).
- Insert this line:
```csharp
public const string StartAction = "startaction";
```





B. Add URI handling case

    File: source/Playnite/App/PlayniteApplication.cs

    Function: internal void ProcessUriRequest(PlayniteUriEventArgs args)

    Exact place to insert: after the existing UriCommands.StartGame case handling (ProcessUriRequest switch). In your copy, StartGame handling is roughly around lines ~1133–1156; insert the new case after that case's break.

    Paste this case (uses existing Database and GamesEditor fields; safe defensive logic):





```C#
case UriCommands.StartAction:
    // Expected arguments: startaction / <gameId> / <actionId>
    if (arguments.Count() != 3)
    {
        logger.Error("startaction URI has wrong number of arguments.");
        return;
    }

    if (!Guid.TryParse(arguments[1], out var gameIdAction) || !Guid.TryParse(arguments[2], out var actionId))
    {
        logger.Error($"Can't start action, failed to parse IDs: {arguments[1]}, {arguments[2]}");
        return;
    }

    var gameForAction = Database.Games[gameIdAction];
    if (gameForAction == null)
    {
        logger.Error($"Cannot start action, game {arguments[1]} not found.");
        return;
    }

    var action = gameForAction.GameActions?.FirstOrDefault(a => a.Id == actionId);
    if (action == null)
    {
        logger.Error($"Cannot find action {arguments[2]} in game {arguments[1]}.");
        return;
    }

    // Use existing API to activate the action (already handles all action types)
    GamesEditor.ActivateAction(gameForAction, action);
    break;
```
    C. Optional CLI convenience flag

    If you want a dedicated CLI flag, add:

        File: source/Playnite/App/CmdLineOptions.cs
            After existing Start property, add:
```C#
[Option("startaction")]
public string StartAction { get; set; }
```


File: source/Playnite/App/PlayniteApplication.cs

    In ProcessArguments() (where other CmdLine.* checks occur — around method start near lines ~1090 in your copy), add:

```C#
else if (!CmdLine.StartAction.IsNullOrEmpty())
{
    var arg = CmdLine.StartAction;
    if (arg.StartsWith("playnite://", StringComparison.OrdinalIgnoreCase))
    {
        PipeService_CommandExecuted(this, new CommandExecutedEventArgs(CmdlineCommand.UriRequest, arg));
    }
    else
    {
        // Accept "gameId:actionId" or "gameId/actionId"
        var parts = arg.Contains(":") ? arg.Split(':') : arg.Split('/');
        if (parts.Length == 2)
        {
            var uri = $"playnite://playnite/{UriCommands.StartAction}/{parts[0]}/{parts[1]}";
            PipeService_CommandExecuted(this, new CommandExecutedEventArgs(CmdlineCommand.UriRequest, uri));
        }
    }
}
```

Recommendation: prefer using the existing --uridata option with a playnite:// URL (see "CLI usage" below). Adding the flag is optional.

D. Optional helper (DRY)

    File: source/Playnite/Services/ActionLauncher.cs (create)
    Purpose: single method to lookup and call GamesEditor.ActivateAction. This centralizes logic for future reuse.

Example content:
```C#
using System;
using System.Linq;

namespace Playnite
{
    public static class ActionLauncher
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        public static bool TryLaunchActionByIds(GameDatabase db, GamesEditor gamesEditor, Guid gameId, Guid actionId, out string error)
        {
            error = null;
            var game = db.Games[gameId];
            if (game == null)
            {
                error = $"Game {gameId} not found.";
                return false;
            }

            var action = game.GameActions?.FirstOrDefault(a => a.Id == actionId);
            if (action == null)
            {
                error = $"Action {actionId} not found in game {gameId}.";
                return false;
            }

            try
            {
                gamesEditor.ActivateAction(game, action);
                return true;
            }
            catch (Exception e)
            {
                logger.Error(e, $"Failed to launch action {actionId} for game {gameId}");
                error = e.Message;
                return false;
            }
        }
    }
}
```
 Use it inside ProcessUriRequest as:
```C#
if (!ActionLauncher.TryLaunchActionByIds(Database, GamesEditor, gameIdAction, actionId, out var err))
{
    logger.Error(err);
}
```
E. One-time migration to persist IDs

    Purpose: ensure legacy saved actions get non-empty Ids and those Ids are persisted to the DB.
    Best insertion point: immediately after the GameDatabase is instantiated and set in the app startup (so DB is loaded). In your fork:
        Fullscreen app: FullscreenApplication.InstantiateApp() calls Database = new GameDatabase(); the startup flow calls InstantiateApp() from FullscreenApplication.Startup(). Add migration immediately after the database is created/opened (or after Database.SetAsSingletonInstance()).
        Desktop app: DesktopApplication.InstantiateApp() (similar spot).
    Minimal migration snippet (place in both Desktop and Fullscreen initialization paths after DB is ready):

```C#
// Run once after DB load
var migrationDoneKey = "GameActionIdsMigrationDone"; // use your settings store key
if (!AppSettings.GetBool(migrationDoneKey)) // pseudo API, adjust to actual settings storage
{
    bool anySaved = false;
    using (Database.BufferedUpdate())
    {
        foreach (var g in Database.Games)
        {
            if (g.GameActions == null) continue;
            bool changed = false;
            foreach (var a in g.GameActions)
            {
                if (a.Id == Guid.Empty)
                {
                    a.Id = Guid.NewGuid();
                    changed = true;
                }
            }
            if (changed)
            {
                Database.Games.Update(g);
                anySaved = true;
            }
        }
    }
    if (anySaved)
    {
        // Optionally: Database.Commit() or similar if required by DB API.
    }
    AppSettings.SetBool(migrationDoneKey, true); // persist the migration-done flag
    AppSettings.SaveSettings();
}
```
Important:

    Replace AppSettings.GetBool/SetBool with your actual persistent settings API (PlayniteSettings wrapper). The snippet shows a gate so the migration runs once.
    Always backup DB before running migration.

F. CLI usage & OS protocol

    OS protocol registration already exists in SystemIntegration.RegisterPlayniteUriProtocol() and uses --uridata to forward full playnite:// URLs to the running instance.
    Minimal, no-code approach to invoke StartAction:
        CLI (no code changes required):
```
Playnite.exe --uridata "playnite://playnite/startaction/{gameId}/{actionId}"
```
        OS URL (shortcut): create a playnite:// link to the URI above; system will call Playnite with --uridata "..." and it will forward to ProcessUriRequest.

G. Testing positions & additions

    Unit tests to add:
        PlayniteUriHandler.ParseUri supports startaction path format (add a test similar to existing PlayniteUriHandlerTests).
        GameAction tests:
            New GameAction default Id not empty.
            OnDeserialized assigns Id if Guid.Empty.
            GetCopy returns a different Id.
        ActionLauncher.TryLaunchActionByIds (if added): mock Database and GamesEditor to assert ActivateAction called.
    Integration tests:
        Create a small database with a game that has two GameActions. Use CLI/URI to start action1 and action2, and verify the correct action runs (logs / controller behavior).
        Upgrade a pre-patch DB (backup first), start app with migration, verify GameAction entries now include Id persisted in DB file.
    Manual tests:
        Use UI shortcuts (existing Start flows) still work unchanged.
        Test example URIs:
            Start game default: playnite://playnite/start/{gameId}
            Start action specific: playnite://playnite/startaction/{gameId}/{actionId}
        Test CLI with --uridata as above when app is not running (should start app and execute after DB load) and when app is running (should forward via pipe and execute immediately).

Safety checklist — how this avoids breaking existing behavior

    Uses existing GamesEditor.ActivateAction to execute actions (no duplication of launch logic).
    Does not change GameAction.Equals behavior (keeps content-based equality).
    Migration is run once and gated; always back up DB first.
    All new code validates GUID parsing and logs errors instead of throwing to avoid crashes.
    Add unit tests and run the full app locally before distributing.

How to apply the changes locally (commands)

    Make a feature branch:

git checkout -b add/gameaction-ids
Apply code edits (edit files listed above: PlayniteUriHandler.cs, PlayniteApplication.cs, (optional) CmdLineOptions.cs, add ActionLauncher.cs, insert migration in instantiate app).
Run tests locally:
# from repo root; tests use NUnit in the Tests project
dotnet test source/Tests/Playnite.Tests/Playnite.Tests.csproj
Build and smoke test the app (Desktop or Fullscreen). Verify basic flows.
Commit and push:
git add <modified files>
git commit -m "Add GameAction Ids, startaction URI handler, optional CLI and migration"
git push origin add/gameaction-ids
xample URIs & CLI for testing

    Start default play (existing):
        playnite://playnite/start/{gameId}
    Start specific action (new):
        playnite://playnite/startaction/{gameId}/{actionId}
    CLI (no code changes):
        Playnite.exe --uridata "playnite://playnite/startaction/{gameId}/{actionId}"
    Optional CLI (if you add CmdLineOptions.StartAction):
        Playnite.exe --startaction "gameId:actionId"

Debugging tips

    If action doesn't launch:
        Check logs for errors from ProcessUriRequest (logger.Error lines).
        Verify GameAction.Id is non-empty: inspect DB or log game.GameActions[].Id.
        Ensure migration has run and persisted IDs (check migration flag in settings if you gate it).
    If StartAction case isn't reached:
        Ensure UriCommands.StartAction constant exists and the URI path matches parse behavior (PlayniteUriHandler.ParseUri splits by '/'; format is playnite://playnite/startaction/{gameId}/{actionId}).
    If serialization doesn't persist Id:
        Confirm game's serializer writes public properties. If DB uses DataContract with explicit members, you may need to add [DataMember] to Id or modify serializer settings.

Additional notes

    I inspected the fork code to find the exact insertion points for URI handling and migration. The key files I used as references in your fork are:
        source/Playnite/PlayniteUriHandler.cs
        source/Playnite/App/PlayniteApplication.cs
        source/Playnite/GamesEditor.cs
        source/Playnite/SystemIntegration.cs
        source/Playnite/App/CmdLineOptions.cs
    The play/start plumbing (pipe forwarding and Uri handling) is already wired; adding the StartAction case integrates into that existing flow. The GamesEditor contains ActivateAction which is the correct single place to call for action launch.

