using System.Collections.Generic;
using System.Globalization;

namespace HeelsDesignLinker
{
    public enum UiLanguagePreference
    {
        System = 0,
        Chinese = 1,
        English = 2,
    }

    public static class Localization
    {
        private static UiLanguagePreference _languagePreference = UiLanguagePreference.System;

        public static void SetLanguagePreference(UiLanguagePreference preference) =>
            _languagePreference = preference;

        private static string SystemLanguage => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

        public static bool IsChine => _languagePreference switch
        {
            UiLanguagePreference.Chinese => true,
            UiLanguagePreference.English => false,
            _ => SystemLanguage == "zh",
        };

        public static string LanguagePreview(UiLanguagePreference preference) => preference switch
        {
            UiLanguagePreference.Chinese => LanguageChinese,
            UiLanguagePreference.English => LanguageEnglish,
            _ => LanguageSystem,
        };
        
        // 插件元数据
        public static string PluginName => IsChine ? "Heels Design Linker" : "Heels Design Linker";
        public static string PluginPunchline => IsChine 
            ? "不限于高跟鞋：按 SimpleHeels 高度或当前渲染装备外观匹配规则，自动联动 Glamourer/Penumbra 等" 
            : "Not just for heels: match rules by SimpleHeels height or rendered equipment, then drive Glamourer/Penumbra and more";
        
        // 命令帮助
        public static string CommandHelp => IsChine 
            ? "打开配置；/hdl sfw|nsfw 切换 SFW 临时层；/hdl toggle|pen 切换 Penumbra 直接写入" 
            : "Open settings; /hdl sfw|nsfw toggles SFW temp layer; /hdl toggle|pen toggles direct Penumbra apply";
        
        // 窗口标题
        public static string WindowTitle => $"{PluginName} v{Changelog.CurrentVersion}";

        public static string StatusBarHeight(string height) => IsChine ? $"高度 {height}" : $"H {height}";

        public static string ShowDtrStatusBar => IsChine
            ? "在游戏顶部状态栏显示匹配信息"
            : "Show match info in game status bar (DTR)";

        public static string ShowDtrStatusBarTooltip => IsChine
            ? "在屏幕顶部服务器信息栏显示当前高度与匹配规则；点击可打开配置窗口。"
            : "Shows current height and matched rule in the server info bar at the top of the screen. Click to open settings.";

        public static string ShowDtrPenumbraToggleBar => IsChine
            ? "显示 DTR Penumbra 切换按钮"
            : "Show DTR Penumbra toggle button";

        public static string ShowDtrPenumbraToggleBarTooltip => IsChine
            ? "在屏幕顶部服务器信息栏显示「HDL Pen」按钮；点击一次性写入/还原 Penumbra Collection 设置，不参与规则匹配。"
            : "Shows the \"HDL Pen\" button in the server info bar; click to apply/revert Penumbra collection settings once. Does not interact with rule matching.";
        
        public static string DtrBarTooltip => IsChine
            ? "Heels Design Linker：点击打开配置"
            : "Heels Design Linker: click to open settings";

        public static string WindowMatchSummary(int index, string? ruleName)
        {
            if (!string.IsNullOrWhiteSpace(ruleName))
                return IsChine ? $"规则 #{index + 1}: {ruleName}" : $"Rule #{index + 1}: {ruleName}";

            return RuleMatched(index);
        }
        
        // 标签页
        public static string TabRules => IsChine ? "规则" : "Rules";
        public static string TabSfwMode => IsChine ? "SFW 模式" : "SFW Mode";
        public static string TabPenumbraToggle => IsChine ? "Penumbra 切换" : "Penumbra Toggle";
        public static string TabSettings => IsChine ? "设置" : "Settings";

        public static string SfwModeTabDescription => IsChine
            ? "全局 SFW 模式：激活后按下方 Penumbra 行动覆盖规则层（独立临时 key -1210，在规则 -1211 之后 apply）。与规则中相同 Mod 的行动将被拦截，关闭 SFW 后会重新 apply 规则。"
            : "Global SFW mode: when active, Penumbra actions below overlay the rule layer (separate temp key -1210, applied after rules -1211). Rule actions on the same mod are skipped; disabling SFW re-applies rules.";

        public static string SfwModeActiveLabel => IsChine ? "SFW 模式已激活" : "SFW mode active";
        public static string SfwModeActiveTooltip => IsChine
            ? "开启后应用下方行动列表；关闭时清除 SFW 临时层并重新 apply 被拦截的规则 Penumbra 行动。"
            : "When on, applies the action list below; when off, clears the SFW temp layer and re-applies rule Penumbra actions that were skipped.";

        public static string SfwModeToggleLabel => IsChine ? "SFW" : "SFW";
        public static string SfwModeToggleTooltip => SfwModeActiveTooltip;

        public static string SfwModeRequiresTemporaryApply => IsChine
            ? "SFW 模式需要开启「临时写入模式」（见设置页 Penumbra）。"
            : "SFW mode requires Temporary apply mode (see Penumbra settings).";

        public static string SfwImportModSettings => IsChine ? "导入 Mod 设置" : "Import mod settings";
        public static string SfwImportModSettingsTooltip => IsChine
            ? "在 Penumbra 窗口中选中一个 Mod 后点击，从当前角色的激活 Collection 读取该 Mod 的生效设置并生成行动列表（同 Mod 再次导入会覆盖旧条目）。"
            : "Select a mod in Penumbra, then click to import its effective settings from your active collection (re-import replaces entries for that mod).";

        public static string SfwImportSelectedMod(string modDirectory) => IsChine
            ? $"Penumbra 当前选中：{modDirectory}"
            : $"Penumbra selected: {modDirectory}";

        public static string SfwImportNoSelectedModHint => IsChine
            ? "请在 Penumbra 窗口中选中一个 Mod（打开 Mod 页并点击列表中的 Mod）。"
            : "Select a mod in Penumbra (Mods tab, click a mod in the list).";

        public static string SfwImportCurrentCollection(string collection) => IsChine
            ? $"Penumbra 当前 Collection：{collection}"
            : $"Penumbra current collection: {collection}";

        public static string SfwImportSuccess(string collection, string modDirectory, int actionCount) => IsChine
            ? $"已导入 [{collection}] {modDirectory}：{actionCount} 条行动"
            : $"Imported [{collection}] {modDirectory}: {actionCount} action(s)";

        public static string PenumbraActionGroupTitle(string collection, string modLabel) => IsChine
            ? $"Penumbra 行动组 · [{collection}] {modLabel}"
            : $"Penumbra group · [{collection}] {modLabel}";

        public static string PenumbraActionGroupSummary(string collection, string modLabel, int subActionCount) => IsChine
            ? $"[{collection}] {modLabel} · {subActionCount} 条子行动"
            : $"[{collection}] {modLabel} · {subActionCount} sub-action(s)";

        public static string PenumbraActionGroupCollapseTooltip => IsChine
            ? "折叠 / 展开 Penumbra 行动组"
            : "Collapse / expand Penumbra action group";

        public static string DeletePenumbraActionGroup => IsChine ? "删除组" : "Delete group";
        public static string DeletePenumbraActionGroupTooltip => IsChine
            ? "删除此 Penumbra 行动组及其全部子行动"
            : "Delete this Penumbra group and all sub-actions";

        public static string AddPenumbraActionGroup => IsChine ? "+ 添加 Penumbra 行动组" : "+ Add Penumbra group";
        public static string AddPenumbraSubAction => IsChine ? "+ 添加子行动" : "+ Add sub-action";

        public static string SfwImportNoActions => IsChine
            ? "该 Mod 无可用设置可导入"
            : "No settings to import for this mod";

        public static string SfwModeActionsEmpty => IsChine
            ? "列表为空，请添加 Penumbra 行动组"
            : "List is empty — add a Penumbra action group";

        public static string DtrSfwBarActive => IsChine ? "SFW 开" : "SFW On";
        public static string DtrSfwBarInactive => IsChine ? "SFW 关" : "SFW Off";
        public static string DtrSfwBarTooltip => IsChine
            ? "点击切换 SFW / NSFW 模式（Penumbra 临时层）"
            : "Click to toggle SFW / NSFW mode (Penumbra temporary layer)";

        public static string PenumbraToggleModeTabDescription => IsChine
            ? "配置 DTR「HDL Pen」按钮切换时要写入 Penumbra Collection 的行动（Penumbra.TrySetMod，等同 /penumbra mod enable|disable）。仅在点击按钮时 apply，不参与规则匹配、dedup 或临时层。"
            : "Configure actions applied to the Penumbra collection when toggling the DTR \"HDL Pen\" button (Penumbra.TrySetMod, same as /penumbra mod enable|disable). Applies only on click; does not interact with rule matching, dedup, or temp layers.";

        public static string PenumbraToggleModeActionsEmpty => IsChine
            ? "列表为空，请添加 Penumbra 行动组"
            : "List is empty — add a Penumbra action group";

        public static string DtrPenumbraToggleBarActive => IsChine ? "Pen 开" : "Pen On";
        public static string DtrPenumbraToggleBarInactive => IsChine ? "Pen 关" : "Pen Off";
        public static string DtrPenumbraToggleBarTooltip => IsChine
            ? "点击切换：按「Penumbra 切换」页行动列表永久写入/还原 Penumbra Collection"
            : "Click to toggle: apply/revert Penumbra collection per the Penumbra Toggle tab action list";

