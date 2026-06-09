# Heels Design Linker

**Current version:** 1.4.0.0

**Source repository:** https://github.com/kyodaikokata/HeelsDesignLinker

> **Not just for heels!** Originally built around SimpleHeels height, this plugin now also matches rules by **what your character is actually wearing** (rendered equipment from DrawObject). Combine height and gear conditions, then auto-apply Glamourer, Penumbra, Moodles, Honorific, or SoundMixer actions.
>
> **不仅限于高跟鞋！** 本插件除 SimpleHeels 高度外，还可根据 **当前渲染装备外观**（DrawObject）匹配规则。可组合高度与装备条件，自动执行 Glamourer、Penumbra、Moodles、Honorific、SoundMixer 等行动。

Install and updates are distributed through the unified **KKT-Catalog** custom plugin repository. Add **one** repo URL in Dalamud (Settings → Custom Plugin Repositories):

| Launcher | URL |
|----------|-----|
| XIVLauncher 国服 (CN) | `https://raw.githubusercontent.com/kyodaikokata/KKT-Catalog/main/pluginmaster.cn.json` |
| XIVLauncher 国际 (Global) | `https://raw.githubusercontent.com/kyodaikokata/KKT-Catalog/main/pluginmaster.global.json` |

> If you previously added `HeelsDesignLinker/main/pluginmaster.*.json`, remove that entry to avoid duplicate listings.

Release build (CN/Global → KKT-Catalog):

```powershell
.\scripts\publish-release.ps1
# 仅国服: .\scripts\publish-release.ps1 -SkipGlobal
```

### Developer documentation

| Document | Description |
|----------|-------------|
| [docs/VERSION-1.3.0.0.md](docs/VERSION-1.3.0.0.md) | **1.3.0** release notes (equipment rules, DrawObject) |
| [docs/ARCHITECTURE-1.2.1.2.md](docs/ARCHITECTURE-1.2.1.2.md) | Runtime flow, gates, apply logic |
| [docs/KNOWN-ISSUES.md](docs/KNOWN-ISSUES.md) | Known issues and troubleshooting |
| [docs/Plugin-IPC-Reference.md](docs/Plugin-IPC-Reference.md) | IPC index |
| [docs/ipc/](docs/ipc/) | Per-plugin IPC reference |

In-game changelog: **更新履历 / Changelog** tab (`/hdl`).

---

## English

Automatically applies **Glamourer** designs, **Penumbra** mod options, and more when a rule matches your **SimpleHeels height** and/or **rendered equipment appearance**.

### What can trigger a rule?

| Condition type | Examples |
|----------------|----------|
| **Height** | `≤ 0.03` for flats, `> 0.05` for heels (SimpleHeels) |
| **Equipment** | Feet must be empty; head must wear a specific ModelId; body must have any gear (excl. Emperor's) |
| **Combined** | Height **and** equipment in the same rule (AND/OR condition groups) |

Equipment checks read **DrawObject Human** (what you see on screen), not Glamourer saved designs. Use **Read current appearance** or **search item name** to pick a ModelId.

### Required / Recommended Plugins

**Strongly Recommended:** **SimpleHeels**, **Glamourer**, **Penumbra**

- **SimpleHeels:** Height-based matching (still the primary use case for heels)
- **Glamourer:** Auto-apply designs when rules match
- **Penumbra:** Auto-switch mod options (default: **temporary apply**, key `-1211`)
- **Moodles / Honorific / SoundMixer:** Optional per-rule actions

### Quick start

1. Open settings: `/hdl` or `/heelsdesign`
2. Create or select a **RuleSet**
3. Configure **SimpleHeels** for the active RuleSet (if using height rules)
4. Add rules (if / else if / else). Drag `: :` to reorder
5. Per rule, add **conditions**:
   - **Height:** comparator + value
   - **Equipment:** slot, must be equipped / unequipped, any gear or specific ModelId
6. Per rule, add **actions** (Glamourer, Penumbra, Moodles, Honorific, SoundMixer)
7. Watch the **status bar** above the tabs (height + matched rule in green)

Default rules on first run: barefoot ≤ -0.01, shoes ≤ 0.03, else heels.

### Recent highlights (1.3.0)

- **Equipment conditions** via DrawObject (not limited to heels use cases)
- Item name search, read current appearance, variant-aware name lookup
- Persistent status bar; AND-group conflict warnings
- See [docs/VERSION-1.3.0.0.md](docs/VERSION-1.3.0.0.md)

---

## 中文

当规则的 **SimpleHeels 高度** 和/或 **当前渲染装备外观** 满足时，自动应用 **Glamourer**、**Penumbra** 等行动。

### 可以用什么触发规则？

| 条件类型 | 示例 |
|----------|------|
| **高度** | `≤ 0.03` 平底鞋、`> 0.05` 高跟鞋（SimpleHeels） |
| **装备** | 脚部必须未装备；头部必须为特定 ModelId；身体必须穿戴任意装备（皇帝套除外） |
| **组合** | 同一规则内高度 **且/或** 装备（条件组 AND/OR） |

装备判定读取 **DrawObject Human**（屏幕上实际渲染的外观），不读 Glamourer 保存的设计。可用 **读取当前外观** 或 **搜索物品名** 选取 ModelId。

### 安装

在 Dalamud → 设置 → 自定义插件库 中添加 **KKT-Catalog** 源（见上文表格）。

### 必需 / 推荐插件

**强烈推荐：** **SimpleHeels**、**Glamourer**、**Penumbra**

- **SimpleHeels：** 高度匹配（高跟鞋场景仍是最常见用法）
- **Glamourer：** 规则命中时自动应用设计
- **Penumbra：** 自动切换 mod 选项（默认 **临时写入**，key `-1211`）
- **Moodles / Honorific / SoundMixer：** 可选规则行动

### 快速上手

1. 打开设置：`/hdl` 或 `/heelsdesign`
2. 创建或选择 **规则集**
3. 配置 **SimpleHeels**（若使用高度条件）
4. 添加规则（如果 / 否则如果 / 否则），拖拽 `: :` 调整顺序
5. 为每条规则添加 **条件**：
   - **高度：** 比较符 + 数值
   - **装备：** 槽位、必须装备/未装备、任意装备或特定 ModelId
6. 添加 **行动**（Glamourer、Penumbra、Moodles、Honorific、SoundMixer）
7. Tab 上方 **常驻状态条** 可查看高度与当前匹配规则（绿色）

首次安装默认规则：≤ -0.01 赤脚、≤ 0.03 鞋子、否则高跟鞋。

### 近期要点（1.3.0）

- **装备条件**（DrawObject），用途不限于高跟鞋
- 物品名搜索、读取当前外观、变体精确匹配
- 常驻状态条；「且」条件组内矛盾红色提示
- 详见 [docs/VERSION-1.3.0.0.md](docs/VERSION-1.3.0.0.md)
