using System.Collections.Generic;
using System.Linq;
using SMS.Config;
using SMS.Menu;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Localization;

namespace SMS.Behaviors
{
    public class LordTradeDialogueBehavior : CampaignBehaviorBase
    {
        private Hero? _exchangeCandidateForPlayer;
        private Hero? _exchangeCandidateForLord;
        private bool _isBuyingHeroes;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            AddTradeDialogs(starter);
            AddExchangeDialogs(starter);
        }

        private void AddTradeDialogs(CampaignGameStarter starter)
        {
            // Player initiates trade
            starter.AddPlayerLine(
                "sms_trade_start",
                "lord_talk_speak_diplomacy_2",
                "sms_trade_player_proposal",
                "{=sms_trade_request}Are you interested in trading captives?",
                OnTradeCondition,
                null);

            // Lord asks for details
            starter.AddDialogLine(
                "sms_trade_offer_resp",
                "sms_trade_player_proposal",
                "sms_trade_options",
                "{=sms_trade_offer}What is your offer about?",
                null,
                null);

            // Player proposals (Buy Someone)
            starter.AddPlayerLine(
                "sms_buy_someone_line",
                "sms_trade_options",
                "sms_trade_purchase_check",
                "{=sms_buy_someone}I can pay good amount to buy someone you keep.",
                OnLordHasHeroPrisonersCondition,
                () => { _isBuyingHeroes = true; });

            // Player proposals (Buy Culprits)
            starter.AddPlayerLine(
                "sms_buy_culprits_line",
                "sms_trade_options",
                "sms_trade_purchase_check",
                "{=sms_buy_culprits}I need some culprits to put them in order.",
                OnLordHasRegularPrisonersCondition,
                () => { _isBuyingHeroes = false; });

            // Cartel Option
            starter.AddPlayerLine(
                "sms_exchange_start_line",
                "sms_trade_options",
                "sms_exchange_response_check",
                "{=sms_cartel_request}I think we can arrange a cartel.",
                OnExchangeCondition,
                null);

            // Never Mind Option
            starter.AddPlayerLine(
                "sms_never_mind_line",
                "sms_trade_options",
                "lord_pretalk",
                "{=sms_never_mind}Never mind.",
                null,
                null);

            // Honor check for purchases
            starter.AddDialogLine(
                "sms_trade_dishonor_resp",
                "sms_trade_purchase_check",
                "lord_pretalk",
                "{=sms_trade_dishonor}What do you take me for? I'm an honorable man!",
                OnLordIsHonorableCondition,
                OnLordDishonoredConsequence,
                priority: 200);

            // Acceptance for purchases
            starter.AddDialogLine(
                "sms_trade_agree_resp",
                "sms_trade_purchase_check",
                "sms_trade_agree_screen",
                "{=sms_trade_agree}Be my guest, Its time for them to bring money to me, finally.",
                null,
                null);

            starter.AddDialogLine(
                "sms_trade_agree_screen",
                "sms_trade_agree_screen",
                "lord_pretalk",
                "{=!} ", // Invisible filler, advanced by the screen closing delegate
                null,
                OnTradeAgreementConsequence);
        }

