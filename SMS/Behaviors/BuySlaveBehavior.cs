using System.Collections.Generic;
using System.Linq;
using SMS.Config;
using SMS.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
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
        public void ApplyCriminalConsequences(int goldAmount, bool isLordTransfer = false)
        {
            if (goldAmount <= 0) return;

            // Roguery XP gain
            float xp = (goldAmount / 20f) * SmsSettingsManager.RogueryXpMultiplier;
            if (xp >= 1f)
            {
                Hero.MainHero.AddSkillXp(DefaultSkills.Roguery, xp);
                
                TextObject xpMsg = new TextObject("{=sms_roguery_xp_msg}Gained {XP} Roguery experience from illegal trade.");
                xpMsg.SetTextVariable("XP", (int)xp);
                MBInformationManager.AddQuickInformation(xpMsg);
            }

            // Increase crime rating in the territory's faction
            Settlement? territorySettlement = Settlement.CurrentSettlement ??
                                              Hero.MainHero.CurrentSettlement ??
                                              Helpers.SettlementHelper.FindNearestSettlementToMobileParty(
                                                  MobileParty.MainParty, MobileParty.NavigationType.All);
            if (territorySettlement != null && territorySettlement.MapFaction != null && !territorySettlement.MapFaction.IsBanditFaction)
            {
                float crimeRating = 0f;
                if (isLordTransfer)
                {
                    // Default for lords: 0.2 criminal rating per 1000 gold
                    crimeRating = (goldAmount / 5000f) * SmsSettingsManager.CrimeRatingMultiplier;
                }
                else
                {
                    if (SmsSettingsManager.CrimeRatingMultiplier > 0f)
                    {
                        // For troops: base crime rating scaled by gold, but clamped between 1 and 4
                        crimeRating = (goldAmount / 1000f) * SmsSettingsManager.CrimeRatingMultiplier;
                        crimeRating = TaleWorlds.Library.MathF.Clamp(crimeRating, 1f, 4f);
                    }
                }

                if (crimeRating > 0f)
                {
                    ChangeCrimeRatingAction.Apply(territorySettlement.MapFaction, crimeRating);
                }
            }

            // Apply Honor loss
            ApplyHonorConsequences(goldAmount);
        }

        private void ApplyHonorConsequences(int goldAmount)
        {
            if (goldAmount <= 0 || SmsSettingsManager.HonorLossMultiplier <= 0f) return;

            // 1 point of honor loss per 1000 gold * multiplier
            int honorLoss = (int)((goldAmount / 1000f) * SmsSettingsManager.HonorLossMultiplier);
            if (honorLoss > 0)
                TraitLevelingHelper.OnIncidentResolved(DefaultTraits.Honor, -honorLoss);
        }

        /// <summary>
        /// Increases relation with the lord who sold the prisoners.
        /// Illegal gold is still gold!
        /// </summary>
        public void ApplyRelationConsequences(Hero sellerHero, int goldAmount)
        {
            if (sellerHero == null || goldAmount <= 0) return;

            // 1 relation per 1000 gold * multiplier (Increased from 2000)
            float relationGain = (goldAmount / 1000f) * SmsSettingsManager.RelationGainMultiplier;
            if (relationGain > 0.1f)
            {
                ChangeRelationAction.ApplyPlayerRelation(sellerHero, (int)relationGain);
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
                    Hero lord = delivery.Lord;
                    Hero buyer = delivery.BuyerHero ?? Hero.MainHero;
                    
                    if (buyer == Hero.MainHero)
                    {
                        bool isAtWar = FactionManager.IsAtWarAgainstFaction(lord.MapFaction, Hero.MainHero.MapFaction);
                        if (isAtWar)
                        {
                            DeliverLord(lord);
                        }
                        else
                        {
                            ShowNonHostileInquiry(lord);
                        }
                    }
                    else
                    {
                        // AI Delivery
                        if (buyer.IsAlive && buyer.PartyBelongedTo != null && buyer.PartyBelongedTo.Party != null)
                        {
                            TakePrisonerAction.Apply(buyer.PartyBelongedTo.Party, lord);
                        }
                        else
                        {
                            // If buyer died or lost party, prisoner escapes or goes to nearest settlement
                            lord.ChangeState(Hero.CharacterStates.Released);
                        }
                    }

                    delivered.Add(delivery);
                }
            }

            foreach (LordDeliveryData d in delivered)
            {
                _pendingDeliveries.Remove(d);
            }
        }

        private void DeliverLord(Hero lord)
        {
            TakePrisonerAction.Apply(PartyBase.MainParty, lord);

            TextObject titleText = new TextObject("{=sms_lord_delivered_title}Hero Delivered");
            TextObject bodyText = new TextObject("{=sms_lord_delivered_body}{LORD_NAME} has arrived safely and is now your prisoner.");
            bodyText.SetTextVariable("LORD_NAME", lord.Name);

            InformationManager.ShowInquiry(new InquiryData(
                titleText.ToString(),
                bodyText.ToString(),
                true, false,
                new TextObject("{=sms_ok}Understood").ToString(),
                string.Empty,
                null, null), true);
        }

        private void ShowNonHostileInquiry(Hero lord)
        {
            TextObject titleText = new TextObject("{=sms_lord_non_hostile_title}Noble Delivery (Peace)");
            TextObject bodyText = new TextObject("{=sms_lord_non_hostile_body}{LORD_NAME} has arrived. Since you are not at war with {FACTION_NAME}, keeping them will be seen as a criminal act. What will you do?");
            bodyText.SetTextVariable("LORD_NAME", lord.Name);
            bodyText.SetTextVariable("FACTION_NAME", lord.MapFaction?.Name ?? new TextObject("their faction"));

            InformationManager.ShowInquiry(new InquiryData(
                titleText.ToString(),
                bodyText.ToString(),
                true, true,
                new TextObject("{=sms_release_opt}Release (Relation +)").ToString(),
                new TextObject("{=sms_keep_opt}Keep as Prisoner (Crime ++)").ToString(),
                () => OnLordReleased(lord),
                () => OnLordKept(lord)), true);
        }

        private void OnLordReleased(Hero lord)
        {
            // Release the lord
            EndCaptivityAction.ApplyByReleasedByChoice(lord, Hero.MainHero);
            
            // Relation bonus
            ChangeRelationAction.ApplyPlayerRelation(lord, 10);

            TextObject msg = new TextObject("{=sms_lord_released_msg}You released {LORD_NAME}. Your relation with {CLAN_NAME} has increased.");
            msg.SetTextVariable("LORD_NAME", lord.Name);
            msg.SetTextVariable("CLAN_NAME", lord.Clan?.Name ?? new TextObject("their clan"));
            MBInformationManager.AddQuickInformation(msg);
        }

        private void OnLordKept(Hero lord)
        {
            // Keep as prisoner
            TakePrisonerAction.Apply(PartyBase.MainParty, lord);

            // Relation penalty
            ChangeRelationAction.ApplyPlayerRelation(lord, -15);

            // Crime rating increase (significant)
            if (lord.MapFaction != null && !lord.MapFaction.IsBanditFaction)
            {
                ChangeCrimeRatingAction.Apply(lord.MapFaction, 20f);
            }

            TextObject msg = new TextObject("{=sms_lord_kept_msg}You kept {LORD_NAME} as a prisoner. Your crime rating with {FACTION_NAME} has increased significantly.");
            msg.SetTextVariable("LORD_NAME", lord.Name);
            msg.SetTextVariable("FACTION_NAME", lord.MapFaction?.Name ?? new TextObject("their faction"));
            MBInformationManager.AddQuickInformation(msg);
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
                    
                    Hero buyer = delivery.BuyerHero ?? Hero.MainHero;
                    
                    if (buyer == Hero.MainHero)
                    {
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
                    }

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