using System;
using UnityEngine;

namespace RoyaltyBoat.Inventory
{
    public sealed class BlockInventory : MonoBehaviour
    {
        [SerializeField, Min(0)] private int blockCount;

        public int BlockCount => blockCount;

        public event Action<int> BlockCountChanged;

        public void AddBlock()
        {
            AddBlocks(1);
        }

        public void AddBlocks(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            blockCount += amount;
            BlockCountChanged?.Invoke(blockCount);
        }
    }
}
