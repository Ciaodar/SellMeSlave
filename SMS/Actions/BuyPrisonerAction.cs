using System.Collections.Generic;
using SMS.Calculators;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using SMS.Interop;
using TaleWorlds.CampaignSystem.Settlements;

namespace SMS.Actions
{
    /// <summary>
    /// Handles purchasing regular (non-hero) prisoner troops.
    /// Deducts gold from the player and adds the troops directly to the player's PrisonRoster.
    /// </summary>
    public static class BuyPrisonerAction
    {
        public static void Apply(TroopRoster purchasedPrisoners, Settlement? currentSettlement = null)
        {
            if (purchasedPrisoners == null || purchasedPrisoners.TotalManCount == 0)
                return;

            int totalCost = SlavePriceCalculator.CalculateTotalCost(purchasedPrisoners);

            // Deduct gold
            Hero.MainHero.ChangeHeroGold(-totalCost);

            // Add prisoners to the player's prison roster
            foreach (TroopRosterElement element in (List<TroopRosterElement>)purchasedPrisoners.GetTroopRoster())
            {
                if (!element.Character.IsHero)
                {
                    PartyBase.MainParty.AddPrisoner(element.Character, element.Number);
                }
            }

            // Interop event for 3rd party mods
            SmsInteropEvents.RaiseSlavePurchased(new SmsSlavePurchaseRecord
            {
                BuyerHeroId = Hero.MainHero.StringId,
                IsLordPurchase = false,
                PurchasedLordId = null,
                PurchasedTroopCount = purchasedPrisoners.TotalManCount,
                GoldPaid = totalCost,
                SettlementId = currentSettlement?.StringId ?? MobileParty.MainParty?.CurrentSettlement?.StringId,
                CampaignTimeDays = (float)CampaignTime.Now.ToDays
            });

            // Show notification
            TextObject message = new TextObject("{=sms_bought_prisoners}You purchased {COUNT} prisoners for {COST}{GOLD_ICON}.");
            message.SetTextVariable("COUNT", purchasedPrisoners.TotalManCount);
            message.SetTextVariable("COST", totalCost);
            MBInformationManager.AddQuickInformation(message);
        }

        /// <summary>
        /// Calculates total cost of given prisoners for UI display.
        /// </summary>
        public static int GetTotalCost(TroopRoster prisoners)
        {
            return SlavePriceCalculator.CalculateTotalCost(prisoners);
        }
    }
}