        private void AddExchangeDialogs(CampaignGameStarter starter)
        {
            // Lord rejection
            starter.AddDialogLine(
                "sms_exchange_rejected_resp",
                "sms_exchange_response_check",
                "lord_pretalk",
                "{=sms_cartel_rejected}I don't think so.",
                OnExchangeRejectedCondition,
                null);

            // Lord acceptance (Ask who)
            starter.AddDialogLine(
                "sms_exchange_ask_who_resp",
                "sms_exchange_response_check",
                "sms_exchange_player_select",
                "{=sms_cartel_ask_who}Whom do you want to be freed?",
                null,
                null);

            starter.AddPlayerLine(
                "sms_exchange_open_inquiry",
                "sms_exchange_player_select",
                "sms_exchange_result",
                "{=sms_cartel_select_opt}Let me see who you have...",
                null,
                OnExchangeStartedConsequence);

            // Result of exchange (Deal)
            starter.AddDialogLine(
                "sms_exchange_proposal_resp",
                "sms_exchange_result",
                "sms_exchange_player_final",
                "{=sms_cartel_proposal}Okay, I want {EXCHANGE_LORD_NAME} to be freed then.",
                OnExchangeProposalCondition,
                null);

            starter.AddPlayerLine(
                "sms_exchange_deal_line",
                "sms_exchange_player_final",
                "lord_pretalk",
                "{=sms_cartel_deal}Deal!",
                null,
                OnExchangeDealConsequence);

            starter.AddPlayerLine(
                "sms_exchange_refuse_line",
                "sms_exchange_player_final",
                "sms_exchange_lord_loss",
                "{=sms_cartel_refuse}This is not a good deal for me.",
                null,
                null);

            starter.AddDialogLine(
                "sms_exchange_loss_resp",
                "sms_exchange_lord_loss",
                "lord_politics_request",
                "{=sms_cartel_loss}Your loss.",
                null,
                null);
        }

        // ──────────────────────────── Conditions & Consequences ────────────────────────────

        private bool OnTradeCondition()
        {
            Hero? lord = Hero.OneToOneConversationHero;
            return lord != null && lord.IsLord && lord.PartyBelongedTo?.PrisonRoster != null &&
                   lord.PartyBelongedTo.PrisonRoster.TotalManCount > 0;
        }

        private bool OnLordIsHonorableCondition()
        {
            return Hero.OneToOneConversationHero?.GetTraitLevel(DefaultTraits.Honor) > 0;
        }

        private void OnLordDishonoredConsequence()
        {
            if (Hero.OneToOneConversationHero != null)
            {
                ChangeRelationAction.ApplyPlayerRelation(Hero.OneToOneConversationHero, -2);
            }
        }

        private bool OnLordHasHeroPrisonersCondition()
        {
            Hero? lord = Hero.OneToOneConversationHero;
            return lord?.PartyBelongedTo?.PrisonRoster != null &&
                   lord.PartyBelongedTo.PrisonRoster.GetTroopRoster().Any(x => x.Character.IsHero);
        }

        private bool OnLordHasRegularPrisonersCondition()
        {
            Hero? lord = Hero.OneToOneConversationHero;
            return lord?.PartyBelongedTo?.PrisonRoster != null &&
                   lord.PartyBelongedTo.PrisonRoster.GetTroopRoster().Any(x => !x.Character.IsHero);
        }

        private void OnTradeAgreementConsequence()
        {
            Hero? lord = Hero.OneToOneConversationHero;
            if (lord?.PartyBelongedTo != null)
            {
                SlaveTradeScreenManager.OpenLordTradeScreen(lord.PartyBelongedTo.Party, _isBuyingHeroes);
            }

            _isBuyingHeroes = false;
        }

        private bool OnExchangeCondition()
        {
            if (!SmsSettingsManager.EnablePrisonerExchange) return false;

            Hero? lord = Hero.OneToOneConversationHero;
            if (lord == null || !lord.IsLord) return false;

            // Check if lord's clan holds any of player clan/friends
            bool lordHoldsOurs = Hero.AllAliveHeroes.Any(h =>
                (h.Clan == Clan.PlayerClan || h.IsFriend(Hero.MainHero)) &&
                h.IsPrisoner && h.PartyBelongedToAsPrisoner?.MapFaction == lord.MapFaction);

            // Check if player's clan holds any of lord's clan
            bool weHoldLords = Hero.AllAliveHeroes.Any(h =>
                h.Clan == lord.Clan &&
                h.IsPrisoner && h.PartyBelongedToAsPrisoner?.MapFaction == Hero.MainHero.MapFaction);

            return lordHoldsOurs && weHoldLords; // Mutual interest required for cartel
        }

