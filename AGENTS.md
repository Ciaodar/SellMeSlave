# Agent Knowledge Base: Sell Me Slave (SMS)

This document provides technical insights, architectural patterns, and development guidelines for AI agents and future developers working on the **Sell Me Slave** mod.

## Project Architecture

### 1. Mod Initialization (`SMSSubModule.cs`)
The mod uses a standard `MBSubModuleBase` entry point. It features a late-initialization pattern for settings and an optional bridge for MCM.
- **`OnSubModuleLoad`**: Attempts to activate the MCM bridge.
- **`OnBeforeInitialModuleScreenSetAsRoot`**: Initializes settings (`SmsSettingsManager`).
- **`OnGameStart`**: Registers the custom `SmsRansomValueCalculationModel` and the `BuySlaveBehavior`.

### 2. Optional Dependencies (MCM Bridge)
To avoid a hard dependency on **Mod Configuration Menu (MCM)**, the mod uses a "Bridge" pattern:
- Core logic resides in the `SMS` assembly.
- MCM-specific code resides in `SMS.MCMBridge`.
- `SMSSubModule` checks for the presence of `MCMv5` at runtime. If found, it dynamically loads `SMS.MCMBridge.dll` and invokes `BridgeBootstrap.TryRegister`.
- If MCM is missing, the mod falls back to a standard JSON-based settings system.

### 3. Core Behavior (`BuySlaveBehavior.cs`)
This is the heart of the mod's gameplay logic.
- **Town Stocks**: Manages the persistent availability of prisoners in town centers.
- **Lord Transit**: Handles the delayed delivery of purchased lords. Deliveries are processed in `OnHourlyTick` to ensure responsiveness.
- **Escape Logic**: Processed in `OnDailyTick`. Escape probability is weighted by the lord's value (purchase price).
- **Criminal Penalties**: Proportional Roguery XP and Crime Rating increases are applied via `ApplyCriminalConsequences`.

### 4. Deterministic Price Calculation (`SlavePriceCalculator.cs`)
The mod implements a deterministic price engine to prevent price flickering (e.g., when reopening a menu).
- **Seeding**: Seeds `System.Random` using `(int)CampaignTime.Now.ToDays + character.StringId.GetHashCode()`.
- **Logic**: Prices remain stable for the entire game day but vary between different characters and across different days.
- **Native Interaction**: When calculating lord prices, it instantiates a fresh `DefaultRansomValueCalculationModel` directly to bypass its own model overrides and get the true "Native" base price.

## Development Guidelines

### Internationalization
The project has been fully internationalized.
- **Source Code**: All comments, variable names, and logic must be in **English**.
- **User Interface**: All user-facing strings must use the localization system (`{=ID}Text`).
- **Translations**: Localized XMLs are located in `ModuleData/Languages`. Ensure both `EN` and `TR` are updated when adding new strings.

### Key Patterns to Maintain
- **Static Accessors**: Use `BuySlaveBehavior.Instance` for cross-component communication where appropriate.
- **Save Compatibility**: Use `SMSSaveDefiner` to register custom data types (`TownPrisonerStock`, `LordDeliveryData`) for serialization.
- **Defensive Programming**: Always check for `null` on `Campaign.Current` or `Hero.MainHero` during initialization phases.

### 5. Settings Architecture (`SmsSettingsManager`)
- Static class with property-expression accessors (e.g., `SlavePriceMultiplier => SettingsOrDefault().SlavePriceMultiplier`).
- Thread-safe with `lock(SyncRoot)`.
- Supports `RegisterExternalSettingsProvider(Func<SmsJsonModel>)` for bridge injection.
- Config path priority: `Modules/SellMeSlave/config.json` → `Documents/Mount and Blade II Bannerlord/Configs/SellMeSlave/config.json`.
- Includes `TriggerClearDataEvent()` for dev/debug data reset.

### 6. Automatic Version Management
Build-time version bumping via `SMS/Build/UpdateVersion.ps1`:
- **Version format**: `vMAJOR.UPDATE.HOTFIX.DEBUG` (e.g., `v1.0.0.28`).
- **Debug build**: Increments DEBUG segment.
- **Hotfix build**: Increments HOTFIX and DEBUG, does NOT reset DEBUG.
- **Update build**: Increments UPDATE and DEBUG, resets HOTFIX.
- Script is invoked by MSBuild `UpdateVersion` target in `SMS.csproj` before `PostBuildEvent`.
- Returns exit code 1 on failure to halt the build.

## File Structure

- `SMS/Actions`: Discrete transaction logic.
- `SMS/Behaviors`: Long-running campaign event listeners.
- `SMS/Calculators`: Mathematical engines (Price, XP, Crime).
- `SMS/Config`: Settings management (MCM vs. JSON).
- `SMS/Data`: Serializable data structures.
- `SMS/Menu`: Game menu registration and callbacks.
- `SMS/Models`: Overridden Native game models.
- `SMS.MCMBridge`: Reflection-based bridge for MCM.
