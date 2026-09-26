using UnityEngine;

namespace RoyaltyBoat.Obstacles
{
    public sealed class AcidWaterVisual : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float bobHeight = 0.12f;
        [SerializeField, Min(0f)] private float bobSpeed = 1.7f;

        private Transform[] bubbles;
        private Vector3[] startingPositions;

        private void Awake()
        {
            CacheBubbles();
        }

        private void OnEnable()
        {
            CacheBubbles();
        }

        private void Update()
        {
            if (bubbles == null)
            {
                return;
            }

            for (int i = 0; i < bubbles.Length; i++)
            {
                float phase = i * 1.913f;
                Vector3 position = startingPositions[i];
                position.y += Mathf.Sin(Time.time * bobSpeed + phase) * bobHeight;
                bubbles[i].localPosition = position;
            }
        }

        private void CacheBubbles()
        {
            int childCount = transform.childCount;
            bubbles = new Transform[childCount];
            startingPositions = new Vector3[childCount];

            for (int i = 0; i < childCount; i++)
            {
                bubbles[i] = transform.GetChild(i);
                startingPositions[i] = bubbles[i].localPosition;
            }
        }
    }
}
