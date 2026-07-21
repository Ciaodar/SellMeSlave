using SMS.Behaviors;
using SMS.Config;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using System.Linq;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using System;
using TaleWorlds.Core;

namespace SMS.Menu
{
    /// <summary>
    /// Manages the game menus for the slave buying system.
    /// Adds "Buy prisoners" option to town_backstreet and creates the sms_buyslaves submenu.
    /// </summary>
    public static class SlaveMenuManager
    {
        public static void AddGameMenus(CampaignGameStarter starter)
        {
            // Add "Buy prisoners" option to town_backstreet (before the back button)
            starter.AddGameMenuOption(
                "town_backstreet",
                "sms_buy_slaves",
                "{=sms_buy_slaves_opt}Captive Trading",
                OnBuySlavesCondition,
                OnBuySlavesConsequence,
                index: 2);

            // Create the buy slaves submenu
            starter.AddGameMenu(
                "sms_buyslaves",
                "{=sms_buyslaves_desc}The ransom broker has brought you to a secret area where slave trading takes place. 'Buying' slaves is illegal, and things are expensive on the black market. What will you do?",
                OnBuySlavesMenuInit,
                GameMenu.MenuOverlayType.SettlementWithBoth);

            // "Buy prisoner troops" button
            starter.AddGameMenuOption(
                "sms_buyslaves",
                "sms_buy_troops",
                "{=sms_buy_troops_opt}Buy prisoner troops",
                OnBuyTroopsCondition,
                OnBuyTroopsConsequence);

            // "Buy a prisoner lord" button
            starter.AddGameMenuOption(
                "sms_buyslaves",
                "sms_buy_lord",
                "{=sms_buy_lord_opt}Buy a prisoner lord",
                OnBuyLordCondition,
                OnBuyLordConsequence);

            // "Send ransom offer for an allied hero" button
            starter.AddGameMenuOption(
                "sms_buyslaves",
                "sms_offer_ransom",
                "{=sms_offer_ransom_opt}Send ransom offer for an allied hero",
                OnOfferRansomCondition,
                OnOfferRansomConsequence);

            // "Sell only Heroes" button
            starter.AddGameMenuOption(
                "sms_buyslaves",
                "sms_sell_lords",
                "{=sms_sell_lords_opt}Sell only Heroes",
                args => OnSellCondition(args, x => x.Character.IsHero),
                args => OnSellConsequence(args, x => x.Character.IsHero));

            // "Sell only regular troops" button
            starter.AddGameMenuOption(
                "sms_buyslaves",
                "sms_sell_troops",
                "{=sms_sell_troops_opt}Sell only regular troops",
                args => OnSellCondition(args, x => !x.Character.IsHero),
                args => OnSellConsequence(args, x => !x.Character.IsHero));

            // "Sell all except bandits" button
            starter.AddGameMenuOption(
                "sms_buyslaves",
                "sms_sell_non_bandits",
                "{=sms_sell_non_bandits_opt}Sell all troops except bandits",
                args => OnSellCondition(args, x => x.Character.Occupation != Occupation.Bandit && !x.Character.IsHero),
                args => OnSellConsequence(args, x => x.Character.Occupation != Occupation.Bandit && !x.Character.IsHero));

            // "Sell only bandits" button
            starter.AddGameMenuOption(
                "sms_buyslaves",
                "sms_sell_bandits",
                "{=sms_sell_bandits_opt}Sell only bandits",
                args => OnSellCondition(args, x => x.Character.Occupation == Occupation.Bandit && !x.Character.IsHero),
                args => OnSellConsequence(args, x => x.Character.Occupation == Occupation.Bandit && !x.Character.IsHero));

            // Back button
            starter.AddGameMenuOption(
                "sms_buyslaves",
                "sms_buyslaves_back",
                "{=sms_back_opt}Back",
                OnBackCondition,
                OnBackConsequence,
                isLeave: true);
        }

        // ──────────────────────────── Conditions ────────────────────────────

        private static bool OnBuySlavesCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Submenu;

            if (!SmsSettingsManager.EnableBuySlaveMenu)
                return false;

            var current = Hero.MainHero?.CurrentSettlement ??
                          Settlement.CurrentSettlement ??
                          PlayerEncounter.EncounterSettlement;
            return current != null && current.IsTown;
        }

        private static bool OnBuyTroopsCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.DonatePrisoners;


            var current = Hero.MainHero.CurrentSettlement??
                          Settlement.CurrentSettlement??
                          PlayerEncounter.EncounterSettlement;

            bool hasStock = BuySlaveBehavior.Instance?.HasTroopStock(current) ?? false;
            if (!hasStock)
            {
                args.Tooltip = new TaleWorlds.Localization.TextObject(
                    "{=sms_no_stock}The broker has no prisoners available at this time.");
                args.IsEnabled = false;
            }

