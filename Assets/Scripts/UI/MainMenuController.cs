using RoyaltyBoat.Flow;
using RoyaltyBoat.Audio;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace RoyaltyBoat.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class MainMenuController : MonoBehaviour
    {
        private UIDocument document;
        private VisualElement mainScreen;
        private VisualElement settingsScreen;
        private VisualElement creditsScreen;
        private Button startButton;
        private Button settingsButton;
        private Button creditsButton;
        private Button quitButton;
        private Button settingsBackButton;
        private Button creditsBackButton;
        private Button oceanCreditButton;
        private VisualSettingsUIBinder settingsBinder;

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
            GameAudio.BindUi(root);
            mainScreen = root.Q<VisualElement>("main-screen");
            settingsScreen = root.Q<VisualElement>("settings-screen");
            creditsScreen = root.Q<VisualElement>("credits-screen");
            startButton = root.Q<Button>("start-button");
            settingsButton = root.Q<Button>("settings-button");
            creditsButton = root.Q<Button>("credits-button");
            quitButton = root.Q<Button>("quit-button");
            settingsBackButton = root.Q<Button>("settings-back-button");
            creditsBackButton = root.Q<Button>("credits-back-button");
            oceanCreditButton = root.Q<Button>("ocean-credit-button");

            startButton.clicked += StartGame;
            settingsButton.clicked += ShowSettings;
            creditsButton.clicked += ShowCredits;
            quitButton.clicked += QuitGame;
            settingsBackButton.clicked += ShowMainMenu;
            creditsBackButton.clicked += ShowMainMenu;
            oceanCreditButton.clicked += OpenOceanCredit;
            root.RegisterCallback<GeometryChangedEvent>(HandleGeometryChanged);

            settingsBinder = new VisualSettingsUIBinder(root);
            ShowMainMenu();
        }

        private void OnDisable()
        {
            if (startButton != null) startButton.clicked -= StartGame;
            if (settingsButton != null) settingsButton.clicked -= ShowSettings;
            if (creditsButton != null) creditsButton.clicked -= ShowCredits;
            if (quitButton != null) quitButton.clicked -= QuitGame;
            if (settingsBackButton != null) settingsBackButton.clicked -= ShowMainMenu;
            if (creditsBackButton != null) creditsBackButton.clicked -= ShowMainMenu;
            if (oceanCreditButton != null) oceanCreditButton.clicked -= OpenOceanCredit;
            settingsBinder?.Dispose();
            settingsBinder = null;
            VisualElement root = document != null ? document.rootVisualElement : null;
            if (root != null)
            {
                root.UnregisterCallback<GeometryChangedEvent>(HandleGeometryChanged);
            }
        }

        private void Update()
        {
            if (Keyboard.current?.escapeKey.wasPressedThisFrame != true)
            {
                return;
            }

            bool subScreenOpen = !settingsScreen.ClassListContains("is-hidden") ||
                                 !creditsScreen.ClassListContains("is-hidden");
            if (subScreenOpen)
            {
                ShowMainMenu();
            }
        }

        private void StartGame()
        {
            VoyageFlow.OpenShipBuilder();
        }

        private void ShowCredits()
        {
            mainScreen.AddToClassList("is-hidden");
            settingsScreen.AddToClassList("is-hidden");
            creditsScreen.RemoveFromClassList("is-hidden");
            creditsBackButton.Focus();
        }

        private void ShowSettings()
        {
            mainScreen.AddToClassList("is-hidden");
            creditsScreen.AddToClassList("is-hidden");
            settingsScreen.RemoveFromClassList("is-hidden");
            settingsBinder.FocusPrimaryControl();
        }

        private void ShowMainMenu()
        {
            settingsScreen.AddToClassList("is-hidden");
            creditsScreen.AddToClassList("is-hidden");
            mainScreen.RemoveFromClassList("is-hidden");
            startButton.Focus();
        }

        private void HandleGeometryChanged(GeometryChangedEvent evt)
        {
            bool compact = evt.newRect.width < 900f;
            mainScreen.EnableInClassList("is-compact", compact);
            settingsScreen.EnableInClassList("is-compact", compact);
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
