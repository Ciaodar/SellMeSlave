using System.ComponentModel;
using SMS.Config;

namespace SMS.MCMBridge
{
    public static class BridgeBootstrap
    {
        private static SmsMcmSettings? _subscribedInstance;

        public static void TryRegister()
        {
            // Register the provider in the core mod's settings manager
            SmsSettingsManager.RegisterExternalSettingsProvider(BuildModelFromMcm);

            // Initialize the singleton to let MCM know about it and hook PropertyChanged
            var instance = SmsMcmSettings.Instance;
            if (instance != null && !ReferenceEquals(_subscribedInstance, instance))
            {
                if (_subscribedInstance != null)
                {
                    _subscribedInstance.PropertyChanged -= OnMcmPropertyChanged;
                }

                instance.PropertyChanged += OnMcmPropertyChanged;
                _subscribedInstance = instance;
            }
        }

        private static void OnMcmPropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            SmsSettingsManager.Reload();
        }

        private static SmsJsonModel BuildModelFromMcm()
        {
            var mcm = SmsMcmSettings.Instance ?? new SmsMcmSettings();
            return new SmsJsonModel
            {
                SlavePriceMultiplier = mcm.SlavePriceMultiplier,
                EnableRandomization = mcm.EnableRandomization,
                LordPriceMultiplier = mcm.LordPriceMultiplier,
                MaxSlavesPerBroker = mcm.MaxSlavesPerBroker,

                // Buy Slave Menu
                EnableBuySlaveMenu = mcm.EnableBuySlaveMenu,
                MinSlavesPerBroker = mcm.MinSlavesPerBroker,
                StockExpirationDays = mcm.StockExpirationDays,

                // Lord Purchase
                MaxLordTransferCount = mcm.MaxLordTransferCount,
                LordEscapeChancePerDay = mcm.LordEscapeChancePerDay,
                LordDeliverySpeedMultiplier = mcm.LordDeliverySpeedMultiplier,
                EnablePrisonerExchange = mcm.EnablePrisonerExchange,
                CrimeRatingMultiplier = mcm.CrimeRatingMultiplier,
                RelationGainMultiplier = mcm.RelationGainMultiplier,
                RogueryXpMultiplier = mcm.RogueryXpMultiplier,
                HonorLossMultiplier = mcm.HonorLossMultiplier,

                // AI Slave Trade
                EnableAiSlaveTrade = mcm.EnableAiSlaveTrade,
                AiSettlementPurchaseChance = mcm.AiSettlementPurchaseChance,
                AiHourlyTradeChance = mcm.AiHourlyTradeChance,
                AiMaxHonorForTroopPurchase = mcm.AiMaxHonorForTroopPurchase,
                AiMaxHonorForLordPurchase = mcm.AiMaxHonorForLordPurchase
            };
        }
    }
}
