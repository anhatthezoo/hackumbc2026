using System.Collections.Generic;
using UnityEngine;

public class Ship : MonoBehaviour
{
    [Header("Required Blocks")]
    [SerializeField] private Block coreBlock;
    [SerializeField] private Block kingBlock;

    [Header("Ship Structure")]
    [SerializeField] private List<Block> blocks = new List<Block>();
    [SerializeField] private int blockCount;

    public Block CoreBlock => coreBlock;
    public Block KingBlock => kingBlock;
    public IReadOnlyList<Block> Blocks => blocks;
    public int BlockCount => blockCount;
    public bool IsAlive => CheckBoatLife();

    private void Reset()
    {
        coreBlock = GetComponent<Block>();
        CollectAttachedBlocks();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            CollectAttachedBlocks();
        }
    }

    private void Awake()
    {
        if (coreBlock == null)
        {
            coreBlock = GetComponent<Block>();
        }

        RefreshBlocks();

        if (coreBlock == null)
        {
            Debug.LogError("A Ship requires a core Block on the same GameObject.", this);
        }

        if (kingBlock == null)
        {
            Debug.LogWarning("Assign the King's Block in the Ship component.", this);
        }
    }

    private void OnDestroy()
    {
        UnsubscribeFromBlocks();
    }

    [ContextMenu("Refresh Blocks")]
    public void RefreshBlocks()
    {
        UnsubscribeFromBlocks();
        CollectAttachedBlocks();

        foreach (Block block in blocks)
        {
            if (block != null)
            {
                block.Destroyed += HandleBlockDestroyed;
            }
        }
    }

    public void RegisterBlock(Block block)
    {
        if (block == null || blocks.Contains(block))
        {
            return;
        }

        blocks.Add(block);
        block.Destroyed += HandleBlockDestroyed;
        blockCount = blocks.Count;
    }

    public void UnregisterBlock(Block block)
    {
        if (block == null || !blocks.Remove(block))
        {
            return;
        }

        block.Destroyed -= HandleBlockDestroyed;
        blockCount = blocks.Count;
    }

    public bool CheckBoatLife()
    {
        return coreBlock != null
            && coreBlock.IsAlive
            && kingBlock != null
            && kingBlock.IsAlive;
    }

    private void HandleBlockDestroyed(Block destroyedBlock)
    {
        UnregisterBlock(destroyedBlock);
    }

    private void CollectAttachedBlocks()
    {
        blocks = new List<Block>(GetComponentsInChildren<Block>(true));
        blockCount = blocks.Count;
    }

    private void UnsubscribeFromBlocks()
    {
        foreach (Block block in blocks)
        {
            if (block != null)
            {
                block.Destroyed -= HandleBlockDestroyed;
            }
        }
    }
}