        private bool OnExchangeRejectedCondition()
        {
            // Honor check or no mutual candidates
            Hero? lord = Hero.OneToOneConversationHero;
            if (lord == null) return true;

            if (lord.GetTraitLevel(DefaultTraits.Honor) < 0) return true;

            // Find candidates with broader scope
            var oursHeldByLordsClan = Hero.AllAliveHeroes.Where(h =>
                (h.Clan == Clan.PlayerClan || h.IsFriend(Hero.MainHero)) &&
                h.IsPrisoner && h.PartyBelongedToAsPrisoner?.MapFaction == lord.MapFaction).ToList();

            var lordsHeldByOurs = Hero.AllAliveHeroes.Where(h =>
                h.Clan == lord.Clan &&
                h.IsPrisoner && h.PartyBelongedToAsPrisoner?.MapFaction == Hero.MainHero.MapFaction).ToList();

            return oursHeldByLordsClan.Count == 0 || lordsHeldByOurs.Count == 0;
        }

        private void OnExchangeStartedConsequence()
        {
            Hero? lord = Hero.OneToOneConversationHero;
            if (lord == null) return;

            // Clear previous states to prevent old data from persisting
            _exchangeCandidateForPlayer = null;
            _exchangeCandidateForLord = null;

            var candidates = Hero.AllAliveHeroes.Where(h =>
                (h.Clan == Clan.PlayerClan || h.IsFriend(Hero.MainHero)) &&
                h.IsPrisoner && h.PartyBelongedToAsPrisoner?.MapFaction == lord.MapFaction).ToList();

            if (candidates.Count == 0) return;

            List<InquiryElement> elements = new List<InquiryElement>();
            foreach (var h in candidates)
            {
                elements.Add(new InquiryElement(h, h.Name.ToString(),
                    new CharacterImageIdentifier(CharacterCode.CreateFrom(h.CharacterObject))));
            }


            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                new TextObject("{=sms_cartel_select}Select captive to exchange").ToString(),
                string.Empty,
                elements,
                false, 1, 1, // isExitShown = false as requested
                new TextObject("{=sms_ok}Confirm").ToString(),
                string.Empty,
                selectedElements =>
                {
                    _exchangeCandidateForPlayer = selectedElements[0].Identifier as Hero;

                    // Lord picks someone from his clan held by player clan (highest level preferably)
                    _exchangeCandidateForLord = Hero.AllAliveHeroes
                        .Where(h => h.Clan == lord.Clan && h.IsPrisoner &&
                                    h.PartyBelongedToAsPrisoner?.MapFaction == Hero.MainHero.MapFaction)
                        .OrderByDescending(h => h.Level)
                        .FirstOrDefault();

                    // Advance conversation
                    if (Campaign.Current.ConversationManager != null)
                    {
                        Campaign.Current.ConversationManager.ContinueConversation();
                    }
                },
                null), true);
        }

        private bool OnExchangeProposalCondition()
        {
            if (_exchangeCandidateForLord == null) return false;

            MBTextManager.SetTextVariable("EXCHANGE_LORD_NAME", _exchangeCandidateForLord.Name);
            return true;
        }

        private void OnExchangeDealConsequence()
        {
            if (_exchangeCandidateForPlayer != null && _exchangeCandidateForLord != null)
            {
                EndCaptivityAction.ApplyByReleasedByChoice(_exchangeCandidateForPlayer, Hero.MainHero);
                EndCaptivityAction.ApplyByReleasedByChoice(_exchangeCandidateForLord, Hero.MainHero);

                if (Hero.OneToOneConversationHero != null)
                {
                    ChangeRelationAction.ApplyPlayerRelation(Hero.OneToOneConversationHero, 5);
                }
            }

            _exchangeCandidateForPlayer = null;
            _exchangeCandidateForLord = null;
        }

        public override void SyncData(IDataStore dataStore)
        {
            // No persistent data needed for this behavior as it's conversation-based
        }
    }
}
