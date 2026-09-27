using UnityEngine;

namespace RoyaltyBoat.Economy
{
    public readonly struct LevelRewardResult
    {
        public LevelRewardResult(
            int levelValue,
            int completionPay,
            int healthBonus,
            int decreeBonus = 0)
        {
            LevelValue = levelValue;
            CompletionPay = completionPay;
            HealthBonus = healthBonus;
            DecreeBonus = decreeBonus;
        }

        public int LevelValue { get; }
        public int CompletionPay { get; }
        public int HealthBonus { get; }
        public int DecreeBonus { get; }
        public int TotalReward => CompletionPay + HealthBonus + DecreeBonus;

        public LevelRewardResult WithDecreeBonus(int decreeBonus)
        {
            return new LevelRewardResult(
                LevelValue,
                CompletionPay,
                HealthBonus,
                Mathf.Max(0, decreeBonus));
        }
    }

    public static class LevelRewardCalculator
    {
        private const int FirstLevelValue = 150;
        private const int ValuePerLevel = 45;
        private const float CompletionShare = 0.25f;

        public static LevelRewardResult Calculate(int levelNumber, float normalizedKingHealth)
        {
            int safeLevel = Mathf.Max(1, levelNumber);
            float health = Mathf.Clamp01(normalizedKingHealth);
            int levelValue = FirstLevelValue + (safeLevel - 1) * ValuePerLevel;
            int completionPay = Mathf.RoundToInt(levelValue * CompletionShare);
            int healthBonus = Mathf.RoundToInt(levelValue * (1f - CompletionShare) * health);
            return new LevelRewardResult(levelValue, completionPay, healthBonus);
        }
    }
}
