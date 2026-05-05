using System.Collections.Generic;
using System.Linq;
using SMS.Config;
using SMS.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace SMS.Behaviors
{
    /// <summary>
    /// Core behavior for the slave buying system.
    /// Manages town prisoner stocks, pending lord deliveries, daily tick events,
    /// and provides a static accessor for menu/screen managers.
    /// </summary>
    public class BuySlaveBehavior : CampaignBehaviorBase
    {
        private TownPrisonerStock _townStock = new TownPrisonerStock();
        private List<LordDeliveryData> _pendingDeliveries = new List<LordDeliveryData>();

        /// <summary>
        /// Static accessor for menu/screen managers to reach this behavior instance.
        /// </summary>
        public static BuySlaveBehavior? Instance =>
            Campaign.Current?.GetCampaignBehavior<BuySlaveBehavior>();

        private CampaignGameStarter? _campaignStarter;

        /// <summary>
        /// Stores the CampaignGameStarter reference for menu registration during OnSessionLaunched.
        /// Must be called from SMSSubModule.OnGameStart.
        /// </summary>
        public void SetCampaignStarter(CampaignGameStarter starter)
        {
            _campaignStarter = starter;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            // Use the starter passed via event (preferred) or fallback to stored reference
            var gameStarter = starter ?? _campaignStarter;
            if (gameStarter != null)
            {
                SMS.Menu.SlaveMenuManager.AddGameMenus(gameStarter);
            }
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("_sms_townStock", ref _townStock);
            dataStore.SyncData("_sms_pendingDeliveries", ref _pendingDeliveries);

            if (_townStock == null) _townStock = new TownPrisonerStock();
            if (_pendingDeliveries == null) _pendingDeliveries = new List<LordDeliveryData>();
        }

        // ──────────────────────────── Town Stock API ────────────────────────────

        /// <summary>
        /// Ensures stock exists for the given settlement (creates if needed).
        /// </summary>
        public void EnsureStockExists(Settlement settlement)
        {
            _townStock.GetOrCreateStock(settlement);
        }

        /// <summary>
        /// Gets the current troop stock for a settlement.
        /// </summary>
        public TroopRoster GetTroopStock(Settlement settlement)
        {
            return _townStock.GetOrCreateStock(settlement);
        }

        /// <summary>
        /// Checks if a settlement has available troop stock.
        /// </summary>
        public bool HasTroopStock(Settlement settlement)
        {
            return _townStock.HasStock(settlement);
        }

        /// <summary>
        /// Updates the stock after a purchase.
        /// </summary>
        public void UpdateTroopStock(Settlement settlement, TroopRoster remaining)
        {
            _townStock.UpdateStock(settlement, remaining);
        }

        // ──────────────────────────── Lord Delivery API ────────────────────────────

        /// <summary>
        /// Adds a pending lord delivery.
        /// </summary>
        public void AddPendingDelivery(LordDeliveryData delivery)
        {
            _pendingDeliveries.Add(delivery);
        }

        /// <summary>
        /// Checks if a lord is already pending delivery.
        /// </summary>
        public bool IsLordPendingDelivery(Hero lord)
        {
            return _pendingDeliveries.Any(d => d.Lord == lord);
        }

        public int GetPendingDeliveriesCount()
        {
            return _pendingDeliveries.Count;
        }

        // ──────────────────────────── Crime and Roguery ────────────────────────────

        /// <summary>
        /// Grants Roguery XP and increases Crime Rating in the current settlement's faction
        /// proportional to the gold spent on illegal prisoner trade.
        /// </summary>
        public void ApplyCriminalConsequences(int goldAmount)
        {
            if (goldAmount <= 0) return;

            // 1 point of roguery XP per 50 gold
            float xp = goldAmount / 50f;
            Hero.MainHero.AddSkillXp(DefaultSkills.Roguery, xp);

            // Increase crime rating in current settlement's faction
            Settlement currentSettlement = Settlement.CurrentSettlement ?? Hero.MainHero.CurrentSettlement;
            if (currentSettlement != null && currentSettlement.MapFaction != null && !currentSettlement.MapFaction.IsBanditFaction)
            {
                // 0.5 criminal rating per 1000 gold
                float crimeRating = goldAmount / 2000f;
                ChangeCrimeRatingAction.Apply(currentSettlement.MapFaction, crimeRating);
            }
        }

        // ──────────────────────────── Data Management ────────────────────────────

        public void ClearAllData()
        {
            _townStock.ClearAllStock();
            _pendingDeliveries.Clear();
        }

        // ──────────────────────────── Daily Tick ────────────────────────────

        private void OnDailyTick()
        {
            // Remove expired town stocks
            _townStock.RemoveExpiredStocks();

            // Process lord escapes (done daily)
            ProcessLordEscapes();
        }

        // ──────────────────────────── Hourly Tick ────────────────────────────

        private void OnHourlyTick()
        {
            // Process lord deliveries hourly to avoid artificial delays
            ProcessLordDeliveries();
        }

        /// <summary>
        /// Checks all pending deliveries and delivers lords whose time has come.
        /// </summary>
        private void ProcessLordDeliveries()
        {
            List<LordDeliveryData> delivered = new List<LordDeliveryData>();

            foreach (LordDeliveryData delivery in _pendingDeliveries)
            {
                if (delivery.Lord == null || delivery.Lord.IsDead)
                {
                    delivered.Add(delivery);
                    continue;
                }

                if (delivery.IsReadyForDelivery())
                {
                    // Deliver the lord to the player's party
                    TakePrisonerAction.Apply(PartyBase.MainParty, delivery.Lord);

                    // Notify the player
                    TextObject titleText = new TextObject("{=sms_lord_delivered_title}Lord Delivered");
                    TextObject bodyText = new TextObject(
                        "{=sms_lord_delivered_body}{LORD_NAME} has been delivered to your party as a prisoner.");
                    bodyText.SetTextVariable("LORD_NAME", delivery.Lord.Name);

                    InformationManager.ShowInquiry(
                        new InquiryData(
                            titleText.ToString(),
                            bodyText.ToString(),
                            true, false,
                            new TextObject("{=sms_ok}Understood").ToString(),
                            string.Empty,
                            null, null),
                        true);

                    delivered.Add(delivery);
                }
            }

            foreach (LordDeliveryData d in delivered)
            {
                _pendingDeliveries.Remove(d);
            }
        }

        /// <summary>
        /// Checks daily escape chance for all lords in transit.
        /// Escape chance is proportional to the lord's value.
        /// </summary>
        private void ProcessLordEscapes()
        {
            List<LordDeliveryData> escaped = new List<LordDeliveryData>();

            // Calculate average lord price for proportional escape chance
            float averagePrice = _pendingDeliveries.Count > 0
                ? (float)_pendingDeliveries.Average(d => d.PurchasePrice)
                : 1f;

            foreach (LordDeliveryData delivery in _pendingDeliveries)
            {
                if (delivery.Lord == null || delivery.Lord.IsDead)
                {
                    escaped.Add(delivery);
                    continue;
                }

                // Escape chance proportional to lord value
                float priceRatio = delivery.PurchasePrice / averagePrice;
                float escapeChance = SmsSettingsManager.LordEscapeChancePerDay * priceRatio;

                if (MBRandom.RandomFloat < escapeChance)
                {
                    // Lord escaped!
                    delivery.Lord.ChangeState(Hero.CharacterStates.Released);

                    TextObject titleText = new TextObject("{=sms_lord_escaped_title}Lord Escaped!");
                    TextObject bodyText = new TextObject(
                        "{=sms_lord_escaped_body}{LORD_NAME} has escaped during transit!\n\n" +
                        "You paid {PRICE}{GOLD_ICON} for this lord, and the gold will not be refunded.");
                    bodyText.SetTextVariable("LORD_NAME", delivery.Lord.Name);
                    bodyText.SetTextVariable("PRICE", delivery.PurchasePrice);

                    InformationManager.ShowInquiry(
                        new InquiryData(
                            titleText.ToString(),
                            bodyText.ToString(),
                            true, false,
                            new TextObject("{=sms_ok}Understood").ToString(),
                            string.Empty,
                            null, null),
                        true);

                    escaped.Add(delivery);
                }
            }

            foreach (LordDeliveryData d in escaped)
            {
                _pendingDeliveries.Remove(d);
            }
        }
    }
}