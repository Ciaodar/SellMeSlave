using SMS.Behaviors;
using SMS.Config;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;

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
                "{=sms_buy_slaves_opt}Buy prisoners",
                OnBuySlavesCondition,
                OnBuySlavesConsequence,
                index: 2);

            // Create the buy slaves submenu
            starter.AddGameMenu(
                "sms_buyslaves",
                "{=sms_buyslaves_desc}\"Welcome, you can buy prisoners from me, Also I can deliver imprisoned lords or ladies from all over Calradia to you. But it costs. What would you like to do?\"",
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
                    "{=sms_no_lords}There are no captured lords available in Calradia.");
                args.IsEnabled = false;
            }

            return true;
        }

        private static bool OnBackCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Leave;
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

        private static void OnBackConsequence(MenuCallbackArgs args)
        {
            GameMenu.SwitchToMenu("town_backstreet");
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
