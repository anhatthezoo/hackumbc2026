using UnityEngine;

/// <summary>
/// Marks an ordinary structural block as a valid seat for the King.
/// The block keeps all normal health, dragging, snapping, and ship behavior.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Block))]
public sealed class ChairSeat : MonoBehaviour
{
    [SerializeField] private Transform seatAnchor;

    public bool TryGetSeatPose(out Vector3 position, out Quaternion rotation)
    {
        if (seatAnchor == null)
        {
            position = default;
            rotation = default;
            return false;
        }

        position = seatAnchor.position;
        rotation = seatAnchor.rotation;
        return true;
    }
}
