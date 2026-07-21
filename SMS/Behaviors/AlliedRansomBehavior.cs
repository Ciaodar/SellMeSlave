using System;
using System.Collections.Generic;
using SMS.Calculators;
using SMS.Config;
using SMS.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace SMS.Behaviors
{
    public class AlliedRansomBehavior : CampaignBehaviorBase
    {
        private List<RansomOfferData> _offers = new List<RansomOfferData>();
        private List<Hero> _trackedHeroes = new List<Hero>();

        public static AlliedRansomBehavior? Instance => Campaign.Current?.GetCampaignBehavior<AlliedRansomBehavior>();

        public override void RegisterEvents()
        {
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
            CampaignEvents.HeroPrisonerReleased.AddNonSerializedListener(this, OnHeroPrisonerReleased);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("_sms_ransomOffers", ref _offers);
            dataStore.SyncData("_sms_trackedRansomHeroes", ref _trackedHeroes);

            if (_offers == null) _offers = new List<RansomOfferData>();
            if (_trackedHeroes == null) _trackedHeroes = new List<Hero>();
        }

        public bool HasActiveOrPastOffer(Hero hero)
        {
            return _trackedHeroes.Contains(hero);
        }

        public void AddOffer(Hero hero, int offerAmount)
        {
            if (HasActiveOrPastOffer(hero)) return;

            // resolution is 4 to 24 hours
            float waitHours = MBRandom.RandomFloatRanged(4f, 24f);
            CampaignTime resTime = CampaignTime.Now + CampaignTime.Hours(waitHours);

            RansomOfferData offer = new RansomOfferData(hero, offerAmount, resTime);
            _offers.Add(offer);
            _trackedHeroes.Add(hero);

            // Deduct gold
            Hero.MainHero.ChangeHeroGold(-offerAmount);
        }

        private void OnHeroPrisonerReleased(Hero hero, PartyBase party, IFaction captorFaction, EndCaptivityDetail detail, bool isPlayerInvolved)
        {
            // If the hero is freed naturally or by our ransom, remove them from tracked list so they can be ransomed again if captured in the future.
            if (_trackedHeroes.Contains(hero))
            {
                _trackedHeroes.Remove(hero);
            }
        }

        private void OnHourlyTick()
        {
            for (int i = _offers.Count - 1; i >= 0; i--)
            {
                RansomOfferData offer = _offers[i];

                if (offer.Hero == null || offer.Hero.IsDead)
                {
                    _offers.RemoveAt(i);
                    continue;
                }

                if (!offer.IsInTransit && CampaignTime.Now >= offer.ResolutionTime)
                {
                    ResolveOffer(offer);
                }
                else if (offer.IsInTransit && CampaignTime.Now >= offer.DeliveryTime)
                {
                    DeliverHero(offer);
                    _offers.RemoveAt(i);
                }
            }
        }

        private void ResolveOffer(RansomOfferData offer)
        {
            int normalRansom = Campaign.Current.Models.RansomValueCalculationModel.PrisonerRansomValue(offer.Hero.CharacterObject, Hero.MainHero);
            float prob = AlliedRansomCalculator.CalculateAcceptanceProbability(offer.OfferAmount, normalRansom);

            if (MBRandom.RandomFloat <= prob)
            {
                // Accepted
                offer.IsInTransit = true;
                
                float deliveryHours = CalculateDeliveryHours(offer.Hero);
                offer.DeliveryTime = CampaignTime.Now + CampaignTime.Hours(deliveryHours);

                // Make them a transit prisoner temporarily so they can't be executed or sold by the AI while traveling
                PartyBase captorParty = offer.Hero.PartyBelongedToAsPrisoner;
                if (captorParty != null && captorParty.PrisonRoster.Contains(offer.Hero.CharacterObject))
                {
                    captorParty.PrisonRoster.RemoveTroop(offer.Hero.CharacterObject);
                }
                offer.Hero.ChangeState(Hero.CharacterStates.Prisoner);

                int estimatedHoursInt = (int)Math.Ceiling(deliveryHours);
                
                TextObject title = new TextObject("{=sms_offer_accepted_title}Offer Accepted");
                TextObject body = new TextObject("{=sms_offer_accepted_body}Your ransom offer of {OFFER}{GOLD_ICON} for {HERO_NAME} was accepted! They have been released and are now traveling to your party.\n\nEstimated arrival: {HOURS} hours.");
                body.SetTextVariable("OFFER", offer.OfferAmount);
                body.SetTextVariable("HERO_NAME", offer.Hero.Name);
                body.SetTextVariable("HOURS", estimatedHoursInt);

                InformationManager.ShowInquiry(new InquiryData(
                    title.ToString(),
                    body.ToString(),
                    true, false,
                    new TextObject("{=sms_ok}Understood").ToString(),
                    string.Empty,
                    null, null), true);
            }
            else
            {
                // Rejected
                int refund = offer.OfferAmount / 2;
                Hero.MainHero.ChangeHeroGold(refund);

                TextObject title = new TextObject("{=sms_offer_rejected_title}Offer Rejected");
                TextObject body = new TextObject("{=sms_offer_rejected_body}Your ransom offer of {OFFER}{GOLD_ICON} for {HERO_NAME} was rejected. The broker took a 50% cut, refunding you {REFUND}{GOLD_ICON}.");
                body.SetTextVariable("OFFER", offer.OfferAmount);
                body.SetTextVariable("HERO_NAME", offer.Hero.Name);
                body.SetTextVariable("REFUND", refund);

                InformationManager.ShowInquiry(new InquiryData(
                    title.ToString(),
                    body.ToString(),
                    true, false,
                    new TextObject("{=sms_ok}Understood").ToString(),
                    string.Empty,
                    null, null), true);

                _offers.Remove(offer);
            }
        }

        private void DeliverHero(RansomOfferData offer)
        {
            EndCaptivityAction.ApplyByRansom(offer.Hero, Hero.MainHero);
            
            if (!MobileParty.MainParty.MemberRoster.Contains(offer.Hero.CharacterObject))
            {
                MobileParty.MainParty.MemberRoster.AddToCounts(offer.Hero.CharacterObject, 1);
            }

            TextObject title = new TextObject("{=sms_hero_arrived_title}Hero Arrived");
            TextObject body = new TextObject("{=sms_hero_arrived_body}{HERO_NAME} has safely arrived at your party.");
            body.SetTextVariable("HERO_NAME", offer.Hero.Name);

            InformationManager.ShowInquiry(new InquiryData(
                title.ToString(),
                body.ToString(),
                true, false,
                new TextObject("{=sms_ok}Understood").ToString(),
                string.Empty,
                null, null), true);
        }

        private float CalculateDeliveryHours(Hero lord)
        {
            Vec2 playerPosition = MobileParty.MainParty.GetPosition2D;
            Vec2 lordPosition;

            PartyBase captorParty = lord.PartyBelongedToAsPrisoner;
            if (captorParty != null)
            {
                if (captorParty.IsMobile)
                    lordPosition = captorParty.MobileParty.GetPosition2D;
                else if (captorParty.IsSettlement)
                    lordPosition = captorParty.Settlement.GetPosition2D;
                else
                    lordPosition = playerPosition;
            }
            else
            {
                Settlement stayingSettlement = lord.StayingInSettlement ?? lord.CurrentSettlement;
                lordPosition = stayingSettlement?.GetPosition2D ?? playerPosition;
            }

            float distance = playerPosition.Distance(lordPosition);
            float hours = MathF.Max(6f, distance * 0.2f * SmsSettingsManager.LordDeliverySpeedMultiplier);
            return hours;
        }
    }
}