        public static string TabDebug => IsChine ? "调试" : "Debug";
        public static string TabChangelog => IsChine ? "更新履历" : "Changelog";
        public static string ChangelogVersion => IsChine ? "版本" : "Version";
        public static string ChangelogDate => IsChine ? "日期" : "Date";
        public static string KoFiSupportButton => IsChine ? "支持作者 (Ko-fi)" : "Support on Ko-fi";
        public static string KoFiSupportTooltip => IsChine
            ? "在浏览器中打开 Ko-fi 页面"
            : "Open the Ko-fi page in your browser";
        
        // 设置标签页
        public static string CurrentHeight => IsChine ? "当前高度" : "Current Height";
        public static string SimpleHeelsHeightSource => IsChine ? "SimpleHeels 高度" : "SimpleHeels Height";
        public static string SimpleHeelsHeightDefault => IsChine ? "默认高度" : "Default";
        public static string SimpleHeelsHeightActual => IsChine ? "实际高度" : "Actual";
        public static string SimpleHeelsHeightManual => IsChine ? "手动" : "Manual";
        public static string SimpleHeelsHeightDefaultTooltip => IsChine
            ? "使用 SimpleHeels IPC 的 DefaultOffset 匹配规则"
            : "Match rules using SimpleHeels IPC DefaultOffset";
        public static string SimpleHeelsHeightActualTooltip => IsChine
            ? "使用受 TempOffset 影响的实际高度；无临时偏移时与默认高度相同"
            : "Match rules using actual height affected by TempOffset; equals default when no temp offset";
        public static string SimpleHeelsHeightManualTooltip => IsChine
            ? "手动输入高度值，不依赖 SimpleHeels（用于测试或 SimpleHeels 不可用时）"
            : "Manually input height value, independent of SimpleHeels (for testing or when SimpleHeels is unavailable)";
        public static string ManualHeightValue => IsChine ? "手动高度" : "Manual Height";
        public static string ManualHeightTooltip => IsChine
            ? "拖拽滑块或直接输入数值设置当前高度，规则会根据此值匹配"
            : "Drag slider or input value to set current height; rules will match based on this value";
        public static string ResetToZero => IsChine ? "重置为 0" : "Reset to 0";
        public static string ManualModeHint => IsChine
            ? "手动模式：可用于测试规则或在 SimpleHeels 不可用时使用。拖动滑块查看不同高度下的规则匹配。"
            : "Manual mode: useful for testing rules or when SimpleHeels is unavailable. Drag the slider to see rule matching at different heights.";
        public static string SimpleHeelsNotAvailableHint => IsChine
            ? "SimpleHeels 不可用，已自动切换到手动模式。你可以通过滑块设置高度来测试规则。"
            : "SimpleHeels is unavailable; automatically switched to manual mode. Use the slider to set height for testing rules.";
        public static string DoubleClickToInput => IsChine ? "双击或Ctrl+点击以手动输入数值" : "Double-click or Ctrl+click to input value manually";

        public static string PenumbraSettingsSection => IsChine ? "Penumbra" : "Penumbra";
        public static string PenumbraSettingsSectionTooltip => IsChine
            ? "控制规则触发的 Penumbra 写入方式及与 Glamourer 临时设置的冲突处理"
            : "Controls how rule-triggered Penumbra writes apply and how Glamourer temporary conflicts are handled";
        public static string PenumbraTemporaryApply => IsChine ? "临时写入模式（推荐）" : "Temporary apply mode (recommended)";
        public static string PenumbraTemporaryApplyTooltip => IsChine
            ? "开启时规则 apply 写入 Penumbra 临时层（登出/规则切换后恢复）；关闭则永久改写 collection 内 Mod 设定"
            : "When on, rule apply writes Penumbra temporary settings (restored on logout/rule change); when off, permanently changes collection mod settings";
        public static string PenumbraDisableTemporaryConfirmMessage => IsChine
            ? "关闭临时写入后，规则 apply 将永久改变 Penumbra 模组设定，不会随登出或规则切换自动恢复。确定要继续吗？"
            : "Disabling temporary apply will permanently change Penumbra mod settings in your collection. Changes will not revert on logout or rule switch. Continue?";
        public static string PenumbraAutoOverwriteGlamourer => IsChine
            ? "自动覆盖 Glamourer 临时设置"
            : "Auto-overwrite Glamourer temporary settings";
        public static string PenumbraAutoOverwriteGlamourerTooltip => IsChine
            ? "开启后，apply 前若 Glamourer 已占用同一 Mod 的临时层，将自动清除再 apply；关闭时需在各 Penumbra 行动中单独授权"
            : "When on, clears Glamourer temporary settings on the same mod before apply; when off, authorize per Penumbra action";
        
        // 条件系统
        public static string ConditionMode => IsChine ? "条件模式" : "Condition Mode";
        public static string ConditionModeNone => IsChine ? "无条件（Else）" : "None (Else)";
        public static string ConditionModeAnd => IsChine ? "全部满足（AND）" : "All conditions (AND)";
        public static string ConditionModeOr => IsChine ? "任一满足（OR）" : "Any condition (OR)";
        public static string Conditions => IsChine ? "条件列表" : "Conditions";
        public static string AddCondition => IsChine ? "添加条件" : "Add Condition";
        public static string RemoveCondition => IsChine ? "删除条件" : "Remove Condition";
        public static string ConditionTypeHeight => IsChine ? "高度条件" : "Height Condition";
        public static string ConditionTypeEquipment => IsChine ? "装备条件" : "Equipment Condition";
        public static string SelectConditionType => IsChine ? "选择条件类型..." : "Select condition type...";
        public static string HeightConditionLabel => IsChine ? "高度" : "Height";
        public static string EquipmentSlot => IsChine ? "装备槽位" : "Equipment Slot";
        public static string MustBeEquipped => IsChine ? "必须装备" : "Must be equipped";
        public static string MustNotBeEquipped => IsChine ? "必须未装备" : "Must not be equipped";
        public static string EquipmentMatchAny => IsChine ? "任意装备（皇帝套除外）" : "Any equipment (excl. Emperor's)";
        public static string EquipmentMatchSpecificModelId => IsChine ? "特定 ModelId" : "Specific ModelId";
        public static string EquipmentMatchModeLabel => IsChine ? "匹配方式" : "Match by";
        public static string EquipmentTargetModelIdLabel => IsChine ? "ModelId" : "ModelId";
        public static string EquipmentReadCurrentModelId => IsChine ? "读取当前外观" : "Read current appearance";
        public static string EquipmentItemSearchHint => IsChine ? "搜索物品名…" : "Search item name…";
        public static string EquipmentItemSearchNoResults => IsChine ? "（无匹配物品）" : "(No matching items)";
        public static string EquipmentItemSearchResult(string itemName, ushort modelId, byte variant) => IsChine
            ? $"{itemName}（ModelId {modelId} · 变体 {variant}）"
            : $"{itemName} (ModelId {modelId} · variant {variant})";
        public static string EquipmentItemSearchResultNoVariant(string itemName, ushort modelId) => IsChine
            ? $"{itemName}（ModelId {modelId}）"
            : $"{itemName} (ModelId {modelId})";
        public static string EquipmentModelIdWeaponsUnsupported => IsChine
            ? "主手/副手不支持按 DrawObject ModelId 匹配，请使用「任意装备」"
            : "Main/off hand cannot match DrawObject ModelId; use Any equipment";
        public static string EquipmentModelIdReadFailed => IsChine ? "无法读取当前外观 ModelId" : "Failed to read current appearance ModelId";
        public static string EquipmentModelIdPreview(string itemName, ushort modelId) => IsChine
            ? $"预览：{itemName}（ModelId {modelId}）"
            : $"Preview: {itemName} (ModelId {modelId})";
        public static string EquipmentModelIdPreviewNoMatch(ushort modelId) => IsChine
            ? $"预览：无匹配物品名（ModelId {modelId}）"
            : $"Preview: no item name (ModelId {modelId})";
        public static string EquipmentMustNotSpecificHint => IsChine
            ? "当前槽位外观不是该 ModelId 时满足（光脚或其它模型也算满足）"
            : "Satisfied when the slot appearance is not this ModelId (bare or another model counts)";
        public static string ConditionHint => IsChine 
            ? "选择 None 表示无条件（Else分支），选择 AND/OR 后添加条件" 
            : "Select None for unconditional (Else), or AND/OR to add conditions";
        public static string AddConditionGroup => IsChine ? "添加条件组" : "Add Condition Group";
        public static string AddConditionGroupTooltip => IsChine
            ? "添加新的条件组，支持更复杂的逻辑（例如 (A || B) && C）"
            : "Add a new condition group for complex logic (e.g., (A || B) && C)";
        public static string DeleteConditionGroup => IsChine ? "删除此条件组" : "Delete this condition group";
        public static string ConfirmDeleteConditionGroup => IsChine ? "确认删除此条件组？" : "Delete this condition group?";
        public static string ConditionGroupLabel(int index) => IsChine ? $"条件组 #{index + 1}" : $"Group #{index + 1}";
        public static string RuleSfwModeEnabledLabel => IsChine ? "SFW 模式参与" : "Active in SFW mode";
        public static string RuleSfwModeEnabledTooltip => IsChine
            ? "关闭后，全局 SFW 模式激活时该规则不参与匹配与应用（非 SFW 模式下始终参与）。"
            : "When off, this rule is skipped for matching and apply while global SFW mode is active (always participates when SFW is off).";
        public static string RuleSfwModeStatusBadge(bool enabled) => enabled
            ? (IsChine ? "[SFW:参与]" : "[SFW:on]")
            : (IsChine ? "[SFW:跳过]" : "[SFW:off]");
        public static string ActionSfwModeEnabledLabel => IsChine ? "SFW 模式参与" : "Active in SFW mode";
        public static string ActionSfwModeEnabledTooltip => IsChine
            ? "关闭后，全局 SFW 模式激活时该行动不参与应用（非 SFW 模式下始终应用）。"
            : "When off, this action is skipped for apply while global SFW mode is active (always applies when SFW is off).";
        public static string ActionSfwModeStatusBadge(bool enabled) => enabled
            ? (IsChine ? "[SFW:参与]" : "[SFW:on]")
            : (IsChine ? "[SFW:跳过]" : "[SFW:off]");
        public static string ConnectToNextGroup => IsChine
            ? "选择如何连接到下一个条件组"
            : "Choose how to connect to the next condition group";
        
