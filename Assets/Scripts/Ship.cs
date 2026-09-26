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
    [SerializeField, Min(0.01f)] private float attachmentGridSize = 1f;
    [Tooltip("How close a released block must be to a valid neighboring tile.")]
    [SerializeField, Min(0f)] private float attachmentSnapDistance = 0.6f;

    public Block CoreBlock => coreBlock;
    public Block KingBlock => kingBlock;
    public IReadOnlyList<Block> Blocks => blocks;
    public int BlockCount => blockCount;
    public float AttachmentGridSize => attachmentGridSize;
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

    private void Start()
    {
        AttachTouchingBlocks();
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
            SubscribeToBlock(block);
        }
    }

    public void RegisterBlock(Block block)
    {
        if (block == null || blocks.Contains(block))
        {
            return;
        }

        blocks.Add(block);
        SubscribeToBlock(block);
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

    public bool ContainsBlock(Block block)
    {
        return block != null && blocks.Contains(block);
    }

    public bool IsAttachmentPositionAvailable(Vector3 position, Block ignoredBlock)
    {
        return !IsPositionOccupied(position, ignoredBlock);
    }

    public bool AttachBlock(Block block)
    {
        if (block == null || block == coreBlock || block.GetComponent<Ship>() != null)
        {
            return false;
        }

        Ship previousShip = block.GetComponentInParent<Ship>();

        if (previousShip != null && previousShip != this)
        {
            previousShip.DetachBlock(block);
        }

        block.transform.SetParent(transform, true);
        RegisterBlock(block);
        return true;
    }

    public bool DetachBlock(Block block)
    {
        if (block == null || block == coreBlock || !blocks.Contains(block))
        {
            return false;
        }

        UnregisterBlock(block);
        block.transform.SetParent(null, true);
        return true;
    }

    public bool TryAttachBlock(Block block)
    {
        if (!TryFindAttachmentPosition(block, out Vector3 attachmentPosition))
        {
            return false;
        }

        block.transform.position = attachmentPosition;
        block.transform.rotation = transform.rotation;
        return AttachBlock(block);
    }

    [ContextMenu("Attach Touching Blocks")]
    public void AttachTouchingBlocks()
    {
        List<Block> candidates = new List<Block>(
            Object.FindObjectsByType<Block>(FindObjectsInactive.Exclude));

        bool attachedBlock;

        do
        {
            attachedBlock = false;

            for (int i = candidates.Count - 1; i >= 0; i--)
            {
                Block candidate = candidates[i];

                if (candidate == null || blocks.Contains(candidate))
                {
                    candidates.RemoveAt(i);
                    continue;
                }

                if (TryAttachBlock(candidate))
                {
                    candidates.RemoveAt(i);
                    attachedBlock = true;
                }
            }
        }
        while (attachedBlock);
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

        if (destroyedBlock != null
            && destroyedBlock != coreBlock
            && destroyedBlock.transform.IsChildOf(transform))
        {
            destroyedBlock.transform.SetParent(null, true);
        }
    }

    private bool TryFindAttachmentPosition(Block candidate, out Vector3 attachmentPosition)
    {
        attachmentPosition = default;

        if (candidate == null || blocks.Contains(candidate))
        {
            return false;
        }

        Vector3[] directions =
        {
            transform.right,
            -transform.right,
            transform.forward,
            -transform.forward,
            transform.up,
            -transform.up
        };

        float closestDistanceSquared = attachmentSnapDistance * attachmentSnapDistance;
        bool foundPosition = false;

        foreach (Block attachedBlock in blocks)
        {
            if (attachedBlock == null)
            {
                continue;
            }

            foreach (Vector3 direction in directions)
            {
                Vector3 possiblePosition =
                    attachedBlock.transform.position + direction * attachmentGridSize;

                if (IsPositionOccupied(possiblePosition, candidate))
                {
                    continue;
                }

                float distanceSquared =
                    (candidate.transform.position - possiblePosition).sqrMagnitude;

                if (distanceSquared <= closestDistanceSquared)
                {
                    closestDistanceSquared = distanceSquared;
                    attachmentPosition = possiblePosition;
                    foundPosition = true;
                }
            }
        }

        if (foundPosition)
        {
            return true;
        }

        return TryFindTopOfColumn(candidate, out attachmentPosition);
    }

    private bool TryFindTopOfColumn(Block candidate, out Vector3 stackPosition)
    {
        stackPosition = default;
        Block closestColumnBlock = null;
        float closestHorizontalDistanceSquared =
            attachmentSnapDistance * attachmentSnapDistance;

        foreach (Block attachedBlock in blocks)
        {
            if (attachedBlock == null)
            {
                continue;
            }

            Vector3 difference =
                candidate.transform.position - attachedBlock.transform.position;
            Vector3 horizontalDifference =
                Vector3.ProjectOnPlane(difference, transform.up);
            float horizontalDistanceSquared = horizontalDifference.sqrMagnitude;

            if (horizontalDistanceSquared <= closestHorizontalDistanceSquared)
            {
                closestHorizontalDistanceSquared = horizontalDistanceSquared;
                closestColumnBlock = attachedBlock;
            }
        }

        if (closestColumnBlock == null)
        {
            return false;
        }

        Block topBlock = closestColumnBlock;
        float highestPoint = Vector3.Dot(topBlock.transform.position, transform.up);

        foreach (Block attachedBlock in blocks)
        {
            if (attachedBlock == null)
            {
                continue;
            }

            Vector3 columnDifference =
                attachedBlock.transform.position - closestColumnBlock.transform.position;
            float horizontalDistance =
                Vector3.ProjectOnPlane(columnDifference, transform.up).magnitude;
            float height = Vector3.Dot(attachedBlock.transform.position, transform.up);

            if (horizontalDistance <= attachmentSnapDistance && height > highestPoint)
            {
                highestPoint = height;
                topBlock = attachedBlock;
            }
        }

        stackPosition = topBlock.transform.position + transform.up * attachmentGridSize;
        return !IsPositionOccupied(stackPosition, candidate);
    }

    private bool IsPositionOccupied(Vector3 position, Block ignoredBlock)
    {
        float occupiedDistance = attachmentGridSize * 0.1f;
        float occupiedDistanceSquared = occupiedDistance * occupiedDistance;

        foreach (Block block in blocks)
        {
            if (block != null
                && block != ignoredBlock
                && (block.transform.position - position).sqrMagnitude
                    <= occupiedDistanceSquared)
            {
                return true;
            }
        }

        return false;
    }

    private void CollectAttachedBlocks()
    {
        blocks = new List<Block>(GetComponentsInChildren<Block>(true));
        blockCount = blocks.Count;
    }

    private void SubscribeToBlock(Block block)
    {
        if (block == null)
        {
            return;
        }

        block.Destroyed -= HandleBlockDestroyed;
        block.Destroyed += HandleBlockDestroyed;
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
