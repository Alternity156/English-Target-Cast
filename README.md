# English Target Cast

A [Dalamud](https://github.com/goatcorp/Dalamud) plugin for Final Fantasy XIV that shows the names on your target's cast bar in **English**, whatever language your game client uses.

Handy if you play on a Japanese, German or French client but learned the fights (or watch guides) in English.

## Features

- Translates the cast bar for your **target** (both the built-in cast bar and the split one from HUD Layout) and your **focus target**.
- Covers regular actions, items, key items, object interactions (like "Opening") and mounts.
- Uses the English text that ships with every global client, so it needs no internet connection or translation service.
- Only replaces text it recognizes as the current cast's name, so things like "Interrupted" are left alone.
- Does nothing on an English client.

## Usage

- `/englishcast` opens the settings, where you can turn the target and focus target cast bars on or off separately.
- The settings are also available from the plugin's entry in `/xlplugins`.

## Building

### Prerequisites

- [XIVLauncher](https://goatcorp.github.io/) with Dalamud, and the game run through it at least once (this also keeps Dalamud up to date, which the build depends on).
- [.NET 10 SDK](https://dotnet.microsoft.com/download).
- An IDE such as Visual Studio 2022 or newer, or JetBrains Rider.

### Build and load

1. Open `EnglishTargetCast.slnx` and build. The plugin ends up in `EnglishTargetCast/bin/x64/Debug/EnglishTargetCast/EnglishTargetCast.dll` (or `Release`).
2. In game, run `/xlsettings`, go to **Experimental** and add the full path to `EnglishTargetCast.dll` under **Dev Plugin Locations**.
3. Run `/xlplugins`, go to **Dev Tools > Installed Dev Plugins** and enable **English Target Cast**.

After that, rebuilding and reloading the plugin from `/xlplugins` is enough; no need to restart the game.

## Troubleshooting

If a cast name stays in your client language, open `/xllog`, set the log level to **Debug** and look for lines starting with `Cast bar shows`. They show what the cast bar displayed and what the plugin expected.

After a game patch, the cast bar's layout can change. The text node IDs are at the top of `EnglishTargetCast/CastBarTranslator.cs` and can be checked in game with `/xldev` > **Addon Inspector**.

## License

[AGPL-3.0](LICENSE.md), as required for Dalamud plugins. Based on goatcorp's [SamplePlugin](https://github.com/goatcorp/SamplePlugin) template.