        // 装备槽位名称
        public static string EquipSlotName(EquipSlot slot) => slot switch
        {
            EquipSlot.MainHand => IsChine ? "主手" : "Main Hand",
            EquipSlot.OffHand => IsChine ? "副手" : "Off Hand",
            EquipSlot.Head => IsChine ? "头部" : "Head",
            EquipSlot.Body => IsChine ? "上衣" : "Body",
            EquipSlot.Hands => IsChine ? "手部" : "Hands",
            EquipSlot.Legs => IsChine ? "裤子" : "Legs",
            EquipSlot.Feet => IsChine ? "鞋子" : "Feet",
            EquipSlot.Ears => IsChine ? "耳饰" : "Ears",
            EquipSlot.Neck => IsChine ? "项链" : "Neck",
            EquipSlot.Wrists => IsChine ? "手镯" : "Wrists",
            EquipSlot.RFinger => IsChine ? "右戒指" : "Right Ring",
            EquipSlot.LFinger => IsChine ? "左戒指" : "Left Ring",
            _ => slot.ToString()
        };
        
        public static string SimpleHeelsTempActiveSuffix => IsChine ? "，含临时偏移" : ", temp active";
        public static string Mode => IsChine ? "模式" : "Mode";
        public static string GlamourerMode => IsChine ? "Glamourer 模式" : "Glamourer Mode";
        public static string PenumbraMode => IsChine ? "Penumbra 模式" : "Penumbra Mode";
        public static string ActionTypeText => IsChine ? "行动类型" : "Action Type";
        public static string ActionTypeLabel(ActionType actionType) => actionType switch
        {
            HeelsDesignLinker.ActionType.Glamourer => IsChine ? "Glamourer 设计" : "Glamourer Design",
            HeelsDesignLinker.ActionType.Penumbra => IsChine ? "Penumbra Mod" : "Penumbra Mod",
            // HeelsDesignLinker.ActionType.CustomizePlus => IsChine ? "Customize+ 配置" : "Customize+ Profile",  // 已移除
            HeelsDesignLinker.ActionType.Honorific => IsChine ? "Honorific 称号" : "Honorific Title",
            HeelsDesignLinker.ActionType.Moodles => IsChine ? "Moodles 状态" : "Moodles Status",
            HeelsDesignLinker.ActionType.SoundMixer => IsChine ? "SoundMixer 音量" : "SoundMixer Audio",
            _ => "Unknown"
        };

        public static string SoundMixerActionKindLabel(SoundMixerActionKind kind) => kind switch
        {
            SoundMixerActionKind.TemporaryPreset => IsChine ? "临时预设" : "Temporary preset",
            SoundMixerActionKind.TemporaryGroupVolume => IsChine ? "临时单组音量" : "Temporary group volume",
            _ => kind.ToString(),
        };
        public static string RulesActionGuideHeader => IsChine ? "行动类型说明" : "Action types";
        public static string PenumbraPriorityWarning => IsChine
            ? "本插件不会验证 Penumbra Mod 优先级，请在 Penumbra 中手动设置优先级。"
            : "This plugin does not verify Penumbra mod priority — set priorities manually in Penumbra.";
        public static string GlamourerSlotConflictNote => IsChine
            ? "本插件会按装备槽位检测 Glamourer 行动冲突，并按优先级应用（数值越大越优先、越后覆盖）；仅检测装备槽位实际是否被应用。"
            : "This plugin checks Glamourer action conflicts by equipment slot and applies them by priority (higher value = higher priority, applied last and wins); only equipment slots actually applied are checked.";
        public static string GlamourerPriorityLabel => IsChine ? "优先级" : "Priority";
        public static string GlamourerPriorityTooltip => IsChine
            ? "数值越大越优先：越后应用，冲突的装备槽位由高优先级胜出；非冲突槽位共存。"
            : "Higher value = higher priority: applied later, wins on conflicting equipment slots; non-conflicting slots coexist.";
        public static string GlamourerSlotConflictUnresolvedHint => IsChine
            ? "未解决的装备槽位冲突：与另一个可同时应用的 Glamourer 行动在相同槽位冲突，且优先级相同（无法决定胜负）。请调整优先级。"
            : "Unresolved equipment-slot conflict: collides with another co-applied Glamourer action on the same slot at equal priority. Adjust priorities.";
        public static string GlamourerSlotConflictResolvedHint => IsChine
            ? "已解决的装备槽位冲突：与另一个可同时应用的 Glamourer 行动在相同槽位冲突，但优先级不同，由高优先级胜出。"
            : "Resolved equipment-slot conflict: collides with another co-applied Glamourer action on the same slot, but priorities differ so the higher one wins.";
        public static string RuleConnectorPrefix => IsChine ? "-> 与下一条规则：" : "-> Link to next rule:";
        public static string RuleConnectorAnd => IsChine ? "新分组（结束本组，独立共存）" : "New group (end this group, coexist)";
        public static string RuleConnectorOr => IsChine ? "同一分组（if/否则如果/否则 链，互斥）" : "Same group (if/elseif/else chain, exclusive)";
        public static string RuleConnectorAndTooltip => IsChine
            ? "新分组：在此结束当前 if/否则如果/否则 分组，下一条规则开启一个独立的新分组。各分组相互独立、可同时命中并叠加应用（后匹配覆盖前匹配）。"
            : "New group: end the current if/elseif/else group here; the next rule starts a separate group. Groups are independent, can all match, and apply on top of each other (later overrides earlier).";
        public static string RuleConnectorOrTooltip => IsChine
            ? "同一分组（默认）：下一条规则与本条属于同一 if/否则如果/否则 链，组内互斥（命中一条即止，否则兜底）。"
            : "Same group (default): the next rule belongs to the same if/elseif/else chain; within a group the first match wins and 'else' is the fallback.";
        public static string RuleGroupHeader(int ordinal) => IsChine ? $"分组 {ordinal}" : $"Group {ordinal}";
        public static string RuleGroupHeaderTooltip => IsChine
            ? "一个分组是一条独立的 if/否则如果/否则 链：组内互斥、首条命中即止、否则兜底。多个分组相互独立，可同时命中并叠加应用。"
            : "A group is one independent if/elseif/else chain: exclusive within the group, first match wins, 'else' is the fallback. Multiple groups are independent and can all match and stack.";
        public static string RuleGroupNameHint => IsChine ? "分组名称（可选）" : "Group name (optional)";
        public static string RuleGroupNameTooltip => IsChine
            ? "为该分组命名，便于识别（仅显示用途，不影响匹配）。"
            : "Name this group for readability (display only; does not affect matching).";
        public static string RuleGroupRuleCount(int count) => IsChine ? $"（{count} 条规则）" : $"({count} rule{(count == 1 ? "" : "s")})";
        public static string ExpandGroupTooltip => IsChine ? "展开本分组" : "Expand this group";
        public static string CollapseGroupTooltip => IsChine ? "折叠本分组（隐藏组内所有规则）" : "Collapse this group (hide all rules in it)";
        public static string GroupDragHandleTooltip => IsChine ? "拖拽以整体调整分组顺序" : "Drag to reorder the whole group";
        public static string GroupDragPreview(int order) => IsChine ? $"移动分组 #{order}" : $"Move group #{order}";
        public static string PerActionTypeHint => IsChine
            ? "每个行动可独立选择类型（Glamourer / Penumbra / Honorific / Moodles / SoundMixer），不再有全局模式。"
            : "Each action independently chooses its type (Glamourer / Penumbra / Honorific / Moodles / SoundMixer). There is no global mode.";
        public static string ModeSeparateRulesHint => IsChine
            ? "每个行动可独立选择类型；同一规则内可混合使用不同类型的行动。"
            : "Each action may use a different type; a single rule can mix Glamourer, Penumbra, Honorific, Moodles and SoundMixer actions.";

        public static string RuleSetLabel => IsChine ? "规则集:" : "Rule Set:";
        public static string RuleSetNew => IsChine ? "新建" : "New";
        public static string RuleSetRename => IsChine ? "重命名" : "Rename";
        public static string RuleSetCopy => IsChine ? "复制" : "Copy";
        public static string RuleSetDelete => IsChine ? "删除" : "Delete";
        public static string RuleSetNewDefaultName => IsChine ? "新规则集" : "New Rule Set";
        public static string RuleSetDefaultName => IsChine ? "默认规则集" : "Default Rule Set";
        public static string RuleSetNamePrompt => IsChine ? "规则集名称:" : "Rule Set Name:";
        public static string RuleSetRenamePrompt => IsChine ? "新名称:" : "New Name:";
        public static string RuleSetCreate => IsChine ? "创建" : "Create";
        public static string RuleSetConfirmDelete => IsChine ? "确定删除" : "Confirm Delete";
        public static string RuleSetDeleteConfirmMessage(string name) => IsChine
            ? $"确定要删除规则集「{name}」吗？此操作无法撤销！"
            : $"Delete rule set \"{name}\"? This cannot be undone!";
        public static string UseSimpleHeels => IsChine ? "使用 SimpleHeels" : "Use SimpleHeels";
        public static string SimpleHeelsSection => IsChine ? "SimpleHeels" : "SimpleHeels";
        public static string SimpleHeelsSectionDisabled => IsChine ? "已关闭" : "Off";

