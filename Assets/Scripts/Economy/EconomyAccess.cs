using System;
using UnityEngine;

namespace RoyaltyBoat.Economy
{
    /// <summary>
    /// Stable access point for systems that need the economy before the final
    /// game-session architecture exists. Install a different IEconomyService
    /// later to replace the current implementation.
    /// </summary>
    public static class EconomyAccess
    {
        private static IEconomyService current = new RuntimeEconomyStub();

        public static IEconomyService Current => current;
        public static int Balance => current.Balance;

        public static event Action<IEconomyService> ServiceChanged;

        public static bool TrySpend(int amount) => current.TrySpend(amount);
        public static void AddFunds(int amount) => current.AddFunds(amount);

        public static void Install(IEconomyService service)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            if (ReferenceEquals(current, service))
            {
                return;
            }

            current = service;
            ServiceChanged?.Invoke(current);
        }

        public static void Uninstall(IEconomyService service)
        {
            if (!ReferenceEquals(current, service))
            {
                return;
            }

            current = new RuntimeEconomyStub();
            ServiceChanged?.Invoke(current);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            current = new RuntimeEconomyStub();
            ServiceChanged = null;
        }

        private sealed class RuntimeEconomyStub : IEconomyService
        {
            public int Balance { get; private set; }

            public event Action<int, int> BalanceChanged;

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
        }
    }
}
