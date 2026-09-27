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
        private Label balanceShadowLabel;
        private VisualElement moneyTextStack;
        private Label statusLabel;
        private VisualElement detailCard;
        private Image detailThumbnail;
        private Label detailName;
        private Label detailCost;
        private Label detailDescription;
        private readonly List<Label> detailFeatures = new();
        private IVisualElementScheduledItem detailHideSchedule;
        private IEconomyService economy;
        private ProductThumbnailRenderer thumbnailRenderer;
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
            balanceShadowLabel = root.Q<Label>("money-balance-shadow");
            moneyTextStack = root.Q<VisualElement>(className: "money-text-stack");
            statusLabel = root.Q<Label>("shop-status");
            detailCard = root.Q<VisualElement>("partDetail");
            detailThumbnail = root.Q<Image>("detailThumbnail");
            detailName = root.Q<Label>("detailName");
            detailCost = root.Q<Label>("detailCost");
            detailDescription = root.Q<Label>("detailDescription");
            detailFeatures.Clear();
            for (int index = 0; index < 4; index++)
            {
                detailFeatures.Add(root.Q<Label>($"detailFeature{index}"));
            }
            thumbnailRenderer = new ProductThumbnailRenderer();

            ScrollView partsRail = root.Q<ScrollView>("partsRail");
            if (partsRail != null)
            {
                partsRail.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                partsRail.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            }

            EconomyAccess.ServiceChanged += HandleServiceChanged;
            BindEconomy(EconomyAccess.Current);
            BuildProductList();
            PlayEntrance(root);
        }

        private void OnDisable()
        {
            EconomyAccess.ServiceChanged -= HandleServiceChanged;
            BindEconomy(null);
            ClearPurchaseBindings();
            detailHideSchedule?.Pause();
            thumbnailRenderer?.Dispose();
            thumbnailRenderer = null;
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
            string formattedBalance = $"{(economy?.Balance ?? 0):N0}";
            if (balanceLabel != null)
            {
                balanceLabel.text = formattedBalance;
            }

            if (balanceShadowLabel != null)
            {
                balanceShadowLabel.text = formattedBalance;
            }

            UpdateBalanceWidth(formattedBalance);

            foreach (PurchaseBinding binding in purchaseBindings)
            {
                binding.Button.SetEnabled(
                    binding.Product.Prefab != null);
            }
        }

        private void UpdateBalanceWidth(string formattedBalance)
        {
            if (moneyTextStack == null)
            {
                return;
            }

            moneyTextStack.RemoveFromClassList("balance-width-one");
            moneyTextStack.RemoveFromClassList("balance-width-short");
            moneyTextStack.RemoveFromClassList("balance-width-medium");
            moneyTextStack.RemoveFromClassList("balance-width-long");

            string widthClass = formattedBalance.Length switch
            {
                1 => "balance-width-one",
                <= 3 => "balance-width-short",
                <= 6 => "balance-width-medium",
                _ => "balance-width-long"
            };
            moneyTextStack.AddToClassList(widthClass);
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

            for (int index = 0; index < catalog.Products.Count; index++)
            {
                itemList.Add(CreateProductButton(catalog.Products[index], index));
            }

            SetStatus(string.Empty, false);
            RefreshBalance();
        }

        private Button CreateProductButton(ShopProduct product, int index)
        {
            var button = new Button
            {
                name = $"buy-{product.Id}",
                tooltip = $"{product.DisplayName} · {product.Price:N0} coins"
            };
            button.AddToClassList("part-button");
            button.AddToClassList("is-entering");

            var preview = new Image
            {
                image = thumbnailRenderer.Render(product.Prefab),
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            preview.AddToClassList("part-thumbnail");
            button.Add(preview);

            button.RegisterCallback<PointerEnterEvent>(_ => ShowDetail(product));
            button.RegisterCallback<PointerLeaveEvent>(_ => HideDetailAfterDelay());
            button.RegisterCallback<MouseEnterEvent>(_ => ShowDetail(product));
            button.RegisterCallback<MouseLeaveEvent>(_ => HideDetailAfterDelay());
            button.RegisterCallback<FocusInEvent>(_ => ShowDetail(product));
            button.RegisterCallback<FocusOutEvent>(_ => HideDetailAfterDelay());

            Action handler = () => Purchase(product);
            button.clicked += handler;
            purchaseBindings.Add(new PurchaseBinding
            {
                Product = product,
                Button = button,
                Handler = handler
            });

            button.schedule.Execute(
                () => button.RemoveFromClassList("is-entering"))
                .StartingIn(120 + index * 75);
            return button;
        }

        private void ShowDetail(ShopProduct product)
        {
            if (detailCard == null)
            {
                return;
            }

            detailHideSchedule?.Pause();
            detailThumbnail.image = thumbnailRenderer.Render(product.Prefab);
            detailName.text = product.DisplayName;
            detailCost.text = $"Cost: {product.Price:N0}";
            detailDescription.text = product.Description;

            IReadOnlyList<string> features = GetDetailFeatures(product);
            for (int index = 0; index < detailFeatures.Count; index++)
            {
                if (detailFeatures[index] != null)
                {
                    detailFeatures[index].text = $"•  {features[index]}";
                }
            }

            detailCard.AddToClassList("part-detail-visible");
        }

        private void HideDetailAfterDelay()
        {
            if (detailCard == null)
            {
                return;
            }

            detailHideSchedule?.Pause();
            detailHideSchedule = detailCard.schedule.Execute(
                () => detailCard.RemoveFromClassList("part-detail-visible"))
                .StartingIn(100);
        }

        private static IReadOnlyList<string> GetDetailFeatures(ShopProduct product)
        {
            var features = new List<string>
            {
                $"{product.Category} component"
            };
            Block block = product.Prefab == null
                ? null
                : product.Prefab.GetComponentInChildren<Block>();

            if (block == null)
            {
                features.Add("Royal support item");
                features.Add("Ready for your build");
            }
            else
            {
                features.Add($"{block.MaxHealth} durability");
                features.Add(block.DamageReduction > 0
                    ? $"{block.DamageReduction} impact armor"
                    : "Lightweight construction");
            }

            features.Add("Buy, then drag to build");
            return features;
        }

        private void Purchase(ShopProduct product)
        {
            if (product.Prefab == null)
            {
                SetStatus($"{product.DisplayName} is not ready to place.", true);
                return;
            }

            economy ??= EconomyAccess.Current;

            if (economy == null || !economy.TrySpend(product.Price))
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
            FindAnyObjectByType<ShipBuildArea>()?.ConfigureLoosePart(purchasedPart);
            SetStatus($"{product.DisplayName} added to the floor.", false);
            RefreshBalance();
        }

        private Vector3 FindOpenSpawnPosition()
        {
            ShipBuildArea buildArea = FindAnyObjectByType<ShipBuildArea>();
            Vector3 center = buildArea != null && buildArea.Platform != null
                ? buildArea.Platform.position
                : Vector3.zero;
            float gridSize = buildArea == null
                ? Ship.DefaultAttachmentGridSize
                : buildArea.AttachmentGridSize;
            float floorHeight = buildArea == null
                ? 0f
                : buildArea.GetBuildFloorHeight();

            const int width = 5;
            const int slots = 25;

            for (int attempt = 0; attempt < slots; attempt++)
            {
                int index = (spawnSequence + attempt) % slots;
                int column = index % width;
                int row = index / width;
                Vector3 candidate = new(
                    center.x + (column - 2) * gridSize,
                    floorHeight + gridSize * 0.5f,
                    center.z + (row - 2) * gridSize);

                Collider[] overlaps = Physics.OverlapBox(
                    candidate,
                    Vector3.one * (gridSize * 0.48f),
                    Quaternion.identity,
                    ~0,
                    QueryTriggerInteraction.Ignore);
                bool occupied = false;
                foreach (Collider overlap in overlaps)
                {
                    if (overlap.GetComponentInParent<Block>() != null)
                    {
                        occupied = true;
                        break;
                    }
                }

                if (!occupied)
                {
                    spawnSequence = index + 1;
                    return candidate;
                }
            }

            spawnSequence++;
            return new Vector3(
                center.x,
                floorHeight + gridSize * (0.5f + spawnSequence),
                center.z);
        }

        private void SetStatus(string message, bool isError)
        {
            if (statusLabel == null)
            {
                return;
            }

            statusLabel.text = message;
            bool visible = !string.IsNullOrWhiteSpace(message);
            statusLabel.EnableInClassList("shop-status-visible", visible);
            statusLabel.EnableInClassList("shop-status-error", isError);

            if (visible)
            {
                statusLabel.schedule.Execute(() =>
                    statusLabel.RemoveFromClassList("shop-status-visible"))
                    .StartingIn(2200);
            }
        }

        private static void PlayEntrance(VisualElement root)
        {
            root.schedule.Execute(() =>
            {
                root.Q<Label>("buildTitle")?.RemoveFromClassList("screen-enter");
                root.Q<Label>("buildTitleShadow")?.RemoveFromClassList("screen-enter");
            }).StartingIn(50);

            root.schedule.Execute(() =>
                root.Q<ScrollView>("partsRail")?.RemoveFromClassList("screen-enter"))
                .StartingIn(110);

            root.schedule.Execute(() =>
                root.Q<VisualElement>("moneyHud")?.RemoveFromClassList("screen-enter"))
                .StartingIn(170);

            root.schedule.Execute(() =>
                root.Q<Button>("set-sail-button")?.RemoveFromClassList("screen-enter"))
                .StartingIn(230);
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