        public static string Ok => IsChine ? "确定" : "OK";
        public static string Confirm => IsChine ? "确认" : "Confirm";
        public static string RuleLabel(int index) => IsChine ? $"规则 {index}" : $"Rule {index}";
        public static string RuleNameLabel => IsChine ? "名称:" : "Name:";
        public static string ConditionGroupCount(int count) => IsChine
            ? $"{count} 个条件组"
            : $"{count} condition group(s)";
        public static string RuleActionCount(int count) => IsChine
            ? $"  {count} 个行动"
            : $"  {count} action(s)";
        public static string NoConditionsSummary => IsChine ? "无条件" : "No conditions";
        public static string HeightAndEquipmentSummary => IsChine ? "高度 + 装备" : "Height + Equipment";
        public static string HeightOnlySummary => IsChine ? "高度" : "Height";
        public static string EquipmentOnlySummary => IsChine ? "装备" : "Equipment";
        public static string EmptyConditionGroupsSummary => IsChine ? "空条件组" : "Empty condition groups";
        public static string EnableDisableRuleTooltip => IsChine ? "启用/禁用规则" : "Enable or disable rule";
        public static string ExpandRuleTooltip => IsChine ? "展开规则" : "Expand rule";
        public static string CollapseRuleTooltip => IsChine ? "折叠规则" : "Collapse rule";
        public static string PenumbraActionTypeLabel => IsChine ? "Penumbra 操作类型:" : "Penumbra action type:";
        public static string PenumbraActionSetModOption => IsChine ? "设置 Mod 选项" : "Set mod option";
        public static string PenumbraActionEnableMod => IsChine ? "启用 Mod" : "Enable mod";
        public static string PenumbraActionDisableMod => IsChine ? "禁用 Mod" : "Disable mod";
        public static string PenumbraEnableShort => IsChine ? "启用" : "Enable";
        public static string PenumbraDisableShort => IsChine ? "禁用" : "Disable";
        
        public static string PenumbraSettings => IsChine ? "Penumbra 设置" : "Penumbra Settings";
        public static string Collection => IsChine ? "Collection（集合）" : "Collection";
        public static string ModName => IsChine ? "Mod Name（模组名称）" : "Mod Name";
        public static string Option => IsChine ? "Option（选项）" : "Option";
        
        public static string DecimalPrecision => IsChine ? "小数精度" : "Decimal Precision";
        public static string Places => IsChine ? "位" : "places";
        public static string ApplyCooldown => IsChine ? "应用冷却" : "Apply Cooldown";
        public static string ApplyCooldownHint => IsChine
            ? "两次自动应用（Glamourer/Penumbra/Moodles/Honorific）的最短间隔；0 = 不限制（默认 1 秒）"
            : "Minimum seconds between auto-applies (Glamourer/Penumbra/Moodles/Honorific); 0 = no limit (default 1s)";
        public static string RuleMatchStableDelay => IsChine ? "规则匹配稳定延迟" : "Rule Match Stable Delay";
        public static string RuleMatchStableDelayHint => IsChine
            ? "同一规则需持续匹配该时长后才会执行行动，可减轻切图/加载时高度抖动误触发；0 = 不等待（默认 0.5 秒）"
            : "Actions run only after the same rule stays matched for this long; reduces false triggers during zone loads. 0 = no wait (default 0.5s)";
        public static string BaselineMoodleRuleApplyDelay => IsChine
            ? "基准→规则 Moodles 延迟"
            : "Baseline→Rule Moodles Delay";
        public static string BaselineMoodleRuleApplyDelayHint => IsChine
            ? "基准 Moodles 成功 apply 后，等待该时长再 apply 规则 Moodles，避免 Moodles 清 buff 覆盖规则结果（0.1～3 秒，默认 0.1 秒）"
            : "After baseline Moodles apply, wait this long before rule Moodles apply, so Moodles buff clears do not overwrite rule results (0.1–3s, default 0.1s)";
        public static string RuleMatchStableWait(double remainingSeconds) => IsChine
            ? $"规则匹配稳定等待 {remainingSeconds:F2}s"
            : $"Rule match stable wait {remainingSeconds:F2}s";
        public static string RestoreDefaults => IsChine ? "恢复默认配置" : "Restore Defaults";
        public static string RestoreDefaultsTooltip => IsChine
            ? "将所有规则与设置恢复为出厂默认值"
            : "Reset all rules and settings to factory defaults";
        public static string RestoreDefaultsConfirmMessage => IsChine
            ? "将清除全部规则集与设置，并恢复为默认值。此操作不可撤销，请再次点击「确定恢复」以继续。"
            : "This clears all rule sets and settings and restores defaults. This cannot be undone. Click \"Confirm Restore\" again to proceed.";
        public static string RestoreDefaultsConfirm => IsChine ? "确定恢复" : "Confirm Restore";
        public static string Cancel => IsChine ? "取消" : "Cancel";
        
        public static string HeightRules => IsChine ? "匹配规则" : "Match Rules";
        public static string HeightRulesOrderHint => IsChine
            ? "规则按从上到下的顺序匹配：命中第一条后不再检查后续规则（如果 / 否则如果 / 否则）。每条规则可组合高度条件与装备外观条件（读取 DrawObject 渲染结果）。最后一条可设为「否则」兜底；「否则」后不能再有规则。可拖拽左侧握把调整顺序。"
            : "Rules are evaluated top to bottom (if / else if / else). Each rule can combine height and rendered equipment conditions (DrawObject). The last rule may be an else fallback; rules after else are unreachable. Drag the left handle to reorder.";
        public static string HeightValue => IsChine ? "高度" : "Height";
        public static string RuleDragHandle => ": :";
        public static string RuleDragHandleTooltip => IsChine ? "拖拽以调整规则顺序" : "Drag to reorder rules";
        public static string RuleDragPreview(int order) => IsChine ? $"移动规则 #{order}" : $"Move rule #{order}";
        public static string RuleOrderIf => IsChine ? "如果" : "IF";
        public static string RuleBranchLabel(RuleBranchKind branch) => branch switch
        {
            RuleBranchKind.Else => IsChine ? "否则" : "ELSE",
            _ => IsChine ? "否则如果" : "ELSE IF",
        };
        public static string RuleElseNoCondition => IsChine ? "（无条件）" : "(no condition)";
        public static string RuleBranchElseInvalidHint => IsChine
            ? "「否则」之后不能再有规则，请调整顺序或改回「否则如果」"
            : "No rules may follow an else branch. Reorder or switch back to else if.";
        public static string HeightComparisonSymbol(HeightComparison comparison) => comparison switch
        {
            HeightComparison.GreaterThan => ">",
            HeightComparison.GreaterThanOrEqual => ">=",
            HeightComparison.LessThan => "<",
            HeightComparison.LessThanOrEqual => "<=",
            HeightComparison.Equal => "=",
            _ => comparison.ToString(),
        };

        public static string HeightComparisonTooltip(HeightComparison comparison) => comparison switch
        {
            HeightComparison.GreaterThan => IsChine ? "大于" : "Greater than",
            HeightComparison.GreaterThanOrEqual => IsChine ? "大于等于" : "Greater or equal",
            HeightComparison.LessThan => IsChine ? "小于" : "Less than",
            HeightComparison.LessThanOrEqual => IsChine ? "小于等于" : "Less or equal",
            HeightComparison.Equal => IsChine ? "等于" : "Equal to",
            _ => comparison.ToString(),
        };

        public static string RuleConditionHeightConflictHint => IsChine
            ? "与同组其他高度条件矛盾，在「且」逻辑下无法同时满足"
            : "Conflicts with another height condition in this group; cannot all be true under AND";
        public static string RuleConditionEquipmentConflictHint => IsChine
            ? "与同组同槽位装备条件矛盾，在「且」逻辑下无法同时满足"
            : "Conflicts with another equipment condition on the same slot under AND";
        public static string RuleUnreachableHint => IsChine
            ? "该规则永远不会被匹配：前面已有「否则」分支、已覆盖所有可能高度，或无法执行到此"
            : "This rule can never match: a prior else branch, full height coverage, or blocked execution.";
        public static string DesignName => IsChine ? "设计名称" : "Design Name";
        public static string SelectGlamourerDesign => IsChine ? "选择 Glamourer 设计…" : "Select Glamourer design…";
        public static string GlamourerDesignListEmpty => IsChine ? "（无可用设计）" : "(No designs available)";
        public static string GlamourerDesignManualFallback => IsChine
            ? "Glamourer 不可用，手动输入设计名"
            : "Glamourer unavailable — enter design name";
        public static string GlamourerDesignNotInList(string designName) => IsChine
            ? $"{designName}（未在列表中）"
            : $"{designName} (not in list)";
        public static string OptionName => IsChine ? "选项名称" : "Option Name";
        public static string Active => IsChine ? "启用" : "Active";
        public static string Delete => IsChine ? "删除" : "Delete";
        public static string AddNewRule => IsChine ? "添加新规则" : "Add New Rule";
        public static string AddRuleAction => IsChine ? "+ 添加行动" : "+ Add action";
        public static string DeleteRuleAction => IsChine ? "删除行动" : "Remove action";
        public static string RuleActionLabel(int index) => IsChine ? $"行动 {index}" : $"Action {index}";
        public static string RuleActionCollapseTooltip => IsChine ? "折叠 / 展开行动" : "Collapse / expand action";
        public static string RulePenumbraModEnableDisableConflictHint => IsChine
            ? "与同规则内其他 Penumbra 行动冲突：同一 Collection 的同一 Mod 同时启用与禁用"
            : "Conflicts with another Penumbra action in this rule: the same collection and mod are both enabled and disabled";
        public static string RulePenumbraDisableBlocksOptionHint => IsChine
            ? "与同规则内其他 Penumbra 行动冲突：禁用 Mod 时临时模式不会应用同 Mod 的选项设置（选项行动将被跳过）"
            : "Conflicts with another Penumbra action: Disable Mod skips option apply for the same mod in temporary mode";
        public static string RulePenumbraOptionConflictHint => IsChine
            ? "与同规则内其他 Penumbra 行动冲突：同一 Collection、同一 Mod 的同一选项组设置了不同效果"
            : "Conflicts with another Penumbra action in this rule: the same collection, mod, and option group have different settings";
        public static string RulePenumbraMultiToggleConflictHint => IsChine
            ? "与同规则内其他 Penumbra 行动冲突：同一选项组内的子选项设置了相反的开关状态"
            : "Conflicts with another Penumbra action in this rule: opposite toggle states for the same sub-option";
        public static string RulePenumbraMatchedRuleConflictHint => IsChine
            ? "与其它命中规则的 Penumbra 行动在同一 Mod 上冲突（按列表顺序后覆盖前）"
            : "Conflicts with a Penumbra action in another matched rule on the same mod (later rule overrides earlier)";
        public static string RulePenumbraConflictWithActions(IEnumerable<int> actionNumbers)
        {
            var list = string.Join("、", actionNumbers);
            return IsChine ? $"（与行动 {list} 冲突）" : $"(conflicts with action {list})";
        }
        public static string Save => IsChine ? "保存" : "Save";
        
