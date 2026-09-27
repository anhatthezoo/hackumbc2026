using System;
using System.Collections.Generic;
using RoyaltyBoat.Economy;
using UnityEngine;
using UnityEngine.UIElements;

namespace RoyaltyBoat.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class ShopUIController : MonoBehaviour
    {
        private const string CatalogResourcePath = "ShipBuilding/ShopCatalog";

        private readonly List<PurchaseBinding> purchaseBindings = new();
        private UIDocument document;
        private ShopCatalog catalog;
        private VisualElement itemList;
        private Label balanceLabel;
        private Label statusLabel;
        private IEconomyService economy;
        private int spawnSequence;

        private sealed class PurchaseBinding
        {
            public ShopProduct Product;
            public Button Button;
            public Action Handler;
        }

        private void Awake()
        {
            document = GetComponent<UIDocument>();
            catalog = Resources.Load<ShopCatalog>(CatalogResourcePath);
        }

        private void OnEnable()
        {
            document ??= GetComponent<UIDocument>();
            VisualElement root = document.rootVisualElement;
            itemList = root.Q<VisualElement>("shop-items");
            balanceLabel = root.Q<Label>("money-balance");
            statusLabel = root.Q<Label>("shop-status");

            EconomyAccess.ServiceChanged += HandleServiceChanged;
            BindEconomy(EconomyAccess.Current);
            BuildProductList();
        }

        private void OnDisable()
        {
            EconomyAccess.ServiceChanged -= HandleServiceChanged;
            BindEconomy(null);
            ClearPurchaseBindings();
        }

        private void HandleServiceChanged(IEconomyService service)
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
            }

            RefreshBalance();
        }

        private void HandleBalanceChanged(int previousBalance, int newBalance)
        {
            RefreshBalance();
        }

        private void RefreshBalance()
        {
            int balance = economy?.Balance ?? 0;

            if (balanceLabel != null)
            {
                balanceLabel.text = $"${balance:N0}";
            }

            foreach (PurchaseBinding binding in purchaseBindings)
            {
                binding.Button.SetEnabled(
                    binding.Product.Prefab != null
                    && economy != null
                    && economy.CanAfford(binding.Product.Price));
            }
        }

        private void BuildProductList()
        {
            ClearPurchaseBindings();
            itemList?.Clear();

            if (itemList == null)
            {
                Debug.LogError("Ship shop UI is missing shop-items.", this);
                return;
            }

            if (catalog == null || catalog.Products.Count == 0)
            {
                SetStatus("No parts are stocked right now.", true);
                return;
            }

            foreach (ShopProduct product in catalog.Products)
            {
                itemList.Add(CreateProductCard(product));
            }

            SetStatus("Purchased parts appear on the build floor.", false);
            RefreshBalance();
        }

        private VisualElement CreateProductCard(ShopProduct product)
        {
            var card = new VisualElement();
            card.AddToClassList("product-card");

            var preview = new VisualElement();
            preview.AddToClassList("product-preview");
            AddBlockPreview(preview);
            card.Add(preview);

            var copy = new VisualElement();
            copy.AddToClassList("product-copy");

            var category = new Label(product.Category);
            category.AddToClassList("product-category");
            copy.Add(category);

            var name = new Label(product.DisplayName);
            name.AddToClassList("product-name");
            copy.Add(name);

            var description = new Label(product.Description);
            description.AddToClassList("product-description");
            copy.Add(description);

            var purchaseRow = new VisualElement();
            purchaseRow.AddToClassList("purchase-row");

            var price = new Label($"${product.Price:N0}");
            price.AddToClassList("product-price");
            purchaseRow.Add(price);

            var buyButton = new Button { text = "BUY" };
            buyButton.name = $"buy-{product.Id}";
            buyButton.AddToClassList("product-buy-button");
            Action handler = () => Purchase(product);
            buyButton.clicked += handler;
            purchaseBindings.Add(new PurchaseBinding
            {
                Product = product,
                Button = buyButton,
                Handler = handler
            });
            purchaseRow.Add(buyButton);
            copy.Add(purchaseRow);
            card.Add(copy);
            return card;
        }

        private static void AddBlockPreview(VisualElement preview)
        {
            var shadow = new VisualElement();
            shadow.AddToClassList("preview-shadow");
            preview.Add(shadow);

            var top = new VisualElement();
            top.AddToClassList("preview-block-top");
            preview.Add(top);

            var front = new VisualElement();
            front.AddToClassList("preview-block-front");
            preview.Add(front);

            var side = new VisualElement();
            side.AddToClassList("preview-block-side");
            preview.Add(side);
        }

        private void Purchase(ShopProduct product)
        {
            if (product.Prefab == null)
            {
                SetStatus($"{product.DisplayName} is not ready to place.", true);
                return;
            }

            economy ??= EconomyAccess.Current;

            if (!economy.TrySpend(product.Price))
            {
                SetStatus("Not enough royal funds.", true);
                RefreshBalance();
                return;
            }

            Vector3 spawnPosition = FindOpenSpawnPosition();
            GameObject purchasedPart = Instantiate(
                product.Prefab,
                spawnPosition,
                Quaternion.identity);
            purchasedPart.name = product.DisplayName;
            SetStatus($"{product.DisplayName} added to the floor.", false);
            RefreshBalance();
        }

        private Vector3 FindOpenSpawnPosition()
        {
            ShipBuildArea buildArea = FindAnyObjectByType<ShipBuildArea>();
            Vector3 center = buildArea != null && buildArea.Platform != null
                ? buildArea.Platform.position
                : Vector3.zero;

            const int width = 7;
            const int slots = 42;

            for (int attempt = 0; attempt < slots; attempt++)
            {
                int index = (spawnSequence + attempt) % slots;
                int column = index % width;
                int row = index / width;
                Vector3 candidate = center + new Vector3(
                    -3f + column,
                    1f,
                    -3f + row);

                if (!Physics.CheckBox(
                        candidate,
                        Vector3.one * 0.46f,
                        Quaternion.identity,
                        ~0,
                        QueryTriggerInteraction.Ignore))
                {
                    spawnSequence = index + 1;
                    return candidate;
                }
            }

            spawnSequence++;
            return center + new Vector3(0f, 1f + spawnSequence, 0f);
        }

        private void SetStatus(string message, bool isError)
        {
            if (statusLabel == null)
            {
                return;
            }

            statusLabel.text = message;
            statusLabel.EnableInClassList("shop-status-error", isError);
        }

        private void ClearPurchaseBindings()
        {
            foreach (PurchaseBinding binding in purchaseBindings)
            {
                binding.Button.clicked -= binding.Handler;
            }

            purchaseBindings.Clear();
        }
    }
}
