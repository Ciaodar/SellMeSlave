using SMS.Config;

namespace SMS.MCMBridge
{
    public static class BridgeBootstrap
    {
        public static void TryRegister()
        {
            // Initialize the singleton to let MCM know about it
            var instance = SmsMcmSettings.Instance;

            // Register the provider in the core mod's settings manager
            SmsSettingsManager.RegisterExternalSettingsProvider(BuildModelFromMcm);

            // Hook into MCM's property changed event to apply settings immediately
            if (instance != null)
            {
                instance.PropertyChanged += (sender, args) =>
                {
                    SmsSettingsManager.Reload();
                };
            }
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
                LordDeliverySpeedMultiplier = mcm.LordDeliverySpeedMultiplier
            };
        }
    }
}
