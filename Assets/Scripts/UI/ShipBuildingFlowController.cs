using RoyaltyBoat.Economy;
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
        private Label moneyBalance;
        private IEconomyService economy;

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
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            setSailButton = root.Q<Button>("set-sail-button");
            moneyBalance = root.Q<Label>("money-balance");
            EconomyAccess.ServiceChanged += HandleEconomyServiceChanged;
            BindEconomy(EconomyAccess.Current);

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
            EconomyAccess.ServiceChanged -= HandleEconomyServiceChanged;
            if (economy != null)
            {
                economy.BalanceChanged -= HandleBalanceChanged;
                economy = null;
            }

            if (setSailButton != null)
            {
                setSailButton.clicked -= HandleSetSailClicked;
            }
        }

        private void HandleEconomyServiceChanged(IEconomyService service)
        {
            BindEconomy(service);
        }

        private void BindEconomy(IEconomyService service)
        {
            if (economy != null)
            {
                economy.BalanceChanged -= HandleBalanceChanged;
            }

            economy = service;
            if (economy != null)
            {
                economy.BalanceChanged += HandleBalanceChanged;
                UpdateMoneyBalance(economy.Balance);
            }
        }

        private void HandleBalanceChanged(int previousBalance, int currentBalance)
        {
            UpdateMoneyBalance(currentBalance);
        }

        private void UpdateMoneyBalance(int balance)
        {
            if (moneyBalance != null)
            {
                moneyBalance.text = $"COINS  {balance}";
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
