using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Project101.App
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class MainMenu : MonoBehaviour
    {
        void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            Localization.Apply(root);

            root.Q<Button>("singleplayer").clicked += () => SceneManager.LoadScene(Scenes.Game);

            // Matchmaking does not exist yet.
            root.Q<Button>("multiplayer").SetEnabled(false);

            root.Q<Button>("language").clicked += () =>
            {
                Localization.Current = Localization.Current == Language.English ? Language.Turkish : Language.English;
                Localization.Apply(root);
            };

            // Phones and tablets close apps from the system UI, and the stores expect no quit button.
            var quit = root.Q<Button>("quit");
            quit.style.display = Application.isMobilePlatform ? DisplayStyle.None : DisplayStyle.Flex;
            quit.clicked += Quit;
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
