using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
using SMS.Actions;
using SMS.Behaviors;
using SMS.Calculators;
using SMS.Config;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace SMS.Menu
{
    /// <summary>
    /// Manages the PartyScreen-based trade screens for buying prisoners and lords.
    /// Uses native PartyScreen system with custom delegates for price validation and transfer logic.
    /// </summary>
    public static class SlaveTradeScreenManager
    {
        private static Settlement? _currentTradeSettlement;
        
        /// <summary>
        /// Flag to indicate if the custom trade screen is currently active.
        /// This is used by the SmsRansomValueCalculationModel to apply custom prices.
        /// </summary>
        public static bool IsTradeScreenActive { get; private set; } = false;

        // ──────────────────────────── Troop Trade ────────────────────────────

        /// <summary>
        /// Opens a PartyScreen for buying regular prisoner troops.
        /// Left side shows available prisoners from the broker; right side shows player's party.
        /// </summary>
        public static void OpenBuyTroopsScreen(TroopRoster availablePrisoners, Settlement settlement)
        {
            _currentTradeSettlement = settlement;

            PartyScreenLogic partyScreenLogic = new PartyScreenLogic();

            TroopRoster leftMemberRoster = TroopRoster.CreateDummyTroopRoster();
            TroopRoster leftPrisonerRoster = availablePrisoners.CloneRosterData();

            PartyScreenLogicInitializationData initData = new PartyScreenLogicInitializationData()
            {
                LeftOwnerParty = null,
                RightOwnerParty = PartyBase.MainParty,
                LeftMemberRoster = leftMemberRoster,
                LeftPrisonerRoster = leftPrisonerRoster,
                RightMemberRoster = MobileParty.MainParty.MemberRoster,
                RightPrisonerRoster = MobileParty.MainParty.PrisonRoster,
                LeftLeaderHero = null,
                RightLeaderHero = PartyBase.MainParty.LeaderHero,
                LeftPartyMembersSizeLimit = 0,
                LeftPartyPrisonersSizeLimit = availablePrisoners.TotalManCount,
                RightPartyMembersSizeLimit = PartyBase.MainParty.PartySizeLimit,
                RightPartyPrisonersSizeLimit = PartyBase.MainParty.PrisonerSizeLimit,
                LeftPartyName = new TextObject("{=sms_broker_name}Prisoner Broker"),
                RightPartyName = PartyBase.MainParty.Name,
                TroopTransferableDelegate = new IsTroopTransferableDelegate(TroopBuyTransferableDelegate),
                PartyPresentationDoneButtonDelegate = new PartyPresentationDoneButtonDelegate(BuyTroopsDoneHandler),
                PartyPresentationDoneButtonConditionDelegate = new PartyPresentationDoneButtonConditionDelegate(BuyTroopsDoneCondition),
                PartyPresentationCancelButtonActivateDelegate = null,
                PartyPresentationCancelButtonDelegate = null,
                IsDismissMode = false,
                IsTroopUpgradesDisabled = true,
                Header = new TextObject("{=sms_buy_header}Buy Prisoners"),
                PartyScreenClosedDelegate = new PartyScreenClosedDelegate(OnBuyTroopsScreenClosed),
                TransferHealthiesGetWoundedsFirst = false,
                ShowProgressBar = false,
                MemberTransferState = PartyScreenLogic.TransferState.NotTransferable,
                PrisonerTransferState = PartyScreenLogic.TransferState.TransferableWithTrade,
                AccompanyingTransferState = PartyScreenLogic.TransferState.NotTransferable,
                DoNotApplyGoldTransactions = true,
                PartyScreenMode = Helpers.PartyScreenHelper.PartyScreenMode.Ransom
            };

            IsTradeScreenActive = true;

            partyScreenLogic.Initialize(initData);

            PartyState state = Game.Current.GameStateManager.CreateState<PartyState>();
            state.PartyScreenLogic = partyScreenLogic;
            state.IsDonating = false;
            Game.Current.GameStateManager.PushState((TaleWorlds.Core.GameState)state);
        }

        /// <summary>
        /// Allow prisoners to be transferred in both directions (buy and put back).
        /// Only prisoner type is transferable, not members.
        /// </summary>
        private static bool TroopBuyTransferableDelegate(
            CharacterObject character,
            PartyScreenLogic.TroopType type,
            PartyScreenLogic.PartyRosterSide side,
            PartyBase leftOwnerParty)
        {
            // Allow prisoner transfers in both directions (buy = left→right, put back = right→left)
            return type == PartyScreenLogic.TroopType.Prisoner;
        }

        /// <summary>
        /// Validates that the player has enough gold before allowing Done.
        /// </summary>
        private static Tuple<bool, TextObject> BuyTroopsDoneCondition(
            TroopRoster leftMemberRoster,
            TroopRoster leftPrisonRoster,
            TroopRoster rightMemberRoster,
            TroopRoster rightPrisonRoster,
            int leftLimitNum,
            int rightLimitNum)
        {
            PartyState? partyState = Game.Current.GameStateManager.ActiveState as PartyState;
            PartyScreenLogic? logic = partyState?.PartyScreenLogic;

            if (logic == null)
                return new Tuple<bool, TextObject>(true, TextObject.GetEmpty());

            // Calculate how many prisoners were moved from left to right
            TroopRoster purchasedPrisoners = GetTransferredPrisoners(logic);
            if (purchasedPrisoners.TotalManCount == 0)
                return new Tuple<bool, TextObject>(true, TextObject.GetEmpty());

            int totalCost = SlavePriceCalculator.CalculateTotalCost(purchasedPrisoners);

            if (Hero.MainHero.Gold < totalCost)
            {
                TextObject reason = new TextObject(
                    "{=sms_not_enough}Not enough gold. Cost: {COST}{GOLD_ICON}, Your gold: {GOLD}{GOLD_ICON}");
                reason.SetTextVariable("COST", totalCost);
                reason.SetTextVariable("GOLD", Hero.MainHero.Gold);
                return new Tuple<bool, TextObject>(false, reason);
            }

            // Show cost info even when affordable
            TextObject costInfo = new TextObject(
                "{=sms_cost_info}Total cost: {COST}{GOLD_ICON} | Remaining: {REMAINING}{GOLD_ICON}");
            costInfo.SetTextVariable("COST", totalCost);
            costInfo.SetTextVariable("REMAINING", Hero.MainHero.Gold - totalCost);
            return new Tuple<bool, TextObject>(true, costInfo);
        }

        /// <summary>
        /// Handles the Done button click — executes the purchase.
        /// </summary>
        private static bool BuyTroopsDoneHandler(
            TroopRoster leftMemberRoster,
            TroopRoster leftPrisonRoster,
            TroopRoster rightMemberRoster,
            TroopRoster rightPrisonRoster,
            FlattenedTroopRoster takenPrisonerRoster,
            FlattenedTroopRoster releasedPrisonerRoster,
            bool isForced,
            PartyBase leftParty = null!,
            PartyBase rightParty = null!)
        {
            // The prisoners that were moved to the right side are the purchased ones
            // takenPrisonerRoster contains the prisoners that were transferred
            if (takenPrisonerRoster != null && !takenPrisonerRoster.IsEmpty<FlattenedTroopRosterElement>())
            {
                TroopRoster purchasedRoster = TroopRoster.CreateDummyTroopRoster();
                foreach (FlattenedTroopRosterElement element in takenPrisonerRoster)
                {
                    purchasedRoster.AddToCounts(element.Troop, 1);
                }

                int totalCost = SlavePriceCalculator.CalculateTotalCost(purchasedRoster);

                // Deduct gold
                Hero.MainHero.ChangeHeroGold(-totalCost);

                // Apply criminal rating and roguery XP
                SMS.Behaviors.BuySlaveBehavior.Instance?.ApplyCriminalConsequences(totalCost);

                // Show notification
                TextObject message = new TextObject(
                    "{=sms_bought_troops}You purchased {COUNT} prisoners for {COST}{GOLD_ICON}.");
                message.SetTextVariable("COUNT", purchasedRoster.TotalManCount);
                message.SetTextVariable("COST", totalCost);
                MBInformationManager.AddQuickInformation(message);
            }

            return true;
        }

        /// <summary>
        /// Called when the buy troops screen is closed. Updates the remaining stock.
        /// </summary>
        private static void OnBuyTroopsScreenClosed(
            PartyBase leftOwnerParty,
            TroopRoster leftMemberRoster,
            TroopRoster leftPrisonRoster,
            PartyBase rightOwnerParty,
            TroopRoster rightMemberRoster,
            TroopRoster rightPrisonRoster,
            bool fromCancel)
        {
            IsTradeScreenActive = false;
            
            if (!fromCancel && _currentTradeSettlement != null)
            {
                // Update the stock with whatever remains on the left side
                BuySlaveBehavior.Instance?.UpdateTroopStock(_currentTradeSettlement, leftPrisonRoster);
            }

            _currentTradeSettlement = null!;
        }

        /// <summary>
        /// Helper to calculate which prisoners were transferred from left to right.
        /// </summary>
        private static TroopRoster GetTransferredPrisoners(PartyScreenLogic logic)
        {
            TroopRoster transferred = TroopRoster.CreateDummyTroopRoster();

            if (logic?.CurrentData?.TransferredPrisonersHistory != null)
            {
                foreach (var entry in logic.CurrentData.TransferredPrisonersHistory)
                {
                    // Positive means transferred to right (purchased by player)
                    if (entry.Item2 > 0)
                    {
                        transferred.AddToCounts(entry.Item1, entry.Item2);
                    }
                }
            }

            return transferred;
        }

        // ──────────────────────────── Lord Trade ────────────────────────────

        /// <summary>
        /// Gets the count of available captured lords in Calradia (excluding player clan).
        /// </summary>
        public static int GetCapturedLordsCount()
        {
            return GetCapturedLords().Count;
        }

        /// <summary>
        /// Gets all captured lords available for purchase.
        /// </summary>
        public static List<Hero> GetCapturedLords()
        {
            List<Hero> capturedLords = new List<Hero>();

            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                if (hero.IsPrisoner &&
                    hero.IsLord &&
                    hero != Hero.MainHero &&
                    hero.Clan != Clan.PlayerClan &&
                    !(BuySlaveBehavior.Instance?.IsLordPendingDelivery(hero) ?? false))
                {
                    capturedLords.Add(hero);
                }
            }

            return capturedLords;
        }

        /// <summary>
        /// Opens a PartyScreen for buying lord prisoners.
        /// Left side shows all captive lords; right side shows player's party.
        /// </summary>
        public static void OpenBuyLordScreen()
        {
            List<Hero> capturedLords = GetCapturedLords();
            if (capturedLords.Count == 0) return;

            TroopRoster lordRoster = TroopRoster.CreateDummyTroopRoster();
            foreach (Hero lord in capturedLords)
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
                LeftPartyName = new TextObject("{=sms_lords_title}Captured Lords of Calradia"),
                RightPartyName = PartyBase.MainParty.Name,
                TroopTransferableDelegate = new IsTroopTransferableDelegate(LordBuyTransferableDelegate),
                PartyPresentationDoneButtonDelegate = new PartyPresentationDoneButtonDelegate(BuyLordDoneHandler),
                PartyPresentationDoneButtonConditionDelegate = new PartyPresentationDoneButtonConditionDelegate(BuyLordDoneCondition),
                PartyPresentationCancelButtonActivateDelegate = null,
                PartyPresentationCancelButtonDelegate = null,
                IsDismissMode = false,
                IsTroopUpgradesDisabled = true,
                Header = new TextObject("{=sms_buy_lord_header}Buy Prisoner Lords"),
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

            IsTradeScreenActive = true;

            PartyState state = Game.Current.GameStateManager.CreateState<PartyState>();
            state.PartyScreenLogic = partyScreenLogic;
            state.IsDonating = false;
            Game.Current.GameStateManager.PushState((TaleWorlds.Core.GameState)state);
        }

        /// <summary>
        /// Only lord prisoners on the left side can be transferred.
        /// </summary>
        private static bool LordBuyTransferableDelegate(
            CharacterObject character,
            PartyScreenLogic.TroopType type,
            PartyScreenLogic.PartyRosterSide side,
            PartyBase leftOwnerParty)
        {
            return type == PartyScreenLogic.TroopType.Prisoner && character.IsHero;
        }

        /// <summary>
        /// Validates lord purchase: max transfer count and gold check.
        /// </summary>
        private static Tuple<bool, TextObject> BuyLordDoneCondition(
            TroopRoster leftMemberRoster,
            TroopRoster leftPrisonRoster,
            TroopRoster rightMemberRoster,
            TroopRoster rightPrisonRoster,
            int leftLimitNum,
            int rightLimitNum)
        {
            PartyState? partyState = Game.Current.GameStateManager.ActiveState as PartyState;
            PartyScreenLogic? logic = partyState?.PartyScreenLogic;

            if (logic == null)
                return new Tuple<bool, TextObject>(true, TextObject.GetEmpty());

            TroopRoster purchasedLords = GetTransferredPrisoners(logic);
            int lordCount = purchasedLords.TotalHeroes;

            if (lordCount == 0)
                return new Tuple<bool, TextObject>(true, TextObject.GetEmpty());

            // Max lord check
            int maxLords = SmsSettingsManager.MaxLordTransferCount;
            int pendingLords = SMS.Behaviors.BuySlaveBehavior.Instance?.GetPendingDeliveriesCount() ?? 0;
            
            if (lordCount + pendingLords > maxLords)
            {
                TextObject reason = new TextObject(
                    "{=sms_max_lords}You can buy at most {MAX} lords at once.");
                reason.SetTextVariable("MAX", maxLords);
                return new Tuple<bool, TextObject>(false, reason);
            }

            // Gold check
            int totalCost = 0;
            foreach (TroopRosterElement element in (List<TroopRosterElement>)purchasedLords.GetTroopRoster())
            {
                if (element.Character.IsHero)
                {
                    totalCost += SlavePriceCalculator.CalculateLordPrice(element.Character.HeroObject);
                }
            }

            if (Hero.MainHero.Gold < totalCost)
            {
                TextObject reason = new TextObject(
                    "{=sms_lord_no_gold}Not enough gold. Cost: {COST}{GOLD_ICON}, Your gold: {GOLD}{GOLD_ICON}");
                reason.SetTextVariable("COST", totalCost);
                reason.SetTextVariable("GOLD", Hero.MainHero.Gold);
                return new Tuple<bool, TextObject>(false, reason);
            }

            TextObject costInfo = new TextObject(
                "{=sms_cost_info}Total cost: {COST}{GOLD_ICON} | Remaining: {REMAINING}{GOLD_ICON}");
            costInfo.SetTextVariable("COST", totalCost);
            costInfo.SetTextVariable("REMAINING", Hero.MainHero.Gold - totalCost);
            return new Tuple<bool, TextObject>(true, costInfo);
        }

        /// <summary>
        /// Handles Done click for lord purchase — triggers BuyLordPrisonerAction for each lord.
        /// </summary>
        private static bool BuyLordDoneHandler(
            TroopRoster leftMemberRoster,
            TroopRoster leftPrisonRoster,
            TroopRoster rightMemberRoster,
            TroopRoster rightPrisonRoster,
            FlattenedTroopRoster takenPrisonerRoster,
            FlattenedTroopRoster releasedPrisonerRoster,
            bool isForced,
            PartyBase leftParty = null!,
            PartyBase rightParty = null!)
        {
            if (takenPrisonerRoster != null && !takenPrisonerRoster.IsEmpty<FlattenedTroopRosterElement>())
            {
                foreach (FlattenedTroopRosterElement element in takenPrisonerRoster)
                {
                    if (element.Troop.IsHero)
                    {
                        BuyLordPrisonerAction.Apply(element.Troop.HeroObject);
                    }
                }
            }

            return true;
        }

        private static void OnBuyLordsScreenClosed(
            PartyBase leftOwnerParty,
            TroopRoster leftMemberRoster,
            TroopRoster leftPrisonRoster,
            PartyBase rightOwnerParty,
            TroopRoster rightMemberRoster,
            TroopRoster rightPrisonRoster,
            bool fromCancel)
        {
            IsTradeScreenActive = false;
        }
    }
}