        public static string CurrentMatchedRule => IsChine ? "当前匹配规则" : "Current Matched Rule";
        
        // 调试标签页
        public static string LanguageLabel => IsChine ? "语言" : "Language";
        public static string LanguageSystem => IsChine ? "跟随系统" : "System default";
        public static string LanguageChinese => "中文";
        public static string LanguageEnglish => "English";

        public static string PluginStatus => IsChine ? "插件状态" : "Plugin Status";
        public static string SimpleHeelsStatus => IsChine ? "SimpleHeels 状态" : "SimpleHeels Status";
        public static string GlamourerStatus => IsChine ? "Glamourer 状态" : "Glamourer Status";
        public static string PenumbraStatus => IsChine ? "Penumbra 状态" : "Penumbra Status";
        public static string PenumbraIpcReady => IsChine ? "IPC 可用（静默切换）" : "IPC ready (silent apply)";
        public static string SoundMixerStatus => IsChine ? "SoundMixer 状态" : "SoundMixer Status";
        public static string SoundMixerIpcReady => IsChine ? "IPC 已连接" : "IPC connected";
        public static string SoundMixerIpcVersion(int version, int expected) => IsChine
            ? $"API 版本 {version}（期望 {expected}）"
            : $"API version {version} (expected {expected})";
        public static string SoundMixerIpcVersionMismatch => IsChine
            ? "API 版本不匹配，部分 IPC 可能不可用"
            : "API version mismatch; some IPC calls may fail";
        public static string SoundMixerIpcGatesHeader => IsChine ? "SoundMixer IPC Gate 探测" : "SoundMixer IPC gate probe";
        public static string SoundMixerIpcOverridesActive => IsChine
            ? "本插件临时覆盖：活跃"
            : "This plugin temporary overrides: active";
        public static string SoundMixerIpcOverridesInactive => IsChine
            ? "本插件临时覆盖：无"
            : "This plugin temporary overrides: none";
        public static string SoundMixerUnavailable => IsChine ? "SoundMixer 不可用" : "SoundMixer unavailable";
        public static string SoundMixerActionKindHeader => IsChine ? "SoundMixer 操作:" : "SoundMixer action:";
        public static string SoundMixerPresetLabel => IsChine ? "临时预设:" : "Temporary preset:";
        public static string SoundMixerGroupLabel => IsChine ? "音量组:" : "Volume group:";
        public static string SoundMixerVolumeLabel => IsChine ? "目标音量 (%)" : "Target volume (%)";
        public static string SoundMixerVolumeRangeTooltip => IsChine
            ? "范围 0%–350%（与 SoundMixer 引擎听感上限一致）"
            : "Range 0%–350% (matches SoundMixer audible engine cap)";
        public static string SoundMixerPriorityLabel => IsChine ? "优先级:" : "Priority:";
        public static string SoundMixerTemporaryHint => IsChine
            ? "使用 SoundMixer 临时覆盖 API；规则切换或登出时自动清除本插件的临时设置。"
            : "Uses SoundMixer temporary override APIs; cleared when rules change or on logout.";
        public static string NoneSelected => IsChine ? "<无>" : "<None>";
        public static string Refresh => IsChine ? "刷新" : "Refresh";
        public static string Available => IsChine ? "可用" : "Available";
        public static string NotAvailable => IsChine ? "不可用" : "Not Available";
        
        public static string CurrentMode => IsChine ? "当前模式" : "Current Mode";
        public static string LastApplied => IsChine ? "最后应用" : "Last Applied";
        public static string ApplyGate => IsChine ? "自动应用门控" : "Auto-apply gate";
        
        public static string ErrorInfo => IsChine ? "错误信息" : "Error Info";
        public static string TestIPC => IsChine ? "测试 SimpleHeels IPC" : "Test SimpleHeels IPC";
        public static string RawIPCData => IsChine ? "原始 IPC 数据" : "Raw IPC Data";
        
        public static string CommandExamples => IsChine ? "命令示例" : "Command Examples";
        public static string GlamourerFormat => IsChine ? "Glamourer 格式提示" : "Glamourer Format Hint";
        public static string PenumbraFormat => IsChine ? "Penumbra 格式提示" : "Penumbra Format Hint";
        
        public static string GlamourerHint => IsChine
            ? "每条规则可添加多个行动；每个行动可独立选择类型（Glamourer / Penumbra / Honorific / Moodles / SoundMixer）。"
            : "Each rule can include multiple actions; each action independently chooses Glamourer, Penumbra, Honorific, Moodles or SoundMixer.";
        public static string ActionTypeHint => IsChine
            ? "Glamourer 应用外观设计；Penumbra 修改模组选项；Honorific 设置称号；Moodles 应用状态或预设；SoundMixer 切换临时预设或组音量。"
            : "Glamourer applies designs; Penumbra modifies mod options; Honorific sets titles; Moodles applies statuses or presets; SoundMixer sets temporary preset or group volume.";
        public static string GlamourerFeetApplyMark => IsChine ? "[含脚部装备]" : "[Feet equipment]";
        public static string GlamourerFeetApplyTooltip => IsChine
            ? "带 [含脚部装备] 标记的设计，应用后可能改变鞋跟高度并触发其他规则"
            : "Designs marked [Feet equipment] may change heel height and trigger other rules";
        public static string GlamourerFeetWarningBanner => IsChine
            ? "部分规则带 [含脚部装备] 标记：应用后可能改变鞋跟高度，并导致匹配到其他规则、规则来回切换或外观异常。"
            : "Some rules are marked [Feet equipment]: applying them may change heel height, match another rule, flap between rules, or look wrong.";
        public static string HideFeetWarningBanner => IsChine ? "隐藏此提示" : "Hide this warning";
        public static string PenumbraHint => IsChine
            ? "每条规则可添加多个行动；每项行动单独设置 Collection、模组、Option 组与选项名，匹配时全部执行。"
            : "Each rule can include multiple actions; configure Collection, mod, option group, and option per action; all run when matched.";

