using RoyaltyBoat.Flow;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace RoyaltyBoat.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class ShipBuildingFlowController : MonoBehaviour
    {
        private Button setSailButton;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneLoadedHandler()
        {
            SceneManager.sceneLoaded -= Bootstrap;
            SceneManager.sceneLoaded += Bootstrap;
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
        }

        private void OnEnable()
        {
            setSailButton = GetComponent<UIDocument>().rootVisualElement.Q<Button>("set-sail-button");
            if (setSailButton == null)
            {
                Debug.LogError("Ship-building UI is missing set-sail-button.", this);
                return;
            }

            setSailButton.clicked += HandleSetSailClicked;
            setSailButton.Focus();
        }

        private void OnDisable()
        {
            if (setSailButton != null)
            {
                setSailButton.clicked -= HandleSetSailClicked;
            }
        }

        private void HandleSetSailClicked()
        {
            SetSail();
        }

        public bool SetSail()
        {
            Ship ship = FindAnyObjectByType<Ship>();
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
