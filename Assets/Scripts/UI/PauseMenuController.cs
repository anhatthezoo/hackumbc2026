using RoyaltyBoat.Flow;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace RoyaltyBoat.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class PauseMenuController : MonoBehaviour
    {
        private UIDocument document;
        private VisualElement overlay;
        private VisualElement pauseScreen;
        private VisualElement settingsScreen;
        private VisualElement creditsScreen;
        private Button resumeButton;
        private Button settingsButton;
        private Button creditsButton;
        private Button settingsBackButton;
        private Button creditsBackButton;
        private VisualSettingsUIBinder settingsBinder;
        private float previousTimeScale = 1f;
        private bool isOpen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneHandler()
        {
            SceneManager.sceneLoaded -= Bootstrap;
            SceneManager.sceneLoaded += Bootstrap;
        }

        private static void Bootstrap(Scene scene, LoadSceneMode mode)
        {
            bool isMainMenu = scene.name == VoyageFlow.GameplaySceneName &&
                              !VoyageFlow.IsVoyageActive;
            if (isMainMenu || FindAnyObjectByType<PauseMenuController>() != null)
            {
                return;
            }

            VisualTreeAsset layout = Resources.Load<VisualTreeAsset>("PauseMenu/PauseMenu");
            PanelSettings panelSettings = Resources.Load<PanelSettings>("MainMenu/MainMenuPanelSettings");
            if (layout == null || panelSettings == null)
            {
                Debug.LogError("Pause menu UI assets could not be loaded.");
                return;
            }

            GameObject host = new GameObject("Pause Menu");
            UIDocument uiDocument = host.AddComponent<UIDocument>();
            uiDocument.panelSettings = panelSettings;
            uiDocument.visualTreeAsset = layout;
            uiDocument.sortingOrder = 200;
            host.AddComponent<PauseMenuController>();
        }

        private void Awake()
        {
            document = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            VisualElement root = document.rootVisualElement;
            overlay = root.Q<VisualElement>("pause-overlay");
            pauseScreen = root.Q<VisualElement>("pause-screen");
            settingsScreen = root.Q<VisualElement>("pause-settings-screen");
            creditsScreen = root.Q<VisualElement>("pause-credits-screen");
            resumeButton = root.Q<Button>("resume-button");
            settingsButton = root.Q<Button>("pause-settings-button");
            creditsButton = root.Q<Button>("pause-credits-button");
            settingsBackButton = root.Q<Button>("pause-settings-back-button");
            creditsBackButton = root.Q<Button>("pause-credits-back-button");

            resumeButton.clicked += CloseMenu;
            settingsButton.clicked += ShowSettings;
            creditsButton.clicked += ShowCredits;
            settingsBackButton.clicked += ShowPauseScreen;
            creditsBackButton.clicked += ShowPauseScreen;
            settingsBinder = new VisualSettingsUIBinder(root);
            overlay.AddToClassList("is-hidden");
        }

        private void OnDisable()
        {
            if (resumeButton != null) resumeButton.clicked -= CloseMenu;
            if (settingsButton != null) settingsButton.clicked -= ShowSettings;
            if (creditsButton != null) creditsButton.clicked -= ShowCredits;
            if (settingsBackButton != null) settingsBackButton.clicked -= ShowPauseScreen;
            if (creditsBackButton != null) creditsBackButton.clicked -= ShowPauseScreen;
            settingsBinder?.Dispose();
            settingsBinder = null;
            RestoreTimeScale();
        }

        private void Update()
        {
            if (Keyboard.current?.escapeKey.wasPressedThisFrame != true)
            {
                return;
            }

            if (!isOpen)
            {
                OpenMenu();
                return;
            }

            bool subScreenOpen = !settingsScreen.ClassListContains("is-hidden") ||
                                 !creditsScreen.ClassListContains("is-hidden");
            if (subScreenOpen)
            {
                ShowPauseScreen();
            }
            else
            {
                CloseMenu();
            }
        }

        public void OpenMenu()
        {
            if (isOpen)
            {
                return;
            }

            isOpen = true;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            overlay.RemoveFromClassList("is-hidden");
            ShowPauseScreen();
        }

        public void CloseMenu()
        {
            if (!isOpen)
            {
                return;
            }

            overlay.AddToClassList("is-hidden");
            RestoreTimeScale();
        }

        private void ShowPauseScreen()
        {
            settingsScreen.AddToClassList("is-hidden");
            creditsScreen.AddToClassList("is-hidden");
            pauseScreen.RemoveFromClassList("is-hidden");
            resumeButton.Focus();
        }

        private void ShowSettings()
        {
            pauseScreen.AddToClassList("is-hidden");
            creditsScreen.AddToClassList("is-hidden");
            settingsScreen.RemoveFromClassList("is-hidden");
            settingsBinder.FocusPrimaryControl();
        }

        private void ShowCredits()
        {
            pauseScreen.AddToClassList("is-hidden");
            settingsScreen.AddToClassList("is-hidden");
            creditsScreen.RemoveFromClassList("is-hidden");
            creditsBackButton.Focus();
        }

        private void RestoreTimeScale()
        {
            if (isOpen)
            {
                Time.timeScale = previousTimeScale;
                isOpen = false;
            }
        }
    }
}
