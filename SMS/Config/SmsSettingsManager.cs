using System;
using System.IO;
using Newtonsoft.Json;
using TaleWorlds.Library;

namespace SMS.Config
{
    public static class SmsSettingsManager
    {
        private static readonly object SyncRoot = new object();
        private static SmsJsonModel _settings = new SmsJsonModel();
        private static Func<SmsJsonModel>? _externalSettingsProvider;
        private const string ConfigFileName = "config.json";

        public static float SlavePriceMultiplier => SettingsOrDefault().SlavePriceMultiplier;
        public static bool EnableRandomization => SettingsOrDefault().EnableRandomization;
        public static float LordPriceMultiplier => SettingsOrDefault().LordPriceMultiplier;
        public static int MaxSlavesPerBroker => SettingsOrDefault().MaxSlavesPerBroker;

        // Buy Slave Menu settings
        public static bool EnableBuySlaveMenu => SettingsOrDefault().EnableBuySlaveMenu;
        public static int MinSlavesPerBroker => SettingsOrDefault().MinSlavesPerBroker;
        public static int StockExpirationDays => SettingsOrDefault().StockExpirationDays;
        public static int MaxLordTransferCount => SettingsOrDefault().MaxLordTransferCount;
        public static float LordEscapeChancePerDay => SettingsOrDefault().LordEscapeChancePerDay;
        public static float LordDeliverySpeedMultiplier => SettingsOrDefault().LordDeliverySpeedMultiplier;
        public static bool EnablePrisonerExchange => SettingsOrDefault().EnablePrisonerExchange;
        public static float CrimeRatingMultiplier => SettingsOrDefault().CrimeRatingMultiplier;
        public static float RelationGainMultiplier => SettingsOrDefault().RelationGainMultiplier;
        public static float RogueryXpMultiplier => SettingsOrDefault().RogueryXpMultiplier;
        public static float HonorLossMultiplier => SettingsOrDefault().HonorLossMultiplier;

        // AI Slave Trade
        public static bool EnableAiSlaveTrade => SettingsOrDefault().EnableAiSlaveTrade;
        public static float AiSettlementPurchaseChance => SettingsOrDefault().AiSettlementPurchaseChance;
        public static float AiHourlyTradeChance => SettingsOrDefault().AiHourlyTradeChance;
        public static int AiMaxHonorForTroopPurchase => SettingsOrDefault().AiMaxHonorForTroopPurchase;
        public static int AiMaxHonorForLordPurchase => SettingsOrDefault().AiMaxHonorForLordPurchase;

        public static void TriggerClearDataEvent()
        {
            if (TaleWorlds.CampaignSystem.Campaign.Current != null && SMS.Behaviors.BuySlaveBehavior.Instance != null)
            {
                SMS.Behaviors.BuySlaveBehavior.Instance.ClearAllData();
            }
            TaleWorlds.Library.InformationManager.DisplayMessage(
                new TaleWorlds.Library.InformationMessage("SMS Data Cleared!", TaleWorlds.Library.Colors.Green));
        }

        public static void Initialize()
        {
            Reload();
        }

        public static void RegisterExternalSettingsProvider(Func<SmsJsonModel> provider)
        {
            lock (SyncRoot)
            {
                _externalSettingsProvider = provider;
                Reload();
            }
        }

        public static bool Reload()
        {
            lock (SyncRoot)
            {
                if (_externalSettingsProvider != null)
                {
                    try
                    {
                        SmsJsonModel model = _externalSettingsProvider();
                        _settings = model ?? new SmsJsonModel();
                        return true;
                    }
                    catch
                    {
                        // Fallback
                    }
                }

                var defaults = new SmsJsonModel();
                var configPath = GetConfigPath();

                try
                {
                    if (!File.Exists(configPath))
                    {
                        _settings = defaults;
                        SaveInternal(configPath, _settings);
                        return true;
                    }

                    var json = File.ReadAllText(configPath);
                    var loaded = JsonConvert.DeserializeObject<SmsJsonModel>(json);
                    _settings = loaded ?? defaults;
                    SaveInternal(configPath, _settings);
                    return true;
                }
                catch
                {
                    _settings = defaults;
                    return false;
                }
            }
        }

        private static SmsJsonModel SettingsOrDefault() => _settings;

        private static string GetConfigPath()
        {
            var gameRoot = BasePath.Name;
            if (!string.IsNullOrWhiteSpace(gameRoot))
            {
                var moduleConfig = Path.Combine(gameRoot, "Modules", "SellMeSlave", ConfigFileName);
                if (File.Exists(moduleConfig)) return moduleConfig;
            }

            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var fallbackDir = Path.Combine(documents, "Mount and Blade II Bannerlord", "Configs", "SellMeSlave");
            Directory.CreateDirectory(fallbackDir);
            return Path.Combine(fallbackDir, ConfigFileName);
        }

        private static void SaveInternal(string path, SmsJsonModel model)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            var json = JsonConvert.SerializeObject(model, Formatting.Indented);
            File.WriteAllText(path, json);
        }
    }
}
