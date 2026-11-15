using SMS.Calculators;
using SMS.Menu;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;

namespace SMS.Models
{
    public class SmsRansomValueCalculationModel : DefaultRansomValueCalculationModel
    {
        public override int PrisonerRansomValue(CharacterObject prisoner, Hero? sellerHero = null)
        {
            if (SlaveTradeScreenManager.IsTradeScreenActive)
            {
                // We are buying, use our custom formula.
                // Resolve actual captor as sellerHero so v1.5.7 MercyLordRansomEffect and RansomBroker perks
                // reflect the seller rather than Hero.MainHero.
                Hero? effectiveSeller = prisoner.IsHero ? SlavePriceCalculator.GetCaptorSellerHero(prisoner.HeroObject) : null;
                return SlavePriceCalculator.CalculateUnitPrice(prisoner, base.PrisonerRansomValue(prisoner, effectiveSeller));
            }

            if (prisoner.IsHero &&
                prisoner.HeroObject != null &&
                prisoner.HeroObject.IsPrisoner &&
                prisoner.HeroObject.PartyBelongedToAsPrisoner != null &&
                prisoner.HeroObject.PartyBelongedToAsPrisoner != PartyBase.MainParty &&
                sellerHero == Hero.MainHero)
            {
                sellerHero = SlavePriceCalculator.GetCaptorSellerHero(prisoner.HeroObject);
            }

            return base.PrisonerRansomValue(prisoner, sellerHero);
        }
    }
}
