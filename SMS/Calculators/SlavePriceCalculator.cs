using SMS.Config;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace SMS.Calculators
{
    public static class SlavePriceCalculator
    {
        public static int CalculateTotalCost(TroopRoster prisoners)
        {
            int total = 0;
            foreach (var element in prisoners.GetTroopRoster())
            {
                total += CalculateUnitPrice(element.Character) * element.Number;
            }
            return total;
        }

        public static int CalculateUnitPrice(CharacterObject character, int? baseRansom = null)
        {
            if (character.IsHero)
            {
                return CalculateLordPrice(character.HeroObject, baseRansom);
            }

            // (character.Tier + 1) * 50 as base
            float basePrice = (character.Tier + 1) * 50;
            
            // Apply SlavePriceMultiplier (1-10)
            basePrice *= (SmsSettingsManager.SlavePriceMultiplier / 4.0f); // Normalize by 4 if 4 is the base expectation

            // Apply RNG (±10%) if enabled
            if (SmsSettingsManager.EnableRandomization)
            {
                int seed = (int)CampaignTime.Now.ToDays + character.StringId.GetHashCode();
                System.Random rand = new System.Random(seed);
                float randomFactor = 0.9f + ((float)rand.NextDouble() * 0.2f);
                basePrice *= randomFactor;
            }

            return (int)basePrice;
        }

        public static int CalculateLordPrice(Hero hero, int? baseRansom = null)
        {
            // Use provided baseRansom or query a fresh DefaultRansomValueCalculationModel 
            // to avoid querying Campaign.Current.Models which might be our own overridden model right now!
            int nativeRansom = baseRansom ?? new TaleWorlds.CampaignSystem.GameComponents.DefaultRansomValueCalculationModel().PrisonerRansomValue(hero.CharacterObject);
            
            // Apply our multiplier (default 4x)
            float finalPrice = nativeRansom * SmsSettingsManager.LordPriceMultiplier;

            // Apply RNG (±10%) if enabled
            if (SmsSettingsManager.EnableRandomization)
            {
                int seed = (int)CampaignTime.Now.ToDays + hero.StringId.GetHashCode();
                System.Random rand = new System.Random(seed);
                float randomFactor = 0.9f + ((float)rand.NextDouble() * 0.2f);
                finalPrice *= randomFactor;
            }

            return (int)finalPrice;
        }
    }
}
