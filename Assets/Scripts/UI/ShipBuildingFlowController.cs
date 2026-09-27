using RoyaltyBoat.Flow;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace RoyaltyBoat.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class ShipBuildingFlowController : MonoBehaviour
    {
        private Button setSailButton;
        private Button cameraAngleButton;
        private CameraOrbitController orbitController;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneLoadedHandler()
        {
            SceneManager.sceneLoaded -= Bootstrap;
            SceneManager.sceneLoaded += Bootstrap;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapActiveScene()
        {
            Bootstrap(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void Bootstrap(Scene scene, LoadSceneMode loadMode)
        {
            if (scene.name != VoyageFlow.ShipBuildingSceneName ||
                FindAnyObjectByType<ShipBuildingFlowController>() != null)
            {
                return;
            }

            VisualTreeAsset layout = Resources.Load<VisualTreeAsset>("ShipBuilding/ShipBuildingFlow");
            PanelSettings panelSettings = Resources.Load<PanelSettings>("MainMenu/MainMenuPanelSettings");
            if (layout == null || panelSettings == null)
            {
                Debug.LogError("Ship-building flow UI assets could not be loaded from Resources.");
                return;
            }

            GameObject host = new GameObject("Ship Building Flow UI");
            UIDocument document = host.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = layout;
            document.sortingOrder = 100;
            host.AddComponent<ShipBuildingFlowController>();
            host.AddComponent<ShopUIController>();
        }

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            setSailButton = root.Q<Button>("set-sail-button");
            cameraAngleButton = root.Q<Button>("camera-angle-button");
            if (setSailButton == null)
            {
                Debug.LogError("Ship-building UI is missing set-sail-button.", this);
                return;
            }

            setSailButton.clicked += HandleSetSailClicked;
            if (cameraAngleButton != null)
            {
                cameraAngleButton.clicked += HandleCameraAngleClicked;
            }

            Camera mainCamera = Camera.main;
            orbitController = mainCamera == null
                ? null
                : mainCamera.GetComponent<CameraOrbitController>();
            if (orbitController != null)
            {
                orbitController.SetBuildSide(BuildViewSide.NearSide, true);
            }

            UpdateCameraSideLabel();
            setSailButton.Focus();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.vKey.wasPressedThisFrame)
            {
                ToggleCameraAngle();
            }
        }

        private void OnDisable()
        {
            if (setSailButton != null)
            {
                setSailButton.clicked -= HandleSetSailClicked;
            }

            if (cameraAngleButton != null)
            {
                cameraAngleButton.clicked -= HandleCameraAngleClicked;
            }

        }

        private void HandleSetSailClicked()
        {
            SetSail();
        }

        private void HandleCameraAngleClicked()
        {
            ToggleCameraAngle();
        }

        private void ToggleCameraAngle()
        {
            orbitController?.FlipBuildSide();
        }

        private void UpdateCameraSideLabel()
        {
            if (cameraAngleButton != null)
            {
                cameraAngleButton.text = "FLIP SIDE  •  V";
            }
        }

        public bool SetSail()
        {
            ShipBuildArea buildArea = FindAnyObjectByType<ShipBuildArea>();
            Ship ship = buildArea != null
                ? buildArea.PrepareShipForLaunch()
                : FindAnyObjectByType<Ship>();
            if (ship == null)
            {
                Debug.LogError("No Ship was found in the ship-building scene.", this);
                return false;
            }

            setSailButton?.SetEnabled(false);
            if (VoyageFlow.LaunchBuiltShip(ship))
            {
                return true;
            }

            setSailButton?.SetEnabled(true);
            return false;
        }
    }
}
