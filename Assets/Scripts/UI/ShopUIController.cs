using RoyaltyBoat.Inventory;
using UnityEngine;
using UnityEngine.UIElements;

namespace RoyaltyBoat.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class ShopUIController : MonoBehaviour
    {
        [SerializeField] private BlockInventory inventory;

        private UIDocument document;
        private Button buyButton;

        private void Awake()
        {
            document = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            document ??= GetComponent<UIDocument>();
            inventory ??= FindAnyObjectByType<BlockInventory>();
            buyButton = document.rootVisualElement.Q<Button>("buy-block-button");

            if (buyButton != null)
            {
                buyButton.clicked += BuyBlock;
            }
        }

        private void OnDisable()
        {
            if (buyButton != null)
            {
                buyButton.clicked -= BuyBlock;
            }
        }

        private void BuyBlock()
        {
            if (inventory == null)
            {
                inventory = FindAnyObjectByType<BlockInventory>();
            }

            inventory?.AddBlock();
        }
    }
}
