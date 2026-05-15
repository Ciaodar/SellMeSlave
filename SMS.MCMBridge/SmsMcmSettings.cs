using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using TaleWorlds.Localization;

namespace SMS.MCMBridge
{
    public class SmsMcmSettings : AttributeGlobalSettings<SmsMcmSettings>
    {
        public override string Id => "SellMeSlave";
        public override string DisplayName => new TextObject("{=sms_mcm_name}Sell Me Slave").ToString();
        public override string FolderName => "SellMeSlave";
        public override string FormatType => "json";

        // ──────────────────────────── General Settings ────────────────────────────

        [SettingPropertyFloatingInteger("{=sms_mcm_multiplier}Slave Price Multiplier", 1f, 10f, "0.0", Order = 1,
            RequireRestart = false, HintText = "{=sms_mcm_multiplier_hint}General multiplier for troop prices.")]
        [SettingPropertyGroup("{=sms_mcm_general}General Settings")]
        public float SlavePriceMultiplier { get; set; } = 4.0f;

        [SettingPropertyFloatingInteger("{=sms_mcm_lord_multiplier}Hero Price Multiplier", 1f, 10f, "0.0", Order = 2,
            RequireRestart = false,
            HintText = "{=sms_mcm_lord_multiplier_hint}Specific multiplier for noble prisoners.")]
        [SettingPropertyGroup("{=sms_mcm_general}General Settings")]
        public float LordPriceMultiplier { get; set; } = 4.0f;

        [SettingPropertyBool("{=sms_mcm_random}Enable Price Randomization", Order = 3, RequireRestart = false,
            HintText = "{=sms_mcm_random_hint}Adds +/- 10% randomization to prices.")]
        [SettingPropertyGroup("{=sms_mcm_general}General Settings")]
        public bool EnableRandomization { get; set; } = true;
        
        [SettingPropertyFloatingInteger("{=sms_mcm_roguery_multiplier}Roguery XP Multiplier", 0.0f, 10.0f, "0.0",
            Order = 4, RequireRestart = false,
            HintText = "{=sms_mcm_roguery_multiplier_hint}Multiplier for Roguery skill XP gain from illegal trades.")]
        [SettingPropertyGroup("{=sms_mcm_general}General Settings")]
        public float RogueryXpMultiplier { get; set; } = 1.0f;

        [SettingPropertyFloatingInteger("{=sms_mcm_honor_multiplier}Honor Loss Multiplier", 0.0f, 10.0f, "0.0",
            Order = 5, RequireRestart = false,
            HintText = "{=sms_mcm_honor_multiplier_hint}Multiplier for Honor loss from illegal trades. Set to 0 to disable.")]
        [SettingPropertyGroup("{=sms_mcm_general}General Settings")]
        public float HonorLossMultiplier { get; set; } = 1.0f;

        // ──────────────────────────── Buy Slave Menu ────────────────────────────

        [SettingPropertyBool("{=sms_mcm_enable_menu}Enable Buy Slave Menu", Order = 1, RequireRestart = false,
            HintText = "{=sms_mcm_enable_menu_hint}Show the 'Buy prisoners' option in the tavern district.")]
        [SettingPropertyGroup("{=sms_mcm_buymenu}Buy Slave Menu")]
        public bool EnableBuySlaveMenu { get; set; } = true;

        [SettingPropertyInteger("{=sms_mcm_min_slaves}Min Slaves Per Broker", 1, 30, Order = 2, RequireRestart = false,
            HintText = "{=sms_mcm_min_slaves_hint}Minimum number of prisoners a broker can offer.")]
        [SettingPropertyGroup("{=sms_mcm_buymenu}Buy Slave Menu")]
        public int MinSlavesPerBroker { get; set; } = 5;

        [SettingPropertyInteger("{=sms_mcm_max_slaves}Max Slaves Per Broker", 5, 50, Order = 3, RequireRestart = false,
            HintText = "{=sms_mcm_max_slaves_hint}Maximum number of prisoners a broker can offer.")]
        [SettingPropertyGroup("{=sms_mcm_buymenu}Buy Slave Menu")]
        public int MaxSlavesPerBroker { get; set; } = 15;

