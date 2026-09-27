using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoyaltyBoat.UI
{
    [Serializable]
    public sealed class ShopProduct
    {
        [SerializeField] private string id = "basic-block";
        [SerializeField] private string displayName = "Wooden Block";
        [SerializeField] private string category = "STRUCTURE";
        [SerializeField, TextArea] private string description =
            "A reliable chunk of timber for hulls, walls, and questionable ideas.";
        [SerializeField, Min(0)] private int price = 25;
        [SerializeField] private GameObject prefab;

        public string Id => id;
        public string DisplayName => displayName;
        public string Category => category;
        public string Description => description;
        public int Price => price;
        public GameObject Prefab => prefab;
    }

    [CreateAssetMenu(
        fileName = "ShopCatalog",
        menuName = "Royalty Boat/Shop Catalog")]
    public sealed class ShopCatalog : ScriptableObject
    {
        [SerializeField] private List<ShopProduct> products = new();

        public IReadOnlyList<ShopProduct> Products => products;
    }
}
