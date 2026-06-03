# Heels Design Linker

A Dalamud plugin that automatically switches Glamourer designs or Penumbra mod settings based on SimpleHeels height detection.

## Features

- 🔄 **Dual Mode Support**: Works with both Glamourer and Penumbra
- 📏 **Height-based Rules**: Define custom height ranges for automatic switching
- ⚙️ **Flexible Configuration**: Adjustable decimal precision and rule management
- 🎯 **Real-time Detection**: Instant response to SimpleHeels height changes
- 🌐 **IPC Integration**: Seamless communication with SimpleHeels plugin

## Requirements

- **SimpleHeels** plugin (required)
- **Glamourer** plugin (for Glamourer mode)
- **Penumbra** plugin (for Penumbra mode)
- Dalamud API Level 15+
- .NET 10.0

## Installation

### Method 1: Custom Repository (Recommended)

1. Type `/xlsettings` in game
2. Go to "Experimental" tab
3. Add this URL to "Custom Plugin Repositories":
  ```
   https://raw.githubusercontent.com/kyodaikokata/HeelsDesignLinker/main/pluginmaster.json
  ```
4. Save and close settings
5. Type `/xlplugins` and search for "Heels Design Linker"
6. Click Install

### Method 2: Manual Installation

1. Download `latest.zip` from [Releases](https://github.com/YOUR_USERNAME/HeelsToggle/releases)
2. Extract to `%AppData%\XIVLauncher\installedPlugins\HeelsToggle\`
3. Restart the game

## Usage

### Quick Start

1. Open plugin configuration: `/hgl`
2. Choose your mode:
  - **Glamourer Mode**: Auto-apply Glamourer designs
  - **Penumbra Mode**: Auto-switch Penumbra mod settings
3. Configure height ranges and corresponding designs/settings
4. Enable rules and enjoy automatic switching!

### Glamourer Mode

Configure design names for different height ranges:

```
Height -1.00 ~ -0.01 → Design: "SFX_Barefoot"
Height -0.01 ~ 0.05  → Design: "SFX_Shoes"
Height  0.05 ~ 99.00 → Design: "SFX_Heels"
```

### Penumbra Mode

Configure mod settings:

1. Set global settings:
  - Collection: `Default`
  - Mod Name: `Immersive Footsteps`
  - Option: `Footsteps`
2. Set option names for each rule:
  - `Barefoot`
  - `Shoes`
  - `Heels`

## Configuration

### Settings Tab

- **Mode Selection**: Choose between Glamourer and Penumbra
- **Decimal Precision**: Adjust height display precision (0-5 decimal places)
- **Rule Management**: Add, edit, or remove height-based rules

### Debug Tab

- View real-time SimpleHeels IPC data
- Test IPC connection
- View command examples
- Check plugin status

## Support

- **Issues**: [GitHub Issues](https://github.com/YOUR_USERNAME/HeelsToggle/issues)
- **Source Code**: [GitHub Repository](https://github.com/YOUR_USERNAME/HeelsToggle)

## Credits

- **Author**: KKT
- **Dalamud**: [goatcorp/Dalamud](https://github.com/goatcorp/Dalamud)
- **SimpleHeels**: [Caraxi/SimpleHeels](https://github.com/Caraxi/SimpleHeels)
- **Glamourer**: [Ottermandias/Glamourer](https://github.com/Ottermandias/Glamourer)
- **Penumbra**: [xivdev/Penumbra](https://github.com/xivdev/Penumbra)

## License

This project is licensed under the MIT License.

## Changelog

### Version 1.0.0

- Initial release
- Glamourer mode support
- Penumbra mode support
- Configurable height ranges
- Adjustable decimal precision
- IPC integration with SimpleHeels
- Tabbed UI (Settings & Debug)

