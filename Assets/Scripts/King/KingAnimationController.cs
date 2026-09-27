using UnityEngine;

namespace RoyaltyBoat.King
{
    [DisallowMultipleComponent]
    public sealed class KingAnimationController : MonoBehaviour
    {
        private static readonly int FallingAsIdleHash = Animator.StringToHash("FallingAsIdle");
        private static readonly int IdleStateHash = Animator.StringToHash("Idle");
        private static readonly int FallingIdleStateHash = Animator.StringToHash("Falling Idle");

        [SerializeField] private Animator animator;

        public bool IsFallingIdleActive { get; private set; }

        private void Awake()
        {
            CacheAnimator();
            ApplyIdleState();
        }

        public void ActivateFallingAsIdle()
        {
            IsFallingIdleActive = true;
            ApplyIdleState();
        }

        public void RestoreSittingIdle()
        {
            IsFallingIdleActive = false;
            ApplyIdleState();
        }

        private void CacheAnimator()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(true);
            }
        }

        private void ApplyIdleState()
        {
            CacheAnimator();

            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return;
            }

            animator.SetBool(FallingAsIdleHash, IsFallingIdleActive);
            animator.CrossFade(
                IsFallingIdleActive ? FallingIdleStateHash : IdleStateHash,
                0.1f);
        }
    }
}
