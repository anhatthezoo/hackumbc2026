using System;

namespace RoyaltyBoat.Economy
{
    /// <summary>
    /// Small contract shared by purchasing, rewards, repairs, and UI.
    /// The implementation can be replaced without changing callers.
    /// </summary>
    public interface IEconomyService
    {
        int Balance { get; }

        event Action<int, int> BalanceChanged;

        bool CanAfford(int amount);
        bool TrySpend(int amount);
        void AddFunds(int amount);
        void ResetBalance(int amount);
    }
}
