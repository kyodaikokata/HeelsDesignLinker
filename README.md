# Heels Design Linker

**Repository:** https://github.com/kyodaikokata/HeelsDesignLinker

Custom plugin repo URL:

```
https://raw.githubusercontent.com/kyodaikokata/HeelsDesignLinker/main/pluginmaster.json
```

---

## English

Automatically applies **Glamourer** designs or **Penumbra** mod options when your **SimpleHeels** height matches a rule. Optional **Honorific** titles and **Moodles** statuses/presets per rule.

### Required / Recommended Plugins

**Strongly Recommended:** **SimpleHeels**, **Glamourer**, **Penumbra**

While this plugin can technically run without them, you won't have any use for it if you don't install these plugins. This plugin is designed to automatically trigger actions based on SimpleHeels height changes and control Glamourer/Penumbra/Moodles/Honorific accordingly.

- **SimpleHeels:** Required for height-based rule matching
- **Glamourer:** If you want to automatically apply Glamourer designs
- **Penumbra:** If you want to automatically switch Penumbra mod options
- **Moodles:** Optional, if you want to apply Moodles statuses/presets
- **Honorific:** Optional, if you want to set Honorific titles

### Quick start

1. Open settings: `/hdl` or `/heelsdesign`
2. Create or select a **RuleSet** (you can have multiple independent rule configurations)
3. Configure **SimpleHeels** settings for the active RuleSet (enable/disable, Default/Actual mode)
4. Add height rules (if / else if / else, top to bottom). Drag `: :` to reorder
5. Per rule, add one or more **actions**:
   - **Glamourer:** pick a design per action
   - **Penumbra:** set Collection, mod, option group, and option name per action
   - **Honorific:** set a title
   - **Moodles:** pick a status or preset
6. Enable rules. Height changes trigger the first matching rule and run all its actions

Default rules on first run: barefoot ≤ -0.01, shoes ≤ 0.03, else heels.

---

## 中文

根据 **SimpleHeels** 高度自动应用 **Glamourer** 设计或 **Penumbra** mod 选项。每条规则还可选用 **Honorific** 称号与 **Moodles** 状态/预设。

### 必需 / 推荐插件

**强烈推荐安装：** **SimpleHeels**、**Glamourer**、**Penumbra**

虽然本插件在技术上可以不安装这些插件而运行，但如果你不安装这些插件，本插件对你来说也完全没有用处。本插件的设计目的是根据 SimpleHeels 的高度变化自动触发行动，并相应地控制 Glamourer/Penumbra/Moodles/Honorific。

- **SimpleHeels：** 基于高度的规则匹配必需
- **Glamourer：** 如果你想自动应用 Glamourer 设计
- **Penumbra：** 如果你想自动切换 Penumbra mod 选项
- **Moodles：** 可选，如果你想应用 Moodles 状态/预设
- **Honorific：** 可选，如果你想设置 Honorific 称号

### 快速上手

1. 打开设置：`/hdl` 或 `/heelsdesign`
2. 创建或选择一个 **规则集**（你可以拥有多套独立的规则配置）
3. 为当前活动规则集配置 **SimpleHeels** 设置（启用/禁用、Default/Actual 模式）
4. 添加高度规则（如果 / 否则如果 / 否则，从上到下匹配），拖拽 `: :` 调整顺序
5. 每条规则可添加多条 **行动**：
   - **Glamourer：** 每条行动选择一个设计
   - **Penumbra：** 每条行动单独设置 Collection、模组、Option 组与选项名
   - **Honorific：** 设置称号
   - **Moodles：** 选择状态或预设
6. 启用规则后，高度变化会命中第一条匹配规则并执行其全部行动

首次安装默认规则：≤ -0.01 赤脚、≤ 0.03 鞋子、否则高跟鞋。