            return true;
        }

        private static bool OnBuyLordCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.StagePrisonBreak;

            int pendingLords = SMS.Behaviors.BuySlaveBehavior.Instance?.GetPendingDeliveriesCount() ?? 0;
            int maxLords = SMS.Config.SmsSettingsManager.MaxLordTransferCount;

            if (pendingLords >= maxLords)
            {
                args.Tooltip = new TaleWorlds.Localization.TextObject(
                    "{=sms_lord_limit_reached}You have reached the limit for concurrent Lord orders.");
                args.IsEnabled = false;
                return true;
            }

            bool hasLords = SlaveTradeScreenManager.GetCapturedLordsCount() > 0;
            if (!hasLords)
            {
                args.Tooltip = new TaleWorlds.Localization.TextObject(
                    "{=sms_no_lords}There are no captured heroes available in the realm.");
                args.IsEnabled = false;
            }

            return true;
        }

        private static bool OnOfferRansomCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Ransom;
            
            bool hasCaptives = AlliedRansomScreenManager.GetCaptiveClanHeroes().Count > 0;
            if (!hasCaptives)
            {
                args.Tooltip = new TaleWorlds.Localization.TextObject(
                    "{=sms_no_allied_captives}There are no eligible captive clan members available.");
                args.IsEnabled = false;
            }

            return true;
        }

        private static bool OnBackCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Leave;
            return true;
        }

        private static bool OnSellCondition(MenuCallbackArgs args, Func<TroopRosterElement, bool> filter)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Trade;
            
            var prisoners = MobileParty.MainParty?.PrisonRoster?.GetTroopRoster();
            if (prisoners == null || !prisoners.Any(filter))
            {
                return false;
            }

            return true;
        }

        // ──────────────────────────── Consequences ────────────────────────────

        private static void OnBuySlavesConsequence(MenuCallbackArgs args)
        {
            GameMenu.SwitchToMenu("sms_buyslaves");
        }

        private static void OnBuyTroopsConsequence(MenuCallbackArgs args)
        {
            var current = Hero.MainHero?.CurrentSettlement ??
                          Settlement.CurrentSettlement ??
                          PlayerEncounter.EncounterSettlement;
            if (current == null) return;

            var stock = BuySlaveBehavior.Instance?.GetTroopStock(current);
            if (stock == null || stock.TotalManCount == 0) return;

            SlaveTradeScreenManager.OpenBuyTroopsScreen(stock, current);
        }

        private static void OnBuyLordConsequence(MenuCallbackArgs args)
        {
            SlaveTradeScreenManager.OpenBuyLordScreen();
        }

        private static void OnOfferRansomConsequence(MenuCallbackArgs args)
        {
            AlliedRansomScreenManager.OpenRansomScreen();
        }

        private static void OnBackConsequence(MenuCallbackArgs args)
        {
            GameMenu.SwitchToMenu("town_backstreet");
        }

        private static void OnSellConsequence(MenuCallbackArgs args, Func<TroopRosterElement, bool> filter)
        {
            var prisoners = MobileParty.MainParty?.PrisonRoster?.GetTroopRoster()?.Where(filter).ToList();
            if (prisoners == null || !prisoners.Any()) return;

            int count = 0;
            int totalGold = 0;

            var currentSettlement = Settlement.CurrentSettlement ?? Hero.MainHero.CurrentSettlement;

            foreach (var element in prisoners)
            {
                int gold = Campaign.Current.Models.RansomValueCalculationModel.PrisonerRansomValue(element.Character, Hero.MainHero) * element.Number;
                totalGold += gold;
                count += element.Number;

                // Manually remove prisoners and add gold since SellPrisonersAction.ApplyForParty doesn't exist
                if (MobileParty.MainParty?.PrisonRoster != null)
                {
                    MobileParty.MainParty.PrisonRoster.RemoveTroop(element.Character, element.Number);
                    Hero.MainHero.ChangeHeroGold(gold);
                }
            }

            TextObject msg = new TextObject("{=sms_sold_msg}You sold {COUNT} prisoners for {GOLD}{GOLD_ICON}.");
            msg.SetTextVariable("COUNT", count);
            msg.SetTextVariable("GOLD", totalGold);
            MBInformationManager.AddQuickInformation(msg);

            // Refresh menu to update buttons
            GameMenu.ActivateGameMenu("sms_buyslaves");
        }

        // ──────────────────────────── Menu Init ────────────────────────────

        private static void OnBuySlavesMenuInit(MenuCallbackArgs args)
        {
            // Trigger stock generation when menu is opened
            var current = Hero.MainHero?.CurrentSettlement ??
                          Settlement.CurrentSettlement ??
                          PlayerEncounter.EncounterSettlement;
            if (current != null && current.IsTown)
            {
                BuySlaveBehavior.Instance?.EnsureStockExists(current);
            }

            args.MenuTitle = new TaleWorlds.Localization.TextObject("{=sms_menu_title}Prisoner Broker");
        }
    }
}
