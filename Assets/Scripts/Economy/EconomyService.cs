using System;
using UnityEngine;

namespace RoyaltyBoat.Economy
{
    [DefaultExecutionOrder(-1000)]
    public sealed class EconomyService : MonoBehaviour, IEconomyService
    {
        [SerializeField, Min(0)] private int startingBalance = 500;
        [SerializeField] private bool persistBetweenScenes = true;

        public static EconomyService Instance { get; private set; }

        public int Balance { get; private set; }

        public event Action<int, int> BalanceChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            Balance = Mathf.Max(0, startingBalance);
            EconomyAccess.Install(this);

            if (persistBetweenScenes)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (Instance != this)
            {
                return;
            }

            EconomyAccess.Uninstall(this);
            Instance = null;
        }

        public bool CanAfford(int amount)
        {
            return amount >= 0 && Balance >= amount;
        }

        public bool TrySpend(int amount)
        {
            if (!CanAfford(amount))
            {
                return false;
            }

            SetBalance(Balance - amount);
            return true;
        }

        public void AddFunds(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            SetBalance(checked(Balance + amount));
        }

        public void ResetBalance(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            SetBalance(amount);
        }

        private void SetBalance(int value)
        {
            int previous = Balance;
            Balance = value;
            BalanceChanged?.Invoke(previous, Balance);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureServiceExists()
        {
            if (Instance != null || FindAnyObjectByType<EconomyService>() != null)
            {
                return;
            }

            var serviceObject = new GameObject("EconomyService (Auto)");
            serviceObject.AddComponent<EconomyService>();
        }
    }
}