        public static string SelectPenumbraCollection => IsChine ? "选择 Collection…" : "Select collection…";
        public static string SelectPenumbraMod => IsChine ? "选择模组…" : "Select mod…";
        public static string SelectPenumbraOptionGroup => IsChine ? "选择 Option 组…" : "Select option group…";
        public static string SelectPenumbraOptionName => IsChine ? "选择选项名…" : "Select option name…";
        public static string PenumbraGroupTypeLabel(PenumbraGroupType groupType) => groupType switch
        {
            PenumbraGroupType.Multi => IsChine ? "多选开关" : "Multi toggle",
            PenumbraGroupType.Imc => "IMC",
            PenumbraGroupType.Combining => IsChine ? "组合开关" : "Combining",
            _ => IsChine ? "单选/下拉" : "Single select",
        };
        public static string PenumbraOptionGroupTypeHint(PenumbraGroupType groupType) => groupType switch
        {
            PenumbraGroupType.Multi => IsChine
                ? "多选开关组：应用规则时仅启用所选子选项，并关闭同组其他选项"
                : "Multi-toggle group: applies only the selected option and turns off others in the group",
            PenumbraGroupType.Imc => IsChine
                ? "IMC 组：按多选开关方式应用所选子选项"
                : "IMC group: applies the selected option using multi-toggle semantics",
            PenumbraGroupType.Combining => IsChine
                ? "组合开关组：按多选开关方式应用所选子选项"
                : "Combining group: applies the selected option using multi-toggle semantics",
            _ => IsChine
                ? "单选/下拉组：应用规则时切换到所选子选项"
                : "Single-select/dropdown group: switches to the selected option",
        };
        public static string PenumbraCollectionListEmpty => IsChine ? "（无可用 Collection）" : "(No collections available)";
        public static string PenumbraModListEmpty => IsChine ? "（无可用模组）" : "(No mods available)";
        public static string PenumbraModSearchHint => IsChine ? "搜索模组名或目录…" : "Search name or directory…";
        public static string PenumbraModSearchNoResults => IsChine ? "（无匹配模组）" : "(No matching mods)";
        public static string PenumbraOptionGroupListEmpty => IsChine ? "（该模组无 Option 组）" : "(No option groups for this mod)";
        public static string PenumbraOptionNameListEmpty => IsChine ? "（该 Option 组无子选项）" : "(No options in this group)";
        public static string PenumbraOptionEnabled => IsChine ? "开启" : "On";
        public static string PenumbraOptionDisabled => IsChine ? "关闭" : "Off";
        public static string PenumbraOptionEnabledHint => IsChine
            ? "多选开关组：On 表示匹配规则时启用该子选项，Off 表示关闭该子选项"
            : "Multi-toggle group: On enables this sub-option when the rule matches; Off turns it off";
        public static string PenumbraMultiToggleListLabel => IsChine ? "子选项状态" : "Sub-option state";
        public static string PenumbraMultiToggleListHint => IsChine
            ? "匹配规则时按下方 On/Off 设置该 Option 组：On 的子选项开启，Off 的子选项关闭"
            : "When the rule matches, sub-options set to On are enabled and Off are disabled in this option group";
        public static string PenumbraListUnavailable => IsChine
            ? "Penumbra IPC 不可用，无法加载下拉列表"
            : "Penumbra IPC unavailable; cannot load dropdown lists";
        public static string SelectPenumbraModFirst => IsChine ? "请先选择模组" : "Select a mod first";
        public static string SelectPenumbraOptionGroupFirst => IsChine ? "请先选择模组与 Option 组" : "Select mod and option group first";
        public static string PenumbraValueNotInList(string value) => IsChine
            ? $"{value}（未在列表中）"
            : $"{value} (not in list)";
        public static string PenumbraModLabel(string displayName, string directory) => IsChine
            ? $"{displayName} [{directory}]"
            : $"{displayName} [{directory}]";
        public static string ClearGlamourerTempSettings => IsChine
            ? "取消 Glamourer 对该模组的临时接管"
            : "Clear Glamourer temporary takeover for mod";
        public static string ClearGlamourerTempSettingsTooltip => IsChine
            ? "清除当前所选模组上 Glamourer 写入的 Penumbra 临时设置（自动化与手动两种 Key）"
            : "Removes Glamourer-written temporary Penumbra settings on the selected mod (automation and manual keys)";
        public static string ClearGlamourerTempSettingsTooltipActive => IsChine
            ? "当前模组正被 Glamourer 临时接管，点击可清除"
            : "This mod is under Glamourer temporary takeover; click to clear";
        public static string PenumbraModGlamourerTakeover => IsChine
            ? "Glamourer 已接管"
            : "Glamourer takeover active";
        public static string PenumbraModGlamourerTakeoverWarning => IsChine
            ? "Glamourer 已临时接管此模组；未授权覆盖时规则不会 apply"
            : "Glamourer temporarily controls this mod; rule apply is skipped unless overwrite is allowed";
        public static string PenumbraOverwriteGlamourerAction => IsChine
            ? "覆盖 Glamourer 临时设置"
            : "Overwrite Glamourer temporary settings";
        public static string PenumbraOverwriteGlamourerActionTooltip => IsChine
            ? "勾选后，此行动 apply 时会先清除 Glamourer 在该模组上的临时设置"
            : "When checked, this action clears Glamourer temporary settings on this mod before apply";
        public static string PenumbraGlamourerAutoOverwriteHint => IsChine
            ? "已在设置中开启全局自动覆盖，apply 时将自动清除 Glamourer 临时设置"
            : "Global auto-overwrite is enabled in Settings; Glamourer temporary settings are cleared on apply";

        public static string OptionalAddonsHint => IsChine
            ? "每个行动可独立设置 Honorific 称号或 Moodles 状态/预设；不同行动的附加效果会全部执行。"
            : "Each action may independently set Honorific title or Moodles status/preset; all addon effects from matched actions are applied.";
        
        public static string OptionalAddons => IsChine ? "可选附加效果" : "Optional Addons";

        public static string MoodlesRemoteApplyHint => IsChine
            ? "Moodles 远程应用需在 Moodles 设置中开启「Allow Remote Apply」（允许远程应用）。"
            : "Moodles remote apply requires Allow Remote Apply in Moodles settings.";

        public static string EnableHonorific => IsChine ? "Honorific 称号（可选）" : "Honorific title (optional)";
        public static string EnableHonorificDisabled => IsChine ? "Honorific 称号（未启用）" : "Honorific title (disabled)";
        public static string EnableMoodles => IsChine ? "Moodles 状态/预设（可选）" : "Moodles status/preset (optional)";
        public static string EnableMoodlesDisabled => IsChine ? "Moodles 状态/预设（未启用）" : "Moodles status/preset (disabled)";
        public static string OptionalAddonDisabledTooltip => IsChine
            ? "对应插件未安装、未加载或 IPC 不可用"
            : "Plugin is not installed, not loaded, or IPC is unavailable";
        public static string SelectHonorificTitle => IsChine ? "选择称号…" : "Select title…";
        public static string HonorificListEmpty => IsChine ? "（无可用称号）" : "(No titles available)";
        public static string HonorificNotInList => IsChine ? "（已选称号不在列表中）" : "(Selected title not in list)";
        public static string HonorificManualFallback => IsChine
            ? "Honorific 不可用，粘贴称号 JSON"
            : "Honorific unavailable — paste title JSON";
        public static string SelectMoodle => IsChine ? "选择 Moodles…" : "Select Moodle…";
        public static string MoodleListEmpty => IsChine ? "（无可用状态/预设）" : "(No statuses/presets available)";
        public static string MoodleNotInList => IsChine ? "（已选项不在列表中）" : "(Selection not in list)";
        public static string MoodleManualFallback => IsChine
            ? "Moodles 不可用，输入 GUID"
            : "Moodles unavailable — enter GUID";
        public static string MoodleStatusLabel(string title) => IsChine ? $"状态: {title}" : $"Status: {title}";
        public static string MoodlePresetLabel(string title) => IsChine ? $"预设: {title}" : $"Preset: {title}";

        public static string MoodlesStatus => IsChine ? "Moodles 状态" : "Moodles Status";
        public static string HonorificStatus => IsChine ? "Honorific 状态" : "Honorific Status";
        public static string CustomizePlusStatus => IsChine ? "Customize+ 状态" : "Customize+ Status";
        public static string CustomizePlusProfile => IsChine ? "配置" : "Profile";
        public static string SelectCustomizePlusProfile => IsChine ? "选择 Customize+ 配置…" : "Select Customize+ profile…";
        public static string CustomizePlusProfileListEmpty => IsChine ? "（无可用配置）" : "(No profiles available)";
        public static string CustomizePlusListUnavailable => IsChine
            ? "Customize+ IPC 不可用，无法加载配置列表"
            : "Customize+ IPC unavailable; cannot load profile list";
        public static string CustomizePlusValueNotInList(string value) => IsChine
            ? $"{value}（未在列表中）"
            : $"{value} (not in list)";
        
        public static string LastAppliedPrimary => IsChine ? "最后应用（主输出）" : "Last applied (primary)";
        public static string LastAppliedHonorific => IsChine ? "最后应用（Honorific）" : "Last applied (Honorific)";
        public static string LastAppliedMoodle => IsChine ? "最后应用（Moodles）" : "Last applied (Moodles)";
        public static string HonorificApplied => IsChine ? "已应用" : "Applied";
        
        // 状态消息
        public static string NoRuleMatched => IsChine ? "无匹配规则" : "No rule matched";
        public static string RuleMatched(int index) => IsChine 
            ? $"匹配规则 #{index + 1}" 
            : $"Matched Rule #{index + 1}";
        
        // 调试信息
        public static string GlamourerEquipmentStatus => IsChine ? "Glamourer 装备槽状态" : "Glamourer Equipment Status";
        public static string EquipmentStatusUnavailable => IsChine ? "（Glamourer 不可用）" : "(Glamourer unavailable)";
        public static string EquipmentStatusNoCharacter => IsChine ? "（角色未登录）" : "(Character not logged in)";
        public static string EquipmentStatusEquipped => IsChine ? "✓ 已装备" : "✓ Equipped";
        public static string EquipmentStatusNotEquipped => IsChine ? "✗ 未装备" : "✗ Not equipped";
        public static string EquipmentStatusUnknown => IsChine ? "？ 未知" : "? Unknown";
        
