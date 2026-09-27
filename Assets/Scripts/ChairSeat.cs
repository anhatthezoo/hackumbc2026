using UnityEngine;

/// <summary>
/// Marks an ordinary structural block as a valid seat for the King.
/// The block keeps all normal health, dragging, snapping, and ship behavior.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Block))]
public sealed class ChairSeat : MonoBehaviour
{
}
