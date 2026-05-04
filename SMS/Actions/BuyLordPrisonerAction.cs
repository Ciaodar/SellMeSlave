using System;
using SMS.Behaviors;
using SMS.Calculators;
using SMS.Config;
using SMS.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace SMS.Actions
{
    /// <summary>
    /// Handles purchasing a lord prisoner. This involves:
    /// 1. Finding the lord's current location
    /// 2. Calculating distance-based delivery time
    /// 3. Deducting gold from the player
    /// 4. Removing the lord from their current captor
    /// 5. Creating a pending delivery entry
    /// 6. Notifying the player via InquiryPopup
    /// </summary>
    public static class BuyLordPrisonerAction
    {
        public static void Apply(Hero lord)
        {
            if (lord == null || !lord.IsPrisoner)
                return;

            int price = SlavePriceCalculator.CalculateLordPrice(lord);

            if (Hero.MainHero.Gold < price)
            {
                MBInformationManager.AddQuickInformation(
                    new TextObject("{=sms_not_enough_gold}You don't have enough gold to buy this lord."));
                return;
            }

            // Calculate delivery time based on distance
            float deliveryHours = CalculateDeliveryHours(lord);
            CampaignTime deliveryTime = CampaignTime.Now + CampaignTime.Hours(deliveryHours);

            // Deduct gold
            Hero.MainHero.ChangeHeroGold(-price);

            // Apply criminal consequences
            BuySlaveBehavior.Instance?.ApplyCriminalConsequences(price);

            // Remove from current captor — we put them in a "transit" state
            // The lord stays as prisoner in game state but we track delivery separately
            PartyBase captorParty = lord.PartyBelongedToAsPrisoner;
            if (captorParty != null && captorParty.PrisonRoster.Contains(lord.CharacterObject))
            {
                captorParty.PrisonRoster.RemoveTroop(lord.CharacterObject);
            }

            // Set lord to a transit state (still prisoner but no party)
            lord.ChangeState(Hero.CharacterStates.Prisoner);

            // Register the pending delivery
            LordDeliveryData delivery = new LordDeliveryData(lord, CampaignTime.Now, deliveryTime, price);
            BuySlaveBehavior.Instance?.AddPendingDelivery(delivery);

            // Show inquiry to player
            int estimatedHoursInt = (int)Math.Ceiling(deliveryHours);
            float estimatedDays = deliveryHours / 24f;

            TextObject titleText = new TextObject("{=sms_lord_purchased_title}Lord Purchased");
            TextObject bodyText = new TextObject(
                "{=sms_lord_purchased_body}{LORD_NAME} has been purchased for {PRICE}{GOLD_ICON}.\n\n" +
                "Estimated delivery time: {HOURS} hours (~{DAYS} days).\n\n" +
                "Warning: The prisoner may attempt to escape during transit!");
            bodyText.SetTextVariable("LORD_NAME", lord.Name);
            bodyText.SetTextVariable("PRICE", price);
            bodyText.SetTextVariable("HOURS", estimatedHoursInt);
            bodyText.SetTextVariable("DAYS", string.Format("{0:F1}", estimatedDays));

            InformationManager.ShowInquiry(
                new InquiryData(
                    titleText.ToString(),
                    bodyText.ToString(),
                    true, false,
                    new TextObject("{=sms_ok}Understood").ToString(),
                    string.Empty,
                    null, null),
                true);
        }

        /// <summary>
        /// Calculates the delivery time in hours based on the lord's location and distance to the player.
        /// </summary>
        private static float CalculateDeliveryHours(Hero lord)
        {
            Vec2 playerPosition = MobileParty.MainParty.GetPosition2D;
            Vec2 lordPosition;

            // Get lord's position from their captor
            PartyBase captorParty = lord.PartyBelongedToAsPrisoner;
            if (captorParty != null)
            {
                if (captorParty.IsMobile)
                    lordPosition = captorParty.MobileParty.GetPosition2D;
                else if (captorParty.IsSettlement)
                    lordPosition = captorParty.Settlement.GetPosition2D;
                else
                    lordPosition = playerPosition; // fallback
            }
            else
            {
                // Lord might be in a settlement directly
                Settlement stayingSettlement = lord.StayingInSettlement ?? lord.CurrentSettlement;
                lordPosition = stayingSettlement?.GetPosition2D ?? playerPosition;
            }

            float distance = playerPosition.Distance(lordPosition);

            // Base: ~0.2 hours per map unit, adjusted by config multiplier
            // Minimum 6 hours delivery
            float hours = MathF.Max(6f, distance * 0.2f * SmsSettingsManager.LordDeliverySpeedMultiplier);
            return hours;
        }
    }
}
