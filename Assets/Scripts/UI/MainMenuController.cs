using RoyaltyBoat.Flow;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace RoyaltyBoat.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class MainMenuController : MonoBehaviour
    {
        private UIDocument document;
        private VisualElement mainScreen;
        private VisualElement creditsScreen;
        private Button startButton;
        private Button creditsButton;
        private Button quitButton;
        private Button backButton;
        private Button oceanCreditButton;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (SceneManager.GetActiveScene().name != VoyageFlow.GameplaySceneName ||
                VoyageFlow.IsVoyageActive ||
                FindAnyObjectByType<MainMenuController>() != null)
            {
                return;
            }

            VisualTreeAsset layout = Resources.Load<VisualTreeAsset>("MainMenu/MainMenu");
            PanelSettings panelSettings = Resources.Load<PanelSettings>("MainMenu/MainMenuPanelSettings");
            if (layout == null || panelSettings == null)
            {
                Debug.LogError("Main menu UI assets could not be loaded from Resources/MainMenu.");
                return;
            }

            GameObject host = new GameObject("Main Menu");
            UIDocument uiDocument = host.AddComponent<UIDocument>();
            uiDocument.panelSettings = panelSettings;
            uiDocument.visualTreeAsset = layout;
            uiDocument.sortingOrder = 100;
            host.AddComponent<MainMenuController>();
        }

        private void Awake()
        {
            document = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            VisualElement root = document.rootVisualElement;
            mainScreen = root.Q<VisualElement>("main-screen");
            creditsScreen = root.Q<VisualElement>("credits-screen");
            startButton = root.Q<Button>("start-button");
            creditsButton = root.Q<Button>("credits-button");
            quitButton = root.Q<Button>("quit-button");
            backButton = root.Q<Button>("back-button");
            oceanCreditButton = root.Q<Button>("ocean-credit-button");

            startButton.clicked += StartGame;
            creditsButton.clicked += ShowCredits;
            quitButton.clicked += QuitGame;
            backButton.clicked += ShowMainMenu;
            oceanCreditButton.clicked += OpenOceanCredit;
            root.RegisterCallback<KeyDownEvent>(HandleKeyDown);
            root.RegisterCallback<GeometryChangedEvent>(HandleGeometryChanged);

            ShowMainMenu();
        }

        private void OnDisable()
        {
            if (startButton != null) startButton.clicked -= StartGame;
            if (creditsButton != null) creditsButton.clicked -= ShowCredits;
            if (quitButton != null) quitButton.clicked -= QuitGame;
            if (backButton != null) backButton.clicked -= ShowMainMenu;
            if (oceanCreditButton != null) oceanCreditButton.clicked -= OpenOceanCredit;
            VisualElement root = document != null ? document.rootVisualElement : null;
            if (root != null)
            {
                root.UnregisterCallback<KeyDownEvent>(HandleKeyDown);
                root.UnregisterCallback<GeometryChangedEvent>(HandleGeometryChanged);
            }
        }

        private void StartGame()
        {
            VoyageFlow.OpenShipBuilder();
        }

        private void ShowCredits()
        {
            mainScreen.AddToClassList("is-hidden");
            creditsScreen.RemoveFromClassList("is-hidden");
            backButton.Focus();
        }

        private void ShowMainMenu()
        {
            creditsScreen.AddToClassList("is-hidden");
            mainScreen.RemoveFromClassList("is-hidden");
            startButton.Focus();
        }

        private void HandleKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape && !creditsScreen.ClassListContains("is-hidden"))
            {
                ShowMainMenu();
                evt.StopPropagation();
            }
        }

        private void HandleGeometryChanged(GeometryChangedEvent evt)
        {
            bool compact = evt.newRect.width < 900f;
            mainScreen.EnableInClassList("is-compact", compact);
            creditsScreen.EnableInClassList("is-compact", compact);
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static void OpenOceanCredit()
        {
            Application.OpenURL("https://github.com/2Retr0/GodotOceanWaves/");
        }
    }
}
