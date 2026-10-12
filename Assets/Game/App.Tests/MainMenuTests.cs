using System.Collections;
using NUnit.Framework;
using Project101.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Project101.App.Tests
{
    public class MainMenuTests
    {
        static VisualElement Root() => Object.FindFirstObjectByType<UIDocument>().rootVisualElement;

        static void Click(Button button)
        {
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = button;
                button.SendEvent(submit);
            }
        }

        static IEnumerator WaitForScene(string name)
        {
            for (int i = 0; i < 10 && SceneManager.GetActiveScene().name != name; i++) yield return null;
            Assert.AreEqual(name, SceneManager.GetActiveScene().name);
        }

        [UnityTest]
        public IEnumerator SinglePlayerOpensTheTableAndMenuReturns()
        {
            yield return SceneManager.LoadSceneAsync(Scenes.MainMenu);
            yield return null;

            Assert.IsFalse(Root().Q<Button>("multiplayer").enabledSelf);
            Assert.AreEqual(DisplayStyle.Flex, Root().Q<Button>("quit").style.display.value);

            Click(Root().Q<Button>("singleplayer"));
            yield return WaitForScene(Scenes.Game);

            Click(Root().Q<Button>("menu"));
            yield return WaitForScene(Scenes.MainMenu);
        }

        [UnityTest]
        public IEnumerator HumanDiscardsAndTheBotsPlayBackToTheHuman()
        {
            yield return SceneManager.LoadSceneAsync(Scenes.Game);
            yield return null;

            var controller = Object.FindFirstObjectByType<GameController>();
            controller.BotDelay = 0f;
            var round = controller.Match.Round;
            Assert.AreEqual(GameController.Human, round.Current);
            Assert.AreEqual(22, round.Rack(GameController.Human).Count);
            Assert.IsFalse(Root().Q<Button>("discard").enabledSelf);

            // Discarding needs exactly one selected tile.
            var tile = round.Rack(GameController.Human)[0];
            controller.ClickRackTile(tile);
            Assert.IsTrue(Root().Q<Button>("discard").enabledSelf);
            Click(Root().Q<Button>("discard"));
            yield return null;
            Assert.AreEqual(tile, round.DiscardPile(GameController.Human)[0]);

            for (int i = 0; i < 30 && round.Current != GameController.Human; i++) yield return null;
            Assert.AreEqual(GameController.Human, round.Current);
            Assert.AreEqual(TurnPhase.Draw, round.Phase);
            Assert.AreEqual(17, round.StockCount);

            controller.DrawFromStock();
            Assert.AreEqual(TurnPhase.Play, round.Phase);
            Assert.AreEqual(22, round.Rack(GameController.Human).Count);
        }

        [UnityTest]
        public IEnumerator LanguageButtonSwitchesTextsAndIsRemembered()
        {
            var before = Localization.Current;
            try
            {
                Localization.Current = Language.English;
                yield return SceneManager.LoadSceneAsync(Scenes.MainMenu);
                yield return null;
                Assert.AreEqual("Single Player", Root().Q<Button>("singleplayer").text);
                Assert.AreEqual("Language: English", Root().Q<Button>("language").text);

                Click(Root().Q<Button>("language"));
                yield return null;
                Assert.AreEqual("Tek Oyunculu", Root().Q<Button>("singleplayer").text);
                Assert.AreEqual("Dil: Türkçe", Root().Q<Button>("language").text);
                Assert.AreEqual((int)Language.Turkish, PlayerPrefs.GetInt("language"));

                // The choice carries into the game and back to a freshly loaded menu.
                yield return SceneManager.LoadSceneAsync(Scenes.Game);
                yield return null;
                Assert.AreEqual("Taş At", Root().Q<Button>("discard").text);
                yield return SceneManager.LoadSceneAsync(Scenes.MainMenu);
                yield return null;
                Assert.AreEqual("Çıkış", Root().Q<Button>("quit").text);
            }
            finally
            {
                Localization.Current = before;
            }
        }
    }
}
