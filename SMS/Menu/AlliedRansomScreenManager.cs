using System;
using System.Collections.Generic;
using SMS.Behaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace SMS.Menu
{
    public static class AlliedRansomScreenManager
    {
        public static List<Hero> GetCaptiveClanHeroes()
        {
            List<Hero> captives = new List<Hero>();
            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                if (hero.IsPrisoner &&
                    hero.Clan == Clan.PlayerClan &&
                    hero != Hero.MainHero &&
                    (AlliedRansomBehavior.Instance == null || !AlliedRansomBehavior.Instance.HasActiveOrPastOffer(hero)))
                {
                    captives.Add(hero);
                }
            }
            return captives;
        }

        public static void OpenRansomScreen()
        {
            List<Hero> captives = GetCaptiveClanHeroes();
            if (captives.Count == 0) return;

            TroopRoster lordRoster = TroopRoster.CreateDummyTroopRoster();
            foreach (Hero lord in captives)
            {
                lordRoster.AddToCounts(lord.CharacterObject, 1);
            }

            PartyScreenLogic partyScreenLogic = new PartyScreenLogic();

            TroopRoster leftMemberRoster = TroopRoster.CreateDummyTroopRoster();
            TroopRoster leftPrisonerRoster = lordRoster;

            PartyScreenLogicInitializationData initData = new PartyScreenLogicInitializationData()
            {
                LeftOwnerParty = null,
                RightOwnerParty = PartyBase.MainParty,
                LeftMemberRoster = leftMemberRoster,
                LeftPrisonerRoster = leftPrisonerRoster,
                RightMemberRoster = MobileParty.MainParty.MemberRoster,
                RightPrisonerRoster = MobileParty.MainParty.PrisonRoster.CloneRosterData(),
                LeftLeaderHero = null,
                RightLeaderHero = PartyBase.MainParty.LeaderHero,
                LeftPartyMembersSizeLimit = 0,
                LeftPartyPrisonersSizeLimit = lordRoster.TotalManCount,
                RightPartyMembersSizeLimit = PartyBase.MainParty.PartySizeLimit,
                RightPartyPrisonersSizeLimit = PartyBase.MainParty.PrisonerSizeLimit,
                LeftPartyName = new TextObject("{=sms_clan_captives}Captive Clan Members"),
                RightPartyName = PartyBase.MainParty.Name,
                TroopTransferableDelegate = new IsTroopTransferableDelegate(RansomTransferableDelegate),
                PartyPresentationDoneButtonDelegate = new PartyPresentationDoneButtonDelegate(RansomDoneHandler),
                PartyPresentationDoneButtonConditionDelegate = new PartyPresentationDoneButtonConditionDelegate(RansomDoneCondition),
                PartyPresentationCancelButtonActivateDelegate = null,
                PartyPresentationCancelButtonDelegate = null,
                IsDismissMode = false,
                IsTroopUpgradesDisabled = true,
                Header = new TextObject("{=sms_ransom_header}Ransom Allied Heroes"),
                PartyScreenClosedDelegate = null,
                TransferHealthiesGetWoundedsFirst = false,
                ShowProgressBar = false,
                MemberTransferState = PartyScreenLogic.TransferState.NotTransferable,
                PrisonerTransferState = PartyScreenLogic.TransferState.TransferableWithTrade,
                AccompanyingTransferState = PartyScreenLogic.TransferState.NotTransferable,
                DoNotApplyGoldTransactions = true,
                PartyScreenMode = Helpers.PartyScreenHelper.PartyScreenMode.Ransom
            };

            partyScreenLogic.Initialize(initData);

            PartyState state = Game.Current.GameStateManager.CreateState<PartyState>();
            state.PartyScreenLogic = partyScreenLogic;
            state.IsDonating = false;
            Game.Current.GameStateManager.PushState((TaleWorlds.Core.GameState)state);
        }

        private static bool RansomTransferableDelegate(CharacterObject character, PartyScreenLogic.TroopType type, PartyScreenLogic.PartyRosterSide side, PartyBase leftOwnerParty)
        {
            return type == PartyScreenLogic.TroopType.Prisoner && character.IsHero;
        }

        private static TroopRoster GetTransferredPrisoners(PartyScreenLogic logic)
        {
            TroopRoster transferred = TroopRoster.CreateDummyTroopRoster();
            if (logic?.CurrentData?.TransferredPrisonersHistory != null)
            {
                foreach (var entry in logic.CurrentData.TransferredPrisonersHistory)
                {
                    if (entry.Item2 > 0)
                    {
                        transferred.AddToCounts(entry.Item1, entry.Item2);
                    }
                }
            }
            return transferred;
        }

        private static Tuple<bool, TextObject> RansomDoneCondition(TroopRoster leftMemberRoster, TroopRoster leftPrisonRoster, TroopRoster rightMemberRoster, TroopRoster rightPrisonRoster, int leftLimitNum, int rightLimitNum)
        {
            PartyState? partyState = Game.Current.GameStateManager.ActiveState as PartyState;
            PartyScreenLogic? logic = partyState?.PartyScreenLogic;

            if (logic == null) return new Tuple<bool, TextObject>(true, TextObject.GetEmpty());

            TroopRoster purchasedLords = GetTransferredPrisoners(logic);
            int lordCount = purchasedLords.TotalHeroes;

            if (lordCount > 1)
            {
                return new Tuple<bool, TextObject>(false, new TextObject("{=sms_ransom_limit}You can only send one offer at a time."));
            }

            return new Tuple<bool, TextObject>(true, TextObject.GetEmpty());
        }

        private static bool RansomDoneHandler(TroopRoster leftMemberRoster, TroopRoster leftPrisonRoster, TroopRoster rightMemberRoster, TroopRoster rightPrisonRoster, FlattenedTroopRoster takenPrisonerRoster, FlattenedTroopRoster releasedPrisonerRoster, bool isForced, PartyBase leftParty = null!, PartyBase rightParty = null!)
        {
            if (takenPrisonerRoster != null && !takenPrisonerRoster.IsEmpty<FlattenedTroopRosterElement>())
            {
                foreach (FlattenedTroopRosterElement element in takenPrisonerRoster)
                {
                    if (element.Troop.IsHero)
                    {
                        ShowRansomOfferInquiry(element.Troop.HeroObject);
                        break; // Only 1 allowed anyway
                    }
                }
            }
            return true;
        }

        private static void ShowRansomOfferInquiry(Hero hero)
        {
            int normalValue = Campaign.Current.Models.RansomValueCalculationModel.PrisonerRansomValue(hero.CharacterObject, Hero.MainHero);
            int defaultOffer = (int)(normalValue * 1.5f);

            TextObject title = new TextObject("{=sms_ransom_offer_title}Ransom Offer for {HERO_NAME}");
            title.SetTextVariable("HERO_NAME", hero.Name);

            TextObject text = new TextObject("{=sms_ransom_offer_body}The broker asks for your offer. The normal ransom value for {HERO_NAME} is {NORMAL_VALUE}{GOLD_ICON}.\n\nEnter your offer below:");
            text.SetTextVariable("HERO_NAME", hero.Name);
            text.SetTextVariable("NORMAL_VALUE", normalValue);

            TextInquiryData inquiryData = new TextInquiryData(
                title.ToString(),
                text.ToString(),
                true,
                true,
                new TextObject("{=sms_send_offer}Send Offer").ToString(),
                new TextObject("{=sms_cancel}Cancel").ToString(),
                input => OnOfferSent(hero, input),
                null,
                false,
                input => ValidateOfferInput(input)
            );

            // Access internal text using a slight hack: TaleWorlds allows setting initial text via the input string? No, TextInquiryData doesn't have an initial text parameter.
            // Wait, Bannerlord's TextInquiryData does not have a "default text" parameter in the constructor in 1.2.x.
            // Players will just have to type it. The prompt will tell them the normal value.

            InformationManager.ShowTextInquiry(inquiryData, true);
        }

        private static Tuple<bool, string> ValidateOfferInput(string input)
        {
            if (int.TryParse(input, out int offerAmount))
            {
                if (offerAmount <= 0)
                {
                    return new Tuple<bool, string>(false, new TextObject("{=sms_offer_too_low}Offer must be greater than 0.").ToString());
                }
                if (offerAmount > Hero.MainHero.Gold)
                {
                    return new Tuple<bool, string>(false, new TextObject("{=sms_offer_no_gold}You do not have enough gold.").ToString());
                }
                return new Tuple<bool, string>(true, string.Empty);
            }
            return new Tuple<bool, string>(false, new TextObject("{=sms_offer_invalid}Invalid amount.").ToString());
        }

        private static void OnOfferSent(Hero hero, string input)
        {
            if (int.TryParse(input, out int offerAmount))
            {
                AlliedRansomBehavior.Instance?.AddOffer(hero, offerAmount);
            }
        }
    }
}