        // Debug 页面 - Glamourer 装备状态
        public static string DebugForceRefreshGlamourer => IsChine ? "🔄 强制刷新 Glamourer 状态" : "🔄 Force Refresh Glamourer State";
        public static string DebugLastUpdate => IsChine ? "最后更新" : "Last updated";
        public static string DebugLastUpdateNever => IsChine ? "从未" : "Never";
        public static string DebugLastUpdateJustNow => IsChine ? "刚才" : "Just now";
        public static string DebugGlamourerStateNull => IsChine 
            ? "⚠ Glamourer 状态为 null - 点击上方按钮刷新" 
            : "⚠ Glamourer state is null - click button above to refresh";
        public static string DebugShowingInventoryEquipment => IsChine 
            ? "✓ 显示背包实际装备（Glamourer数据仅供参考）" 
            : "✓ Showing actual inventory equipment (Glamourer data for reference only)";
        public static string DebugColorLegend => IsChine 
            ? "○ 蓝色 = 正常装备 | ⚠ 橙色 = 特殊物品 | X 红色 = 未装备" 
            : "○ Blue = Normal | ⚠ Orange = Special item | X Red = Not equipped";
        public static string DebugGlamourerDataNote => IsChine 
            ? "灰色/橙色括号 = Glamourer状态（如果与背包不同）" 
            : "Gray/Orange parentheses = Glamourer state (if different from inventory)";
        public static string DebugLocalPlayerUnavailable => IsChine ? "⚠ 本地玩家不可用" : "⚠ Local player unavailable";
        public static string DebugCharacterDataUnavailable => IsChine ? "⚠ 角色数据不可用" : "⚠ Character data unavailable";
        public static string DebugInventoryManagerUnavailable => IsChine ? "⚠ InventoryManager 不可用" : "⚠ InventoryManager unavailable";
        public static string DebugEquipmentContainerUnavailable => IsChine ? "⚠ 装备背包不可用" : "⚠ Equipment container unavailable";
        public static string DebugInventoryItemId => IsChine ? "背包ItemId" : "Inventory ItemId";
        public static string DebugSpecialItem => IsChine ? "[特殊物品]" : "[Special Item]";
        public static string DebugNotEquipped => IsChine ? "[未装备]" : "[Not Equipped]";
        public static string DebugGlamourerItemId => IsChine ? "Glam: ItemId" : "Glam: ItemId";
        public static string DebugSlotError => IsChine ? "错误" : "Error";
        public static string DebugSlotNoData => IsChine ? "无数据" : "No data";

        public static string DebugDrawObjectEquipmentStatus => IsChine
            ? "DrawObject 渲染装备（ModelId → 物品名）"
            : "DrawObject Rendered Equipment (ModelId → Item Name)";

        public static string DebugDrawObjectNote => IsChine
            ? "读取 DrawObject → Human 10 槽（无腰带）。物品名按 ModelId + 位置ID（EquipSlotCategory）严格匹配，不会跨槽显示。"
            : "DrawObject → Human 10 slots (no waist). Item names require ModelId + EquipSlotCategory row ID; no cross-slot fallback.";

        public static string DebugRebuildModelLookup => IsChine ? "重建 ModelId 对照表" : "Rebuild ModelId Lookup";

        public static string DebugDrawObjectModelId => IsChine ? "ModelId" : "ModelId";

        public static string DebugDrawObjectModelPath => IsChine ? "模型路径" : "Model Path";

        public static string DebugDrawObjectVanillaPath => IsChine ? "原版 (Vanilla)" : "Vanilla";

        public static string DebugDrawObjectPenumbraModClick => IsChine
            ? "点击在 Penumbra 中打开此 Mod"
            : "Click to open this mod in Penumbra";

        public static string DebugDrawObjectItemName => IsChine ? "物品名" : "Item Name";

        public static string DebugDrawObjectNotDrawn => IsChine
            ? "角色当前未绘制（DrawObject 为空）"
            : "Character is not being drawn (DrawObject is null)";

        public static string DebugDrawObjectNotCharacterBase => IsChine
            ? "DrawObject 不是 CharacterBase"
            : "DrawObject is not CharacterBase";

        public static string DebugDrawObjectNotHuman => IsChine
            ? "DrawObject 不是 Human 模型"
            : "DrawObject is not a Human model";

        public static string DebugDrawObjectSmallclothes => IsChine
            ? "Smallclothes（无装备 / 裸模）"
            : "Smallclothes (no equipment)";

        public static string DebugDrawObjectEmperorsNew => IsChine
            ? "皇帝的新衣（隐形）"
            : "Emperor's New Gear (invisible)";

        public static string DebugDrawObjectVariant => IsChine ? "变体" : "Variant";

        public static string DebugDrawObjectAlternateNameCount(int count) => IsChine
            ? $"(另有 {count} 个名称)"
            : $"(+{count} names)";

        public static string DebugDrawObjectUnknownItem => IsChine ? "未收录" : "Unknown";

        public static string DebugDrawObjectNoSlotMatch => IsChine ? "无匹配" : "No match";

        public static string DebugDrawObjectEquipSlotCategoryId => IsChine ? "位置ID" : "SlotCat";

        public static string DebugDrawObjectHumanIndex => IsChine ? "Human#" : "Human#";

        public static string DebugDrawObjectLookupCount(int count) => IsChine
            ? $"对照表条目：{count} 个 ModelId"
            : $"Lookup entries: {count} model IDs";

        public static string DebugDrawObjectTableSlot => IsChine ? "槽位" : "Slot";
        
        // 基准行动系统
        public static string BaselineActions => IsChine ? "基准行动" : "Baseline Actions";
        public static string BaselineActionsDesc => IsChine 
            ? "在应用规则行动前管理各参数的默认状态（Penumbra / Glamourer / Moodles / Honorific）；当前规则已操作的参数会跳过" 
            : "Manage default parameter states before rule actions (Penumbra / Glamourer / Moodles / Honorific); skips parameters the matched rule handles";
        public static string BaselineActionsEnable => IsChine ? "启用基准行动系统" : "Enable Baseline Actions";
        public static string BaselineApplyWaiting => IsChine
            ? "等待基准行动就绪（先于规则行动 apply）…"
            : "Waiting for baseline actions (apply before rule actions)…";
        public static string ConfigSanitizedMisplacedActions(int count) => IsChine
            ? $"配置校验：已将 {count} 个错放在其它类型行动上的字段拆分为独立行动并已保存"
            : $"Config validation: split {count} misplaced field(s) into separate actions and saved";
        public static string BaselineActionsEnableTooltip => IsChine 
            ? "启用后，在应用规则行动前先应用基准状态；当前匹配规则已操作的参数会跳过基准，仅对其余参数生效" 
            : "When enabled, baseline states are applied before rule actions; parameters already handled by the matched rule are skipped";
        public static string BaselineNoParameters => IsChine 
            ? "（未检测到参数，创建规则后会自动扫描）" 
            : "(No parameters detected, will auto-scan after creating rules)";
        public static string BaselinePenumbraDisabledInTemporaryMode => IsChine
            ? "临时写入模式下不显示、不应用 Penumbra 基准设置（请在设置页关闭临时模式以使用 Penumbra 基准）"
            : "Penumbra baseline settings are hidden and not applied in temporary apply mode (disable temporary mode in Settings to use Penumbra baseline)";
        public static string BaselineParameter => IsChine ? "参数" : "Parameter";
        public static string BaselineMode => IsChine ? "模式" : "Mode";
        public static string BaselineModeAuto => IsChine ? "自动" : "Auto";
        public static string BaselineModeManual => IsChine ? "手动" : "Manual";
        public static string BaselineModeIgnore => IsChine ? "忽略" : "Ignore";
        public static string BaselineManualEnabled => IsChine ? "启用" : "Enabled";
        public static string BaselineManualDisabled => IsChine ? "禁用" : "Disabled";
        public static string BaselineManualModEnabled => IsChine ? "Mod 启用" : "Mod enabled";
        public static string BaselineManualOptions => IsChine ? "选项设置" : "Option settings";
        public static string BaselineSyncFromRules => IsChine ? "从规则同步" : "Sync from rules";
        public static string BaselineSyncFromRulesTooltip => IsChine 
            ? "从当前规则集中扫描此 Mod 的选项设置" 
            : "Scan option settings for this mod from current rules";
        public static string BaselineModeAutoTooltip => IsChine 
            ? "自动：使用推荐默认（Penumbra 禁用 Mod；Glamourer 还原；Honorific 清除称号）" 
            : "Auto: Recommended defaults (Penumbra disable mod; Glamourer revert; Honorific clear title)";
        public static string BaselineModeManualTooltip => IsChine 
            ? "手动：展开详细设置，自定义启用/禁用及参数" 
            : "Manual: Expand detailed settings for enable/disable and parameters";
        public static string BaselineModeIgnoreTooltip => IsChine 
            ? "忽略：不管理此参数，保持当前状态" 
            : "Ignore: Don't manage this parameter, keep current state";
        public static string BaselineManualStateTooltip => IsChine 
            ? "手动模式：在应用规则行动前应用的启用/禁用状态" 
            : "Manual mode: Enabled/disabled state applied before rule actions";
        public static string BaselineNewParameter => IsChine ? "[新]" : "[NEW]";
        public static string BaselineDismissAll => IsChine ? "忽略所有新参数" : "Dismiss All New";
        public static string BaselineDismissAllTooltip => IsChine 
            ? "一键消除所有黄色的新参数提醒" 
            : "Clear all yellow new parameter highlights";
        public static string BaselineRefresh => IsChine ? "刷新扫描" : "Refresh Scan";
        public static string BaselineRefreshTooltip => IsChine 
            ? "扫描规则集中的行动，自动添加尚未配置的基准参数。手动添加的基准项不会被删除；仅从规则移除的自动项会隐藏。"
            : "Scan rule actions and add missing baseline parameters. Manually added entries are never removed; auto entries from removed rules are only hidden.";
        public static string BaselineManualAdd => IsChine ? "手动添加" : "Add Manually";
        public static string BaselineManualAddTooltip => IsChine
            ? "手动指定一个基准参数（无需在规则行动中引用）。"
            : "Add a baseline parameter manually (no rule action reference required).";
        public static string BaselineManualAddDescription => IsChine
            ? "选择参数类型并指定目标，将作为手动基准项添加（默认手动模式、禁用状态）。"
            : "Choose a parameter type and target to add as a manual baseline entry (defaults to Manual mode, disabled).";
        public static string BaselineManualAddTypeLabel => IsChine ? "参数类型" : "Parameter type";
        public static string BaselineManualAddIncomplete => IsChine
            ? "请先完整选择要添加的基准参数。"
            : "Select a complete baseline parameter first.";
        public static string BaselineManualAddDuplicate => IsChine
            ? "该基准参数已存在。"
            : "This baseline parameter already exists.";
        public static string BaselineManualBadge => IsChine ? "[手动]" : "[Manual]";
        public static string BaselineManualDelete => IsChine ? "删除" : "Delete";
        public static string BaselineManualDeleteTooltip => IsChine
            ? "删除此手动添加的基准项。"
            : "Remove this manually added baseline entry.";
        public static string BaselineManualDeleteConfirm => IsChine
            ? "确认删除此手动添加的基准项？"
            : "Delete this manually added baseline entry?";
        public static string BaselinePenumbraMod => IsChine ? "Penumbra Mod" : "Penumbra Mod";
        public static string BaselineGlamourerDesign => IsChine ? "Glamourer 设计" : "Glamourer Design";
        public static string BaselineMoodle => IsChine ? "Moodles 状态" : "Moodles Status";
        public static string BaselineHonorific => IsChine ? "Honorific 称号" : "Honorific Title";