        [SettingPropertyInteger("{=sms_mcm_stock_expire}Stock Expiration (Days)", 1, 14, Order = 4,
            RequireRestart = false,
            HintText = "{=sms_mcm_stock_expire_hint}How many days before a broker's stock refreshes.")]
        [SettingPropertyGroup("{=sms_mcm_buymenu}Buy Slave Menu")]
        public int StockExpirationDays { get; set; } = 3;

        // ──────────────────────────── Lord Purchase ────────────────────────────

        [SettingPropertyInteger("{=sms_mcm_max_lords}Max Lord Transfers", 1, 10, Order = 1, RequireRestart = false,
            HintText = "{=sms_mcm_max_lords_hint}Maximum number of heroes you can buy at once.")]
        [SettingPropertyGroup("{=sms_mcm_lord}Lord Purchase")]
        public int MaxLordTransferCount { get; set; } = 2;

        [SettingPropertyFloatingInteger("{=sms_mcm_lord_escape}Lord Escape Chance Per Day", 0.0f, 0.5f, "0.00",
            Order = 2, RequireRestart = false,
            HintText = "{=sms_mcm_lord_escape_hint}Daily chance for a purchased lord to escape during transit.")]
        [SettingPropertyGroup("{=sms_mcm_lord}Lord Purchase")]
        public float LordEscapeChancePerDay { get; set; } = 0.05f;

        [SettingPropertyFloatingInteger("{=sms_mcm_delivery_speed}Delivery Speed Multiplier", 0.5f, 5.0f, "0.0",
            Order = 3, RequireRestart = false,
            HintText = "{=sms_mcm_delivery_speed_hint}Higher values = slower delivery. 1.0 is default speed.")]
        [SettingPropertyGroup("{=sms_mcm_lord}Lord Purchase")]
        public float LordDeliverySpeedMultiplier { get; set; } = 1.0f;

        [SettingPropertyBool("{=sms_mcm_enable_exchange}Enable Prisoner Exchange", Order = 4, RequireRestart = false,
            HintText = "{=sms_mcm_enable_exchange_hint}Enables the 'Cartel' mechanic to exchange captives with other lords.")]
        [SettingPropertyGroup("{=sms_mcm_lord}Lord Purchase")]
        public bool EnablePrisonerExchange { get; set; } = true;

        [SettingPropertyFloatingInteger("{=sms_mcm_crime_multiplier}Crime Rating Multiplier", 0.0f, 5.0f, "0.0",
            Order = 5, RequireRestart = false,
            HintText = "{=sms_mcm_crime_multiplier_hint}Multiplier for criminal rating increase when buying slaves. Set to 0 to disable crime.")]
        [SettingPropertyGroup("{=sms_mcm_lord}Lord Purchase")]
        public float CrimeRatingMultiplier { get; set; } = 1.0f;
        
        [SettingPropertyFloatingInteger("{=sms_mcm_relation_multiplier}Relation Gain Multiplier", 0.0f, 5.0f, "0.0",
            Order = 6, RequireRestart = false,
            HintText = "{=sms_mcm_relation_multiplier_hint}Multiplier for relation gain when buying prisoners from a lord.")]
        [SettingPropertyGroup("{=sms_mcm_lord}Lord Purchase")]
        public float RelationGainMultiplier { get; set; } = 1.0f;

        // ──────────────────────────── Data Management ────────────────────────────

        [SettingPropertyButton("{=sms_mcm_clear_data}Clear All SMS Data", Content = "{=sms_mcm_clear_btn}Clear Data", Order = 1, RequireRestart = false,
            HintText = "{=sms_mcm_clear_data_hint}Click to instantly clear all mod data (pending deliveries, etc).")]
        [SettingPropertyGroup("{=sms_mcm_data}Data Management")]
        public System.Action ClearDataAction { get; set; } = () =>
        {
            SMS.Config.SmsSettingsManager.TriggerClearDataEvent();
        };
    }
}
