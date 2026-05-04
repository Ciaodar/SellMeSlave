using System;
using System.IO;
using System.Linq;
using System.Reflection;
using SMS.Config;
using SMS.Menu;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace SMS
{
    public class SMSSubModule : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            
            // Try to activate MCM bridge as early as possible
            TryActivateOptionalMcmBridge();
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            
            // Initialize settings (will use MCM if bridge was successful, else JSON)
            SmsSettingsManager.Initialize();
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);

            if (game.GameType is Campaign)
            {
                CampaignGameStarter campaignStarter = (CampaignGameStarter)gameStarterObject;

                // Register Models
                campaignStarter.AddModel(new SMS.Models.SmsRansomValueCalculationModel());

                // Register Behaviors
                campaignStarter.AddBehavior(new Behaviors.BuySlaveBehavior());
            }
        }

        private static void TryActivateOptionalMcmBridge()
        {
            try
            {
                // Check if MCM is present in the domain
                Assembly.Load("MCMv5");

                string gameRoot = BasePath.Name;
                if (string.IsNullOrEmpty(gameRoot)) return;

                string bridgePath = Path.Combine(gameRoot, "Modules", "SellMeSlave", "bin", "Win64_Shipping_Client", "SMS.MCMBridge.dll");

                if (!File.Exists(bridgePath)) return;

                Assembly bridgeAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "SMS.MCMBridge") ?? Assembly.LoadFrom(bridgePath);

                Type? bootstrapType = bridgeAssembly.GetType("SMS.MCMBridge.BridgeBootstrap");
                MethodInfo? tryRegister = bootstrapType?.GetMethod("TryRegister", BindingFlags.Public | BindingFlags.Static);
                tryRegister?.Invoke(null, null);
            }
            catch
            {
                // Silent fail to keep mod running without MCM
            }
        }
    }
}