        // 运行时门控 / 状态（原 Plugin 内联字符串）
        public static string NotLoggedIn => IsChine ? "未登录" : "Not logged in";
        public static string WaitingForLocalPlayer => IsChine ? "等待本地角色对象" : "Waiting for local player object";
        public static string BetweenAreasLoading => IsChine ? "过图/加载中" : "Between areas / loading";
        public static string AppearanceTransformActive(short transformationId) => IsChine
            ? $"变身中（TransformationId={transformationId}），暂停外观 apply"
            : $"Transformed (TransformationId={transformationId}); appearance apply paused";
        public static string AppearanceApplySkippedUnchanged => IsChine
            ? "DrawObject 渲染外观未变化，跳过 apply"
            : "DrawObject rendered appearance unchanged; apply skipped";
        public static string AppearanceApplySkippedTargetMet => IsChine
            ? "DrawObject 已符合规则目标，跳过 apply"
            : "DrawObject already matches rule targets; apply skipped";
        public static string AppearanceApplyPausedSimpleHeelsTryOn => IsChine
            ? "SimpleHeels 试穿中，暂停外观 apply"
            : "SimpleHeels try-on active; appearance apply paused";
        public static string AppearanceApplyPausedActivePreview => IsChine
            ? "脚槽外观变化中且未达规则目标，暂停外观 apply（试穿/预览）"
            : "Feet appearance changing and targets not met; appearance apply paused (preview)";
        public static string DebugAppearanceTransformId => IsChine ? "TransformationId" : "TransformationId";
        public static string DebugAppearanceFingerprint => IsChine ? "外观指纹" : "Appearance fingerprint";
        public static string DebugAppearanceTransformInactive => IsChine ? "未变身" : "Not transformed";
        public static string WaitingForMainHandEquipmentData => IsChine ? "等待主手装备数据" : "Waiting for main-hand equipment data";
        public static string WaitingForMainHandEquipment => IsChine ? "等待主手装备加载" : "Waiting for main-hand equipment";
        public static string MainHandAnchorStabilizing(double seconds) => IsChine
            ? $"主手装备稳定中 {seconds:F1}s"
            : $"Main-hand anchor stabilizing {seconds:F1}s";
        public static string StartupDelayRemaining(double seconds) => IsChine
            ? $"启动延迟 {seconds:F1}s"
            : $"Startup delay {seconds:F1}s";
        public static string LoginWarmupRemaining(double seconds) => IsChine
            ? $"登录预热 {seconds:F1}s"
            : $"Login warmup {seconds:F1}s";
        public static string LoginPrepRemaining(double seconds) => IsChine
            ? $"登录准备 {seconds:F1}s"
            : $"Login prep {seconds:F1}s";
        public static string ApplyCooldownRemaining(double seconds) => IsChine
            ? $"应用冷却 {seconds:F1}s"
            : $"Apply cooldown {seconds:F1}s";
        public static string PluginLoadedIpcPending => IsChine ? "△ 已加载 / IPC 等待中" : "△ Loaded / IPC pending";
        public static string LoadedIpcNotReady => IsChine ? "已加载，IPC 未就绪" : "Loaded, IPC not ready";

        // SimpleHeels / IPC 调试
        public static string SimpleHeelsDisabledIpc => IsChine ? "未使用 SimpleHeels" : "SimpleHeels disabled";
        public static string SimpleHeelsUnavailableIpc => IsChine ? "SimpleHeels 不可用" : "SimpleHeels unavailable";
        public static string DefaultOffsetNotFound => IsChine ? "无法找到 DefaultOffset 字段" : "DefaultOffset field not found";
        public static string ParseError(string message) => IsChine ? $"解析错误: {message}" : $"Parse error: {message}";
        public static string SimpleHeelsConnectionFailedWarning => IsChine
            ? "⚠ SimpleHeels 连接失败！"
            : "⚠ SimpleHeels connection failed!";

        // 基准参数预览
        public static string BaselinePreviewCollection(string collection) => IsChine
            ? $"集合: {collection}"
            : $"Collection: {collection}";
        public static string BaselinePreviewModName(string modName) => IsChine
            ? $"Mod名称: {modName}"
            : $"Mod Name: {modName}";
        public static string BaselinePreviewDesign(string design) => IsChine
            ? $"设计名称/GUID: {design}"
            : $"Design Name/GUID: {design}";
        public static string BaselinePreviewName(string name) => IsChine ? $"名称: {name}" : $"Name: {name}";
        public static string BaselinePreviewTitle => IsChine ? "称号" : "Title";
        public static string OptionOnShort => IsChine ? "启用" : "On";

        // 条件 / 行动编辑
        public static string DragToReorderConditions => IsChine ? "拖动以调整顺序" : "Drag to reorder";
        public static string DragToReorderActions => IsChine ? "拖动以调整行动顺序" : "Drag to reorder actions";
        public static string DeleteThisCondition => IsChine ? "删除此条件" : "Delete this condition";
        public static string ConfirmDeleteCondition => IsChine ? "确认删除此条件？" : "Delete this condition?";
        public static string AddNewConditionTooltip => IsChine ? "添加新条件" : "Add new condition";
        public static string ToNextGroupLabel => IsChine ? "连接到下一组" : "to next group";
        public static string DeleteThisAction => IsChine ? "删除此行动" : "Delete this action";

        // Honorific / Moodles 行动编辑
        public static string HonorificUnavailable => IsChine ? "Honorific 不可用" : "Honorific unavailable";
        public static string PlayerNotLoggedIn => IsChine ? "玩家未登录" : "Player not logged in";
        public static string HonorificTitleLabel => IsChine ? "Honorific 称号:" : "Honorific Title:";
        public static string NoTitlesAvailable => IsChine ? "无可用称号" : "No titles available";
        public static string MoodlesUnavailable => IsChine ? "Moodles 不可用" : "Moodles unavailable";
        public static string MoodlesStatusPresetLabel => IsChine ? "Moodles 预设/状态:" : "Moodles Preset/Status:";
        public static string NoMoodlesAvailable => IsChine ? "无可用 Moodles" : "No moodles available";
        public static string MoodlesStatusSection => IsChine ? "--- 状态 ---" : "--- Status ---";
        public static string MoodlesPresetsSection => IsChine ? "--- 预设 ---" : "--- Presets ---";
        public static string MoodleTypePreset => IsChine ? "预设" : "Preset";
        public static string MoodleTypeStatus => IsChine ? "状态" : "Status";
        public static string CustomizePlusSupportRemoved => IsChine ? "Customize+ 支持已移除" : "Customize+ support removed";
        public static string GlamourerDesignFeetConflictWarning => IsChine
            ? "⚠ 此设计包含脚部装备，可能会与 SimpleHeels 冲突"
            : "⚠ This design contains feet equipment, may conflict with SimpleHeels";

        // 调试页
        public static string Checking => IsChine ? "检测中…" : "Checking…";
        public static string IpcTestHeader => IsChine ? "IPC 测试" : "IPC Test";
        public static string ConfigFileLocation => IsChine ? "配置文件位置" : "Config File Location";
        public static string MigrateConfigManually => IsChine ? "手动迁移配置" : "Migrate Config Manually";
        public static string NoConfigFileWarning => IsChine ? "警告：未找到任何配置文件" : "Warning: No config file found";
        public static string LastExecutedActions => IsChine ? "最后执行的行动" : "Last Executed Actions";
        public static string DiagnosticInfo => IsChine ? "诊断信息" : "Diagnostic Info";
        public static string BaselineNoOptionsSyncFromRules => IsChine
            ? "（无选项，请从规则同步）"
            : "(No options — sync from rules)";
        public static string ConfirmDeleteRuleMessage => IsChine
            ? "确定要删除此规则吗？"
            : "Are you sure you want to delete this rule?";
        public static string ConfirmDeleteActionMessage => IsChine
            ? "确定要删除此行动吗？"
            : "Are you sure you want to delete this action?";
        public static string MovingCondition(int index) => IsChine
            ? $"移动条件 {index + 1}"
            : $"Moving condition {index + 1}";
        public static string MovingAction(int index) => IsChine
            ? $"移动行动 {index + 1}"
            : $"Moving action {index + 1}";
        public static string CustomizePlusRemovedTooltip => IsChine
            ? "Customize+ 不提供所需的 IPC 方法，暂时无法支持。\n如需使用 Customize+ 配置，请直接在 Customize+ 插件中管理。"
            : "Customize+ does not provide required IPC methods.\nPlease manage profiles directly in Customize+ plugin.";
    }
}
