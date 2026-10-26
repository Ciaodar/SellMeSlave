using System;
using System.IO;
using System.Linq;
using System.Reflection;
using SMS.Config;
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
                campaignStarter.AddModel(new Models.SmsRansomValueCalculationModel());

                // Register Behaviors
                campaignStarter.AddBehavior(new Behaviors.BuySlaveBehavior());
                campaignStarter.AddBehavior(new Behaviors.LordTradeDialogueBehavior());
                campaignStarter.AddBehavior(new Behaviors.AiBuySlaveBehavior());
                campaignStarter.AddBehavior(new Behaviors.AlliedRansomBehavior());
            }
        }

        private static void TryActivateOptionalMcmBridge()
        {
            try
            {
                // Check if MCM is present in the domain
                Assembly.Load("MCMv5");

                string assemblyLocation = typeof(SMSSubModule).Assembly.Location;
                if (string.IsNullOrWhiteSpace(assemblyLocation)) return;

                string? binDir = Path.GetDirectoryName(assemblyLocation);
                if (string.IsNullOrWhiteSpace(binDir)) return;

                string bridgePath = Path.Combine(binDir, "SMS.MCMBridge.dll");

                if (!File.Exists(bridgePath)) return;

                Assembly bridgeAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => string.Equals(a.GetName().Name, "SMS.MCMBridge", StringComparison.OrdinalIgnoreCase)) ?? Assembly.LoadFrom(bridgePath);

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
