using System;
using System.Collections.Generic;
using System.Linq;
using SMS.Calculators;
using SMS.Config;
using SMS.Data;
using SMS.Interop;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace SMS.Behaviors
{
    public class AiBuySlaveBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // No persistent data specifically for this behavior
        }

        private void OnSettlementEntered(MobileParty mobileParty, Settlement settlement, Hero hero)
        {
            if (!SmsSettingsManager.EnableAiSlaveTrade) return;

            // Ensure it's an AI lord party
            if (mobileParty == null || !mobileParty.IsActive || !mobileParty.IsLordParty) return;
            if (hero == null || hero == Hero.MainHero || hero.Clan == Hero.MainHero.Clan) return;

            // 10% chance to do anything
            if (MBRandom.RandomFloat > SmsSettingsManager.AiSettlementPurchaseChance) return;

            int honor = hero.GetTraitLevel(DefaultTraits.Honor);

            // 1. Try to buy Lord Prisoners (Requires lower honor, default -1)
            if (honor <= SmsSettingsManager.AiMaxHonorForLordPurchase)
            {
                TryBuyLordFromSettlement(mobileParty, settlement, hero);
            }

            // 2. Try to buy Troop Prisoners (Requires default 0)
            if (honor <= SmsSettingsManager.AiMaxHonorForTroopPurchase)
            {
                TryBuyTroopsFromSettlement(mobileParty, settlement, hero);
            }
        }

        private void TryBuyLordFromSettlement(MobileParty party, Settlement settlement, Hero buyer)
        {
            // Find an enemy lord from anywhere in the world (broker network)
            var enemyLords = Hero.AllAliveHeroes
                .Where(h => h.IsPrisoner && h != Hero.MainHero && h.PartyBelongedToAsPrisoner != null)
                .Where(h => FactionManager.IsAtWarAgainstFaction(buyer.MapFaction, h.MapFaction))
                .ToList();

            if (!enemyLords.Any()) return;

            // Pick random enemy lord
            Hero targetLord = enemyLords.GetRandomElement();
            int price = SlavePriceCalculator.CalculateLordPrice(targetLord);

            if (buyer.Gold >= price)
            {
                // Buy them
                buyer.ChangeHeroGold(-price);
                
                // Remove from their current captor
                if (targetLord.PartyBelongedToAsPrisoner != null)
                {
                    targetLord.PartyBelongedToAsPrisoner.PrisonRoster.RemoveTroop(targetLord.CharacterObject);
                }
                targetLord.ChangeState(Hero.CharacterStates.Prisoner);

                // Add to pending delivery
                float deliveryHours = MathF.Max(6f, party.GetPosition2D.Distance(settlement.GetPosition2D) * 0.2f * SmsSettingsManager.LordDeliverySpeedMultiplier);
                CampaignTime deliveryTime = CampaignTime.Now + CampaignTime.Hours(deliveryHours);
                
                LordDeliveryData delivery = new LordDeliveryData(targetLord, CampaignTime.Now, deliveryTime, price, buyer);
                BuySlaveBehavior.Instance?.AddPendingDelivery(delivery);

                // Interop
                SmsInteropEvents.RaiseSlavePurchased(new SmsSlavePurchaseRecord
                {
                    BuyerHeroId = buyer.StringId,
                    IsLordPurchase = true,
                    PurchasedLordId = targetLord.StringId,
                    PurchasedTroopCount = 1,
                    GoldPaid = price,
                    SettlementId = settlement.StringId,
                    CampaignTimeDays = (float)CampaignTime.Now.ToDays
                });
            }
        }

        private void TryBuyTroopsFromSettlement(MobileParty party, Settlement settlement, Hero buyer)
        {
            if (BuySlaveBehavior.Instance == null) return;

            int space = party.Party.PartySizeLimit - party.Party.NumberOfAllMembers;
            if (space <= 0) return;

            TroopRoster stock = BuySlaveBehavior.Instance.GetTroopStock(settlement);
            if (stock == null || stock.TotalManCount <= 0) return;

            int troopsToBuy = Math.Min(space, stock.TotalManCount);
            if (troopsToBuy <= 0) return;

            TroopRoster purchased = TroopRoster.CreateDummyTroopRoster();
            int currentCost = 0;

            // Try to buy random troops
            var elements = stock.GetTroopRoster().ToList();
            foreach (var element in elements)
            {
                if (purchased.TotalManCount >= troopsToBuy) break;

                int numberToTake = Math.Min(element.Number, troopsToBuy - purchased.TotalManCount);
                int costPerTroop = SlavePriceCalculator.CalculateUnitPrice(element.Character);

                // Check gold limit
                int maxCanAfford = buyer.Gold > currentCost ? (buyer.Gold - currentCost) / costPerTroop : 0;
                numberToTake = Math.Min(numberToTake, maxCanAfford);

                if (numberToTake > 0)
                {
                    purchased.AddToCounts(element.Character, numberToTake);
                    currentCost += (costPerTroop * numberToTake);
                }
            }

            if (purchased.TotalManCount > 0)
            {
                buyer.ChangeHeroGold(-currentCost);
                
                // Add to buyer's party as normal members
                party.Party.MemberRoster.Add(purchased);

                // Remove from stock
                TroopRoster remaining = TroopRoster.CreateDummyTroopRoster();
                remaining.Add(stock);
                foreach (var p in purchased.GetTroopRoster())
                {
                    remaining.RemoveTroop(p.Character, p.Number);
                }
                BuySlaveBehavior.Instance.UpdateTroopStock(settlement, remaining);

                // Interop
                SmsInteropEvents.RaiseSlavePurchased(new SmsSlavePurchaseRecord
                {
                    BuyerHeroId = buyer.StringId,
                    IsLordPurchase = false,
                    PurchasedLordId = null,
                    PurchasedTroopCount = purchased.TotalManCount,
                    GoldPaid = currentCost,
                    SettlementId = settlement.StringId,
                    CampaignTimeDays = (float)CampaignTime.Now.ToDays
                });
            }
        }

        private void OnHourlyTick()
        {
            if (!SmsSettingsManager.EnableAiSlaveTrade) return;

            // Global 5% chance
            if (MBRandom.RandomFloat > SmsSettingsManager.AiHourlyTradeChance) return;

            // Perform AI to AI trade
            PerformHourlyAiTrade();
        }

        private void PerformHourlyAiTrade()
        {
            var allActiveLords = Hero.AllAliveHeroes.Where(h => 
                h.IsActive && 
                h != Hero.MainHero && 
                h.Clan != Hero.MainHero.Clan && 
                h.PartyBelongedTo != null && 
                h.PartyBelongedTo.IsActive).ToList();

            // 1. Find potential buyers (Honor <= -1)
            var buyers = allActiveLords.Where(h => h.GetTraitLevel(DefaultTraits.Honor) <= SmsSettingsManager.AiMaxHonorForLordPurchase).ToList();
            if (!buyers.Any()) return;

            // 2. Find potential sellers (Has Lord Prisoners)
            var sellers = allActiveLords.Where(h => h.PartyBelongedTo.PrisonRoster.TotalHeroes > 0).ToList();
            if (!sellers.Any()) return;

            // Randomly shuffle to find a matching pair
            buyers.Shuffle();
            sellers.Shuffle();

            foreach (Hero buyer in buyers)
            {
                foreach (Hero seller in sellers)
                {
                    if (buyer == seller) continue;

                    // Get enemy lords in seller's party
                    var suitablePrisoners = seller.PartyBelongedTo.PrisonRoster.GetTroopRoster()
                        .Where(t => t.Character.IsHero)
                        .Select(t => t.Character.HeroObject)
                        .Where(p => FactionManager.IsAtWarAgainstFaction(buyer.MapFaction, p.MapFaction))
                        .ToList();

                    if (!suitablePrisoners.Any()) continue;

                    Hero targetPrisoner = suitablePrisoners.GetRandomElement();
                    int price = SlavePriceCalculator.CalculateLordPrice(targetPrisoner);

                    if (buyer.Gold >= price)
                    {
                        // TRADE HAPPENS!
                        buyer.ChangeHeroGold(-price);
                        seller.ChangeHeroGold(price); // Seller gets the money

                        // Remove from seller
                        seller.PartyBelongedTo.PrisonRoster.RemoveTroop(targetPrisoner.CharacterObject);
                        targetPrisoner.ChangeState(Hero.CharacterStates.Prisoner);

                        // Calculate distance and delivery time
                        float deliveryHours = MathF.Max(6f, buyer.PartyBelongedTo.GetPosition2D.Distance(seller.PartyBelongedTo.GetPosition2D) * 0.2f * SmsSettingsManager.LordDeliverySpeedMultiplier);
                        CampaignTime deliveryTime = CampaignTime.Now + CampaignTime.Hours(deliveryHours);
                        
                        LordDeliveryData delivery = new LordDeliveryData(targetPrisoner, CampaignTime.Now, deliveryTime, price, buyer);
                        BuySlaveBehavior.Instance?.AddPendingDelivery(delivery);

                        // Interop
                        SmsInteropEvents.RaiseSlavePurchased(new SmsSlavePurchaseRecord
                        {
                            BuyerHeroId = buyer.StringId,
                            IsLordPurchase = true,
                            PurchasedLordId = targetPrisoner.StringId,
                            PurchasedTroopCount = 1,
                            GoldPaid = price,
                            SettlementId = null, // Field trade
                            CampaignTimeDays = (float)CampaignTime.Now.ToDays
                        });

                        return; // Only one trade per hour max
                    }
                }
            }
        }
    }
}
