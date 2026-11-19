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

            // Ensure MCM bridge is activated and subscribed after MCM builds its service container
            TryActivateOptionalMcmBridge();

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

        private static bool _mcmResolverRegistered;
        private static bool _mcmLoadHookRegistered;
        private static bool _isActivatingMcmBridge;

        private static Assembly? FindLoadedAssembly(string shortName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => string.Equals(a.GetName().Name, shortName, StringComparison.OrdinalIgnoreCase));
        }

        private static void EnsureMcmAssemblyResolver()
        {
            if (_mcmResolverRegistered) return;
            AppDomain.CurrentDomain.AssemblyResolve += OnMcmAssemblyResolve;
            _mcmResolverRegistered = true;
        }

        private static Assembly? OnMcmAssemblyResolve(object sender, ResolveEventArgs args)
        {
            try
            {
                if (args == null || string.IsNullOrWhiteSpace(args.Name)) return null;
                string? requestedName = new AssemblyName(args.Name).Name;
                if (string.Equals(requestedName, "MCMv5", StringComparison.OrdinalIgnoreCase))
                {
                    return FindLoadedAssembly("MCMv5");
                }
            }
            catch
            {
                // Ignore malformed assembly names
            }

            return null;
        }

        private static void EnsureMcmAssemblyLoadHook()
        {
            if (_mcmLoadHookRegistered) return;
            AppDomain.CurrentDomain.AssemblyLoad += OnMcmAssemblyLoaded;
            _mcmLoadHookRegistered = true;
        }

        private static void OnMcmAssemblyLoaded(object sender, AssemblyLoadEventArgs args)
        {
            try
            {
                if (string.Equals(args.LoadedAssembly?.GetName().Name, "MCMv5", StringComparison.OrdinalIgnoreCase))
                {
                    AppDomain.CurrentDomain.AssemblyLoad -= OnMcmAssemblyLoaded;
                    _mcmLoadHookRegistered = false;
                    TryActivateOptionalMcmBridge();
                }
            }
            catch
            {
                // Silent fail to keep mod running without MCM
            }
        }

        private static void TryActivateOptionalMcmBridge()
        {
            if (_isActivatingMcmBridge) return;
            _isActivatingMcmBridge = true;

            try
            {
                // Check if MCM is already loaded in the domain; if not, hook AssemblyLoad and try loading
                Assembly? mcmAssembly = FindLoadedAssembly("MCMv5");
                if (mcmAssembly == null)
                {
                    EnsureMcmAssemblyLoadHook();
                    try
                    {
                        mcmAssembly = Assembly.Load("MCMv5");
                    }
                    catch
                    {
                        return;
                    }
                }

                if (mcmAssembly == null) return;

                EnsureMcmAssemblyResolver();

                string assemblyLocation = typeof(SMSSubModule).Assembly.Location;
                if (string.IsNullOrWhiteSpace(assemblyLocation)) return;

                string? binDir = Path.GetDirectoryName(assemblyLocation);
                if (string.IsNullOrWhiteSpace(binDir)) return;

                string bridgePath = Path.Combine(binDir, "SMS.MCMBridge.dll");
                if (!File.Exists(bridgePath)) return;

                Assembly bridgeAssembly = FindLoadedAssembly("SMS.MCMBridge") ?? Assembly.LoadFrom(bridgePath);

                Type? bootstrapType = bridgeAssembly.GetType("SMS.MCMBridge.BridgeBootstrap");
                MethodInfo? tryRegister = bootstrapType?.GetMethod("TryRegister", BindingFlags.Public | BindingFlags.Static);
                tryRegister?.Invoke(null, null);
            }
            catch
            {
                // Silent fail to keep mod running without MCM
            }
            finally
            {
                _isActivatingMcmBridge = false;
            }
        }
    }
}
