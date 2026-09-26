using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class ObstacleDamage : MonoBehaviour
{
    [SerializeField, Min(0)] private int damage = 10;

    public int Damage => damage;
}
