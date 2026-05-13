# Sell Me Slave (SMS)

A Mount & Blade II: Bannerlord mod that introduces an illegal prisoner trade system, allowing players to purchase common prisoners and noble lords from town centers—for a price.

## Features

- **Illegal Prisoner Trade**: Buy prisoner stacks directly from town centers through a dedicated game menu.
- **Noble Lord Procurement**: Purchase captured lords and have them delivered to your party.
- **Realistic Transit System**: Noble prisoners aren't delivered instantly. They take time to be transported to your location, during which they might attempt to escape.
- **Criminal Consequences**: Engaging in the slave trade is illegal. You will gain **Roguery XP**, but your **Crime Rating** with the local faction will increase.
- **Dynamic Pricing**: Prisoner prices are calculated based on their tier, skills, and equipment.
- **MCM Integration**: Fully configurable through the Mod Configuration Menu (MCM).
- **Localization**: Full support for English and Turkish languages.

## Requirements

- **Mount & Blade II: Bannerlord** (v1.2.0+)
- **Harmony** (Required)
- **ButterLib** (Required)
- **UIExtenderEx** (Required)
- **Mod Configuration Menu (MCM)** (Optional, but recommended for settings)

## Installation

1. Download the mod from [Nexus Mods](https://www.nexusmods.com/mountandblade2bannerlord/mods/TODO).
2. Extract the `SellMeSlave` folder into your Bannerlord `Modules` directory.
3. Enable the mod in the Bannerlord launcher.
4. Ensure it is loaded after the required library mods (Harmony, ButterLib, UIExtenderEx, MCM).

## Configuration

If you have MCM installed, you can configure the following settings in-game:
- **Lord Transit Time**: How many hours it takes for a purchased lord to reach your party.
- **Lord Escape Chance**: The daily probability of a lord escaping during transit.
- **Price Multipliers**: Adjust the cost of common prisoners and lords.
- **Crime Rating Penalty**: Adjust how much your reputation suffers per gold spent.

## Development

This mod is developed by **Ciaodar**. It follows a modular architecture with a bridge system for optional dependencies like MCM.

## License

[MIT License](LICENSE)
