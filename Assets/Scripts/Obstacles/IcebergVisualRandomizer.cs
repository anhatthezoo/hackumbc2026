using UnityEngine;

namespace RoyaltyBoat.Obstacles
{
    [DisallowMultipleComponent]
    public sealed class IcebergVisualRandomizer : MonoBehaviour
    {
        [SerializeField] private GameObject[] variants = System.Array.Empty<GameObject>();
        [SerializeField] private bool randomizeRotation = true;

        public int VariantCount => variants?.Length ?? 0;

        public void Configure(GameObject[] visualVariants)
        {
            variants = visualVariants ?? System.Array.Empty<GameObject>();
        }

        private void Awake()
        {
            Randomize();
        }

        public void Randomize()
        {
            if (variants == null || variants.Length == 0)
            {
                return;
            }

            int selectedIndex = Random.Range(0, variants.Length);
            for (int index = 0; index < variants.Length; index++)
            {
                GameObject variant = variants[index];
                if (variant != null)
                {
                    variant.SetActive(index == selectedIndex);
                }
            }

            GameObject selected = variants[selectedIndex];
            if (selected != null && randomizeRotation)
            {
                selected.transform.localRotation =
                    Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            }
        }
    }
}
