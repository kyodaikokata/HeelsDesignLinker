# Heels Design Linker | 高跟鞋设计联动器

[English](#english) | [中文](#中文)

---

## 中文

一个基于 SimpleHeels 高度检测自动切换 Glamourer 设计或 Penumbra Mod 设置的 Dalamud 插件。

### ✨ 功能特性

- 🔄 **双模式支持**：同时支持 Glamourer 和 Penumbra 两种模式
- 📏 **高度规则**：自定义高度范围，实现自动切换
- 🟢 **规则高亮**：当前高度匹配的规则以绿色高亮，便于确认
- ⚙️ **灵活配置**：可调节小数精度和规则管理
- 🎯 **实时检测**：即时响应 SimpleHeels 高度变化
- 🌐 **IPC 集成**：与 SimpleHeels 插件无缝通信
- 🌐 **中英文界面**：随系统语言自动切换
- 📜 **更新履历**：插件内可查看版本更新记录

### 📋 前置要求

- **SimpleHeels** 插件（必需）
- **Glamourer** 插件（Glamourer 模式需要）
- **Penumbra** 插件（Penumbra 模式需要）
- Dalamud API Level 15+
- .NET 10.0

### 📥 安装方法

#### 方法一：自定义仓库（推荐）

1. 游戏内输入 `/xlsettings`
2. 点击 "Experimental（实验性）" 标签页
3. 在 "Custom Plugin Repositories（自定义插件仓库）" 中添加以下地址：
   ```
   https://raw.githubusercontent.com/kyodaikokata/HeelsDesignLinker/main/pluginmaster.json
   ```
4. 保存并关闭设置
5. 输入 `/xlplugins` 搜索 "Heels Design Linker"
6. 点击安装

#### 方法二：手动安装

1. 从 [Releases](https://github.com/kyodaikokata/HeelsToggle/releases) 下载 `latest.zip`
2. 解压到 `%AppData%\XIVLauncher\installedPlugins\HeelsToggle\`
3. 重启游戏

### 🎮 使用说明

#### 快速开始

1. 打开插件配置：`/hdl` 或 `/heelsdesign`
2. 选择模式：
   - **Glamourer 模式**：自动应用 Glamourer 设计
   - **Penumbra 模式**：自动切换 Penumbra Mod 设置
3. 配置高度范围和对应的设计/设置
4. 启用规则，享受自动切换！

#### Glamourer 模式

为不同高度范围配置设计名称：

```
高度 -1.00 ~ -0.01   → 设计: "SFX_Barefoot"
高度 -0.009 ~ 0.03   → 设计: "SFX_Shoes"
高度  0.031 ~ 1.00   → 设计: "SFX_Heels"
```

首次安装时会自动创建以上默认规则（可在设置中修改）。

**示例配置：**
- 使用 Glamourer 创建不同的外观设计
- 在插件中为每个高度范围指定对应的设计名称
- 当 SimpleHeels 检测到高度变化时，自动应用对应设计

#### Penumbra 模式

配置 Mod 设置：

1. **全局设置：**
   - 集合（Collection）：`Default`（首次安装默认）
   - Mod 名称、选项：留空，按你的 Mod 自行填写

2. **为每条规则设置选项名称：**
   - `Barefoot`（赤足）
   - `Shoes`（鞋子）
   - `Heels`（高跟鞋）

**示例配置：**
- 确保 Penumbra 中已安装相应的 Mod
- 在插件中设置 Mod 名称和选项名称
- 当高度变化时，插件会自动切换到对应的 Mod 选项

### ⚙️ 配置界面

#### 设置标签页

- **模式选择**：在 Glamourer 和 Penumbra 之间切换
- **小数精度**：调整高度显示的小数位数（0-5 位）
- **规则管理**：添加、编辑或删除基于高度的规则

#### 调试标签页

- 查看实时 SimpleHeels IPC 数据
- 测试 IPC 连接
- 查看命令示例
- 检查插件状态

#### 更新履历标签页

- 查看各版本更新内容（与 README 更新日志同步）

### 💡 使用技巧

1. **高度范围设置**：建议设置一定的重叠区间，避免频繁切换
2. **命令格式**：
   - Glamourer：设计名称不需要加引号
   - Penumbra：确保 Collection、Mod、Option 名称与游戏内一致
3. **调试模式**：遇到问题时，可在调试标签页查看原始数据

### 🐛 问题反馈

- **问题报告**：[GitHub Issues](https://github.com/kyodaikokata/HeelsDesignLinker/issues)
- **源代码**：[GitHub Repository](https://github.com/kyodaikokata/HeelsDesignLinker)

### 🙏 致谢

- **作者**：KKT
- **Dalamud**：[goatcorp/Dalamud](https://github.com/goatcorp/Dalamud)
- **SimpleHeels**：[Caraxi/SimpleHeels](https://github.com/Caraxi/SimpleHeels)
- **Glamourer**：[Ottermandias/Glamourer](https://github.com/Ottermandias/Glamourer)
- **Penumbra**：[xivdev/Penumbra](https://github.com/xivdev/Penumbra)
- **Immersive Footsteps**:[XIVModArchive](https://www.xivmodarchive.com/modid/95777)

### 📄 开源许可

本项目采用 MIT 许可证开源。

### 📝 更新日志

#### 版本 1.0.1

- 游戏内命令：`/hdl`、`/heelsdesign`（打开配置窗口）
- 新增「更新履历」标签页
- 修复插件加载顺序，启动后无需再手动开关即可识别依赖
- 当前匹配规则绿色高亮显示
- 界面中英文本地化（随系统语言）
- 默认高度规则：`-1～-0.01`、`-0.009～0.03`、`0.031～1.0`
- Penumbra 默认 Collection 为 `Default`，Mod / Option 默认为空

#### 版本 1.0.0

- 首次发布
- Glamourer 模式支持
- Penumbra 模式支持
- 可配置高度范围
- 可调节小数精度
- SimpleHeels IPC 集成
- 选项卡式 UI（设置 & 调试）

---

## English

A Dalamud plugin that automatically switches Glamourer designs or Penumbra mod settings based on SimpleHeels height detection.

### ✨ Features

- 🔄 **Dual Mode Support**: Works with both Glamourer and Penumbra
- 📏 **Height-based Rules**: Define custom height ranges for automatic switching
- 🟢 **Rule Highlight**: Matched rule highlighted in green for easy verification
- ⚙️ **Flexible Configuration**: Adjustable decimal precision and rule management
- 🎯 **Real-time Detection**: Instant response to SimpleHeels height changes
- 🌐 **IPC Integration**: Seamless communication with SimpleHeels plugin
- 🌐 **Localization**: Chinese / English UI based on system language
- 📜 **Changelog Tab**: View version history in-game

### 📋 Requirements

- **SimpleHeels** plugin (required)
- **Glamourer** plugin (for Glamourer mode)
- **Penumbra** plugin (for Penumbra mode)
- Dalamud API Level 15+
- .NET 10.0

### 📥 Installation

#### Method 1: Custom Repository (Recommended)

1. Type `/xlsettings` in game
2. Go to "Experimental" tab
3. Add this URL to "Custom Plugin Repositories":
   ```
   https://raw.githubusercontent.com/kyodaikokata/HeelsDesignLinker/main/pluginmaster.json
   ```
4. Save and close settings
5. Type `/xlplugins` and search for "Heels Design Linker"
6. Click Install

#### Method 2: Manual Installation

1. Download `latest.zip` from [Releases](https://github.com/kyodaikokata/HeelsDesignLinker/releases)
2. Extract to `%AppData%\XIVLauncher\installedPlugins\HeelsToggle\`
3. Restart the game

### 🎮 Usage

#### Quick Start

1. Open plugin configuration: `/hdl` or `/heelsdesign`
2. Choose your mode:
   - **Glamourer Mode**: Auto-apply Glamourer designs
   - **Penumbra Mode**: Auto-switch Penumbra mod settings
3. Configure height ranges and corresponding designs/settings
4. Enable rules and enjoy automatic switching!

#### Glamourer Mode

Configure design names for different height ranges:

```
Height -1.00 ~ -0.01   → Design: "SFX_Barefoot"
Height -0.009 ~ 0.03   → Design: "SFX_Shoes"
Height  0.031 ~ 1.00   → Design: "SFX_Heels"
```

Default rules are created on first run (editable in settings).

#### Penumbra Mode

Configure mod settings:

1. Set global settings:
   - Collection: `Default` (default on first install)
   - Mod Name and Option: leave empty and fill in per your mod
2. Set option names for each rule:
   - `Barefoot`
   - `Shoes`
   - `Heels`

### ⚙️ Configuration

#### Settings Tab

- **Mode Selection**: Choose between Glamourer and Penumbra
- **Decimal Precision**: Adjust height display precision (0-5 decimal places)
- **Rule Management**: Add, edit, or remove height-based rules

#### Debug Tab

- View real-time SimpleHeels IPC data
- Test IPC connection
- View command examples
- Check plugin status

#### Changelog Tab

- View release notes for each version

### 🐛 Support

- **Issues**: [GitHub Issues](https://github.com/kyodaikokata/HeelsDesignLinker/issues)
- **Source Code**: [GitHub Repository](https://github.com/kyodaikokata/HeelsDesignLinker)

### 🙏 Credits

- **Author**: KKT
- **Dalamud**: [goatcorp/Dalamud](https://github.com/goatcorp/Dalamud)
- **SimpleHeels**: [Caraxi/SimpleHeels](https://github.com/Caraxi/SimpleHeels)
- **Glamourer**: [Ottermandias/Glamourer](https://github.com/Ottermandias/Glamourer)
- **Penumbra**: [xivdev/Penumbra](https://github.com/xivdev/Penumbra)
- **Immersive Footsteps**:[XIVModArchive](https://www.xivmodarchive.com/modid/95777)

### 📄 License

This project is licensed under the MIT License.

### 📝 Changelog

#### Version 1.0.1

- In-game commands: `/hdl`, `/heelsdesign` (open configuration)
- Changelog tab in plugin UI
- Fixed load order so dependencies are detected on startup
- Green highlight for the currently matched rule
- Chinese / English UI localization
- Default height rules: `-1～-0.01`, `-0.009～0.03`, `0.031～1.0`
- Penumbra default Collection `Default`; Mod / Option empty by default

#### Version 1.0.0

- Initial release
- Glamourer mode support
- Penumbra mode support
- Configurable height ranges
- Adjustable decimal precision
- IPC integration with SimpleHeels
- Tabbed UI (Settings & Debug)

