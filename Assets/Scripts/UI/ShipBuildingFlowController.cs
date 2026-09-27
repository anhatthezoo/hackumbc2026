using RoyaltyBoat.Flow;
using RoyaltyBoat.King;
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
        private Button devMapButton;
        private Button cameraAngleButton;
        private Button rotateItemButton;
        private Label statusLabel;
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
            devMapButton = root.Q<Button>("dev-map-button");
            cameraAngleButton = root.Q<Button>("camera-angle-button");
            rotateItemButton = root.Q<Button>("rotate-item-button");
            statusLabel = root.Q<Label>("shop-status");
            if (setSailButton == null)
            {
                Debug.LogError("Ship-building UI is missing set-sail-button.", this);
                return;
            }

            setSailButton.clicked += HandleSetSailClicked;
            if (devMapButton != null)
            {
                devMapButton.clicked += HandleDevMapClicked;
            }
            if (cameraAngleButton != null)
            {
                cameraAngleButton.clicked += HandleCameraAngleClicked;
            }
            if (rotateItemButton != null)
            {
                rotateItemButton.clicked += HandleRotateItemClicked;
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

            if (rotateItemButton != null)
            {
                rotateItemButton.clicked -= HandleRotateItemClicked;
            }

            if (devMapButton != null)
            {
                devMapButton.clicked -= HandleDevMapClicked;
            }

        }

        private void HandleSetSailClicked()
        {
            SetSail(false);
        }

        private void HandleDevMapClicked()
        {
            SetSail(true);
        }

        private void HandleCameraAngleClicked()
        {
            ToggleCameraAngle();
        }

        private void HandleRotateItemClicked()
        {
            Camera mainCamera = Camera.main;
            BlockDragController dragController = mainCamera == null
                ? null
                : mainCamera.GetComponent<BlockDragController>();
            if (dragController == null || !dragController.RotateSelectionClockwise())
            {
                ShowLaunchError("Select an item with enough room to rotate it.");
            }
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
            return SetSail(false);
        }

        private bool SetSail(bool useDevMap)
        {
            ShipBuildArea buildArea = FindAnyObjectByType<ShipBuildArea>();
            Ship ship = buildArea != null
                ? buildArea.PrepareShipForLaunch()
                : FindAnyObjectByType<Ship>();
            if (ship == null)
            {
                Debug.LogError("No Ship was found in the ship-building scene.", this);
                ShowLaunchError("Place at least one ship part on the build floor.");
                return false;
            }

            if (!ship.IsAlive)
            {
                ShowLaunchError("Place at least one ship part on the build floor.");
                return false;
            }

            if (!ship.AreAllBlocksConnected(out int disconnectedBlockCount))
            {
                string noun = disconnectedBlockCount == 1 ? "part is" : "parts are";
                ShowLaunchError(
                    $"{disconnectedBlockCount} {noun} disconnected. Join every part before launch.");
                return false;
            }

            KingBuildPlacement king = buildArea != null
                ? buildArea.KingPlacement
                : FindAnyObjectByType<KingBuildPlacement>();
            if (king == null)
            {
                ShowLaunchError("The King is missing from the shipyard.");
                return false;
            }

            if (!king.IsPlaced || king.SupportingShip != ship)
            {
                ShowLaunchError("Place the King on a connected chair before launch.");
                return false;
            }

            setSailButton?.SetEnabled(false);
            devMapButton?.SetEnabled(false);
            if (VoyageFlow.LaunchBuiltShip(ship, useDevMap))
            {
                return true;
            }

            setSailButton?.SetEnabled(true);
            devMapButton?.SetEnabled(true);
            return false;
        }

        private void ShowLaunchError(string message)
        {
            if (statusLabel == null)
            {
                return;
            }

            statusLabel.text = message;
            statusLabel.AddToClassList("shop-status-error");
        }
    }
}
