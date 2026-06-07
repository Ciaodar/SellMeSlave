using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace SMS.Data
{
    /// <summary>
    /// Tracks a purchased lord prisoner that is being delivered to the player.
    /// Distance-based delivery time and daily escape chance apply until delivery completes.
    /// </summary>
    public class LordDeliveryData
    {
        [SaveableField(1)]
        public Hero Lord;

        [SaveableField(2)]
        public CampaignTime PurchaseTime;

        [SaveableField(3)]
        public CampaignTime EstimatedDeliveryTime;

        [SaveableField(4)]
        public int PurchasePrice;

        [SaveableField(5)]
        public Hero BuyerHero;

        public LordDeliveryData() { Lord = null!; BuyerHero = null!; }

        public LordDeliveryData(Hero lord, CampaignTime purchaseTime, CampaignTime deliveryTime, int price, Hero? buyerHero = null)
        {
            Lord = lord;
            PurchaseTime = purchaseTime;
            EstimatedDeliveryTime = deliveryTime;
            PurchasePrice = price;
            // Default to main hero for backwards compatibility
            BuyerHero = buyerHero ?? Hero.MainHero;
        }

        public float GetRemainingHours()
        {
            float remaining = EstimatedDeliveryTime.RemainingHoursFromNow;
            return remaining > 0f ? remaining : 0f;
        }

        public bool IsReadyForDelivery()
        {
            return CampaignTime.Now >= EstimatedDeliveryTime;
        }
    }
}
