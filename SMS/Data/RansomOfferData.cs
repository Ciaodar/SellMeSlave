using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace SMS.Data
{
    public class RansomOfferData
    {
        [SaveableProperty(1)] public Hero Hero { get; set; }
        [SaveableProperty(2)] public int OfferAmount { get; set; }
        [SaveableProperty(3)] public CampaignTime ResolutionTime { get; set; }
        [SaveableProperty(4)] public bool IsInTransit { get; set; }
        [SaveableProperty(5)] public CampaignTime DeliveryTime { get; set; }

        public RansomOfferData(Hero hero, int offerAmount, CampaignTime resolutionTime)
        {
            Hero = hero;
            OfferAmount = offerAmount;
            ResolutionTime = resolutionTime;
            IsInTransit = false;
        }
    }
}
