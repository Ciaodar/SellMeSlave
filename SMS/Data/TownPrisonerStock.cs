using System;
using System.Collections.Generic;
using System.Linq;
using SMS.Config;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem;

namespace SMS.Data
{
    /// <summary>
    /// Manages per-town prisoner stock generation, caching and expiration.
    /// Stock is culture-based with tier-weighted rarity and persists via SyncData.
    /// </summary>
    public class TownPrisonerStock
    {
        [SaveableField(1)]
        private Dictionary<string, TroopRoster> _stockByTownId = new Dictionary<string, TroopRoster>();

        [SaveableField(2)]
        private Dictionary<string, CampaignTime> _stockCreationTime = new Dictionary<string, CampaignTime>();

        /// <summary>
        /// Gets existing stock or generates new stock for the given town settlement.
        /// </summary>
        public TroopRoster GetOrCreateStock(Settlement settlement)
        {
            string townId = settlement.StringId;

            if (_stockByTownId.TryGetValue(townId, out TroopRoster existing) &&
                _stockCreationTime.TryGetValue(townId, out CampaignTime creationTime))
            {
                // Check if stock has expired
                float daysSinceCreation = creationTime.ElapsedDaysUntilNow;
                if (daysSinceCreation < SmsSettingsManager.StockExpirationDays)
                {
                    return existing;
                }

                // Expired - remove and regenerate
                _stockByTownId.Remove(townId);
                _stockCreationTime.Remove(townId);
            }

            TroopRoster newStock = GenerateStock(settlement);
            _stockByTownId[townId] = newStock;
            _stockCreationTime[townId] = CampaignTime.Now;
            return newStock;
        }

        /// <summary>
        /// Updates the stock after a purchase (removes purchased troops from the cached roster).
        /// </summary>
        public void UpdateStock(Settlement settlement, TroopRoster remainingStock)
        {
            string townId = settlement.StringId;
            _stockByTownId[townId] = remainingStock;
        }

        /// <summary>
        /// Removes all expired stocks from the cache (called on daily tick).
        /// </summary>
        public void RemoveExpiredStocks()
        {
            List<string> expired = new List<string>();
            foreach (var kvp in _stockCreationTime)
            {
                if (kvp.Value.ElapsedDaysUntilNow >= SmsSettingsManager.StockExpirationDays)
                {
                    expired.Add(kvp.Key);
                }
            }

            foreach (string townId in expired)
            {
                _stockByTownId.Remove(townId);
                _stockCreationTime.Remove(townId);
            }
        }

        /// <summary>
        /// Clears all stock data. Used for mod removal / data cleanup via config.
        /// </summary>
        public void ClearAllStock()
        {
            _stockByTownId.Clear();
            _stockCreationTime.Clear();
        }

        /// <summary>
        /// Checks if stock exists and has troops for a given settlement.
        /// </summary>
        public bool HasStock(Settlement settlement)
        {
            if (!_stockByTownId.TryGetValue(settlement.StringId, out TroopRoster stock))
                return false;
            return stock.TotalManCount > 0;
        }

        private TroopRoster GenerateStock(Settlement settlement)
        {
            TroopRoster roster = TroopRoster.CreateDummyTroopRoster();
            var culture = settlement.Culture;

            // Gather only real military troops from this culture's troop tree
            // Filter strictly: must be soldier occupation, not a hero, not a template,
            // and must be part of the regular troop tree (IsBasicTroop or IsRegular)
            List<CharacterObject> cultureTroops = CharacterObject.All
                .Where(c => !c.IsHero &&
                            !c.IsTemplate &&
                            c.Culture == culture &&
                            c.Occupation == Occupation.Soldier &&
                            (c.IsBasicTroop || c.IsRegular) &&
                            c.Tier >= 0 &&
                            !c.StringId.Contains("dummy") &&
                            !c.StringId.Contains("_child") &&
                            !c.StringId.Contains("_noncom") &&
                            !c.StringId.Contains("tournament") &&
                            !c.StringId.Contains("merchant") &&
                            !c.StringId.Contains("_lady"))
                .ToList();

            if (cultureTroops.Count == 0)
            {
                // Narrower fallback: any soldier-occupation troop from the culture
                cultureTroops = CharacterObject.All
                    .Where(c => !c.IsHero &&
                                !c.IsTemplate &&
                                c.Culture == culture &&
                                c.Occupation == Occupation.Soldier &&
                                c.Tier >= 1)
                    .ToList();
            }

            if (cultureTroops.Count == 0)
                return roster;

            int minSlaves = SmsSettingsManager.MinSlavesPerBroker;
            int maxSlaves = SmsSettingsManager.MaxSlavesPerBroker;
            int totalCapacity = MBRandom.RandomInt(minSlaves, maxSlaves + 1);
            int currentCount = 0;

            while (currentCount < totalCapacity && cultureTroops.Count > 0)
            {
                CharacterObject? selected = SelectTroopByTierWeight(cultureTroops);
                if (selected != null)
                {
                    // Calculate batch size inversely proportional to tier: (7 - tier) * random(1,9) / 3
                    // Higher tier means fewer troops, lower tier means more troops.
                    int batchSize = Math.Max(1, (7 - selected.Tier) * MBRandom.RandomInt(1, 10) / 3);
                    
                    if (currentCount + batchSize > totalCapacity)
                    {
                        batchSize = totalCapacity - currentCount;
                    }

                    roster.AddToCounts(selected, batchSize);
                    currentCount += batchSize;
                }
                else
                {
                    break;
                }
            }

            return roster;
        }

        /// <summary>
        /// Weighted random selection: higher tier = rarer.
        /// Tier 0-1: weight 50, Tier 2-3: weight 30, Tier 4-5: weight 15, Tier 6+: weight 5
        /// </summary>
        private CharacterObject? SelectTroopByTierWeight(List<CharacterObject> troops)
        {
            if (troops.Count == 0) return null;

            float totalWeight = 0f;
            float[] weights = new float[troops.Count];

            for (int i = 0; i < troops.Count; i++)
            {
                int tier = troops[i].Tier;
                float weight;
                if (tier <= 1) weight = 50f;
                else if (tier <= 3) weight = 30f;
                else if (tier <= 5) weight = 15f;
                else weight = 5f;

                weights[i] = weight;
                totalWeight += weight;
            }

            float roll = MBRandom.RandomFloat * totalWeight;
            float cumulative = 0f;

            for (int i = 0; i < troops.Count; i++)
            {
                cumulative += weights[i];
                if (roll <= cumulative)
                    return troops[i];
            }

            return troops[troops.Count - 1];
        }
    }
}
