using SMS.Calculators;
using SMS.Menu;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;

namespace SMS.Models
{
    public class SmsRansomValueCalculationModel : DefaultRansomValueCalculationModel
    {
        public override int PrisonerRansomValue(CharacterObject prisoner, Hero? sellerHero = null)
        {
            if (SlaveTradeScreenManager.IsTradeScreenActive)
            {
                // We are buying, use our custom formula. 
                // Pass base ransom to prevent infinite recursion loop
                return SlavePriceCalculator.CalculateUnitPrice(prisoner, base.PrisonerRansomValue(prisoner, sellerHero));
            }
            return base.PrisonerRansomValue(prisoner, sellerHero);
        }
    }
}
