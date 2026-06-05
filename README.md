# Heels Design Linker

**Repository:** https://github.com/kyodaikokata/HeelsDesignLinker

Custom plugin repo URL:

```
https://raw.githubusercontent.com/kyodaikokata/HeelsDesignLinker/main/pluginmaster.json
```

---

## English

Automatically applies **Glamourer** designs or **Penumbra** mod options when your **SimpleHeels** height matches a rule. Optional **Honorific** titles and **Moodles** statuses/presets per rule.

**Requires:** SimpleHeels. Glamourer and/or Penumbra depending on mode. Moodles / Honorific only if you use those options.

### Quick start

1. Open settings: `/hdl` or `/heelsdesign`
2. Choose **Glamourer** or **Penumbra** mode
3. Add height rules (if / else if / else, top to bottom). Drag `::` to reorder
4. Per rule, add one or more **actions**:
   - **Glamourer:** pick a design per action
   - **Penumbra:** set Collection, mod, option group, and option name per action
5. Enable rules. Height changes trigger the first matching rule and run all its actions

Default rules on first run: barefoot ≤ -0.01, shoes ≤ 0.03, else heels.

---

## 中文

根据 **SimpleHeels** 高度自动应用 **Glamourer** 设计或 **Penumbra** mod 选项。每条规则还可选用 **Honorific** 称号与 **Moodles** 状态/预设。

**依赖：** SimpleHeels（必需）；按模式需要 Glamourer 和/或 Penumbra；仅在使用对应选项时需要 Moodles / Honorific。

### 快速上手

1. 打开设置：`/hdl` 或 `/heelsdesign`
2. 选择 **Glamourer** 或 **Penumbra** 模式
3. 添加高度规则（如果 / 否则如果 / 否则，从上到下匹配），拖拽 `::` 调整顺序
4. 每条规则可添加多条 **行动**：
   - **Glamourer：** 每条行动选择一个设计
   - **Penumbra：** 每条行动单独设置 Collection、模组、Option 组与选项名
5. 启用规则后，高度变化会命中第一条匹配规则并执行其全部行动

首次安装默认规则：≤ -0.01 赤脚、≤ 0.03 鞋子、否则高跟鞋。
