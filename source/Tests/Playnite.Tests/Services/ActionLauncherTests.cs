using NUnit.Framework;
using Playnite.Controllers;
using Playnite.Database;
using Playnite.SDK.Models;
using System;
using System.Collections.ObjectModel;

namespace Playnite.Tests
{
    [TestFixture]
    public class ActionLauncherTests
    {
        [Test]
        public void TryLaunchActionByIds_GameNotFound_ReturnsFalse()
        {
            using (var wrapper = new GameDbTestWrapper())
            {
                var editor = new GamesEditor(
                    wrapper.DB,
                    new GameControllerFactory(wrapper.DB),
                    new PlayniteSettings(),
                    null,
                    null,
                    new TestPlayniteApplication(),
                    null);

                var result = ActionLauncher.TryLaunchActionByIds(
                    wrapper.DB,
                    editor,
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    out var error);

                Assert.IsFalse(result);
                Assert.IsNotNull(error);
                StringAssert.Contains("not found", error);
            }
        }

        [Test]
        public void TryLaunchActionByIds_ActionNotFound_ReturnsFalse()
        {
            using (var wrapper = new GameDbTestWrapper())
            {
                var game = new Game { Name = "Test Game" };
                wrapper.DB.Games.Add(game);

                var editor = new GamesEditor(
                    wrapper.DB,
                    new GameControllerFactory(wrapper.DB),
                    new PlayniteSettings(),
                    null,
                    null,
                    new TestPlayniteApplication(),
                    null);

                var result = ActionLauncher.TryLaunchActionByIds(
                    wrapper.DB,
                    editor,
                    game.Id,
                    Guid.NewGuid(),
                    out var error);

                Assert.IsFalse(result);
                Assert.IsNotNull(error);
                StringAssert.Contains("Action", error);
            }
        }

        [Test]
        public void TryLaunchActionByIds_ValidAction_ReturnsTrue()
        {
            using (var wrapper = new GameDbTestWrapper())
            {
                var actionId = Guid.NewGuid();
                var action = new GameAction
                {
                    Id = actionId,
                    Name = "Test URL",
                    Type = GameActionType.URL,
                    Path = "https://example.com"
                };

                var game = new Game
                {
                    Name = "Test Game",
                    GameActions = new ObservableCollection<GameAction> { action }
                };
                wrapper.DB.Games.Add(game);

                var editor = new GamesEditor(
                    wrapper.DB,
                    new GameControllerFactory(wrapper.DB),
                    new PlayniteSettings(),
                    null,
                    null,
                    new TestPlayniteApplication(),
                    null);

                var result = ActionLauncher.TryLaunchActionByIds(
                    wrapper.DB,
                    editor,
                    game.Id,
                    actionId,
                    out var error);

                Assert.IsTrue(result);
                Assert.IsNull(error);
            }
        }

        [Test]
        public void TryLaunchActionByIds_LibraryPluginAction_WithoutPlugin_ReturnsFalse()
        {
            using (var wrapper = new GameDbTestWrapper())
            {
                var game = new Game
                {
                    Name = "Plugin Game",
                    PluginId = Guid.NewGuid()
                };
                wrapper.DB.Games.Add(game);

                var editor = new GamesEditor(
                    wrapper.DB,
                    new GameControllerFactory(wrapper.DB),
                    new PlayniteSettings(),
                    null,
                    null,
                    new TestPlayniteApplication(),
                    null);

                var result = ActionLauncher.TryLaunchActionByIds(
                    wrapper.DB,
                    editor,
                    game.Id,
                    game.LibraryPluginPlayActionId,
                    out var error);

                Assert.IsFalse(result);
                Assert.IsNotNull(error);
                StringAssert.Contains("plugin", error.ToLowerInvariant());
            }
        }
    }
}
