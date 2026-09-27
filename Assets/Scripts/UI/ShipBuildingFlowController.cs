using RoyaltyBoat.Flow;
using RoyaltyBoat.Audio;
using RoyaltyBoat.King;
using System;
using System.Collections.Generic;
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
        private Label shipWeightLabel;
        private Label shipCostLabel;
        private VisualElement royalDecreeOverlay;
        private Label royalDecreeMessage;
        private Button royalDecreeButton;
        private CameraOrbitController orbitController;
        private readonly Dictionary<string, int> productPrices = new(StringComparer.Ordinal);
        private string displayedWeight;
        private string displayedCost;

        private static readonly string[] DecreeNames =
        {
            "Business",
            "Harold",
            "Barry",
            "Xeno"
        };

        private static readonly string[] DecreeDescriptions =
        {
            "Oil spills will occur more often",
            "2x more obstacles will spawn every odd round",
            "Gain 50 extra cash every even level.",
            "Lightning spawns more often"
        };

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
            GameAudio.BindUi(root);
            setSailButton = root.Q<Button>("set-sail-button");
            devMapButton = root.Q<Button>("dev-map-button");
            cameraAngleButton = root.Q<Button>("camera-angle-button");
            rotateItemButton = root.Q<Button>("rotate-item-button");
            statusLabel = root.Q<Label>("shop-status");
            shipWeightLabel = root.Q<Label>("ship-weight");
            shipCostLabel = root.Q<Label>("ship-cost");
            royalDecreeOverlay = root.Q<VisualElement>("royal-decree-overlay");
            royalDecreeMessage = root.Q<Label>("royal-decree-message");
            royalDecreeButton = root.Q<Button>("royal-decree-button");
            CacheProductPrices();
            if (setSailButton == null)
            {
                Debug.LogError("Ship-building UI is missing set-sail-button.", this);
                return;
            }

            Label sailLabel = root.Q<Label>("set-sail-label");
            if (sailLabel != null)
            {
                sailLabel.text = "SAIL";
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
            if (royalDecreeButton != null)
            {
                royalDecreeButton.clicked += DismissRoyalDecree;
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
            UpdateShipSummary();
            ShowOpeningDecreeIfNeeded();
        }

        private void Update()
        {
            UpdateShipSummary();
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.vKey.wasPressedThisFrame)
            {
                ToggleCameraAngle();
            }
        }

        private void CacheProductPrices()
        {
            productPrices.Clear();
            ShopCatalog catalog = Resources.Load<ShopCatalog>("ShipBuilding/ShopCatalog");
            if (catalog == null)
            {
                return;
            }

            foreach (ShopProduct product in catalog.Products)
            {
                if (product.Prefab != null)
                {
                    productPrices[product.Prefab.name] = product.Price;
                }
            }
        }

        private void UpdateShipSummary()
        {
            ShipBuildArea buildArea = FindAnyObjectByType<ShipBuildArea>();
            Ship ship = buildArea != null ? buildArea.StartingShip : FindAnyObjectByType<Ship>();
            float mass = ship == null ? 0f : ship.TotalMass;
            int totalCost = 0;

            if (ship != null)
            {
                foreach (Block block in ship.Blocks)
                {
                    if (block == null)
                    {
                        continue;
                    }

                    string blockName = block.gameObject.name.Replace("(Clone)", string.Empty).Trim();
                    if (productPrices.TryGetValue(blockName, out int price))
                    {
                        totalCost += price;
                    }
                }
            }

            string weightText = $"WEIGHT  {mass:0.#} KG";
            string costText = $"TOTAL  {totalCost:N0}";
            if (shipWeightLabel != null && displayedWeight != weightText)
            {
                shipWeightLabel.text = weightText;
                displayedWeight = weightText;
            }
            if (shipCostLabel != null && displayedCost != costText)
            {
                shipCostLabel.text = costText;
                displayedCost = costText;
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

            if (royalDecreeButton != null)
            {
                royalDecreeButton.clicked -= DismissRoyalDecree;
            }

        }

        private void ShowOpeningDecreeIfNeeded()
        {
            if (royalDecreeOverlay == null
                || royalDecreeMessage == null
                || !VoyageFlow.TryConsumeOpeningDecree(out int decreeIndex))
            {
                royalDecreeOverlay?.AddToClassList("royal-decree-hidden");
                setSailButton?.Focus();
                return;
            }

            decreeIndex = Mathf.Clamp(decreeIndex, 0, DecreeNames.Length - 1);
            string description = DecreeDescriptions[decreeIndex].TrimEnd('.');
            royalDecreeMessage.text =
                $"King {DecreeNames[decreeIndex]} has ordered for the exploration of the New World.\n\n{description}.";
            royalDecreeOverlay.RemoveFromClassList("royal-decree-hidden");
            royalDecreeButton?.Focus();
        }

        private void DismissRoyalDecree()
        {
            royalDecreeOverlay?.AddToClassList("royal-decree-hidden");
            setSailButton?.Focus();
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
