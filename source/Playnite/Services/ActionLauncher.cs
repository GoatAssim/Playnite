using Playnite.Database;
using Playnite.SDK;
using Playnite.SDK.Models;
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
