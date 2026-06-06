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
            ? "根据 SimpleHeels 高度自动切换 Glamourer/Penumbra，并可选用 Moodles / Honorific" 
            : "Auto-switch Glamourer/Penumbra by SimpleHeels height, with optional Moodles / Honorific";
        
        // 命令帮助
        public static string CommandHelp => IsChine 
            ? "打开高跟鞋设计联动器配置" 
            : "Open Heels Design Linker configuration";
        
        // 窗口标题
        public static string WindowTitle => $"{PluginName} v{Changelog.CurrentVersion}";
        
        // 标签页
        public static string TabRules => IsChine ? "规则" : "Rules";
        public static string TabSettings => IsChine ? "设置" : "Settings";
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
            _ => "Unknown"
        };
        public static string RulesActionGuideHeader => IsChine ? "行动类型说明" : "Action types";
        public static string PenumbraPriorityWarning => IsChine
            ? "· 本插件不会验证你的 Penumbra Mod 优先级，请在 Penumbra 中手动设置优先级。"
            : "· This plugin does not verify Penumbra mod priority — set priorities manually in Penumbra.";
        public static string PerActionTypeHint => IsChine
            ? "每个行动可独立选择类型（Glamourer / Penumbra / Honorific / Moodles），不再有全局模式。"
            : "Each action independently chooses its type (Glamourer / Penumbra / Honorific / Moodles). There is no global mode.";
        public static string ModeSeparateRulesHint => IsChine
            ? "每个行动可独立选择类型；同一规则内可混合使用不同类型的行动。"
            : "Each action may use a different type; a single rule can mix Glamourer, Penumbra, Honorific and Moodles actions.";

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
        
        public static string HeightRules => IsChine ? "高度规则" : "Height Rules";
        public static string HeightRulesOrderHint => IsChine
            ? "规则按从上到下的顺序匹配：命中第一条后不再检查后续规则（如果 / 否则如果 / 否则）。最后一条可设为「否则」兜底；「否则」后不能再有规则。可拖拽左侧握把调整顺序。"
            : "Rules are evaluated top to bottom (if / else if / else). The last rule may be an else fallback; rules after else are unreachable. Drag the left handle to reorder.";
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
        public static string RulePenumbraOptionConflictHint => IsChine
            ? "与同规则内其他 Penumbra 行动冲突：同一 Collection、同一 Mod 的同一选项组设置了不同效果"
            : "Conflicts with another Penumbra action in this rule: the same collection, mod, and option group have different settings";
        public static string RulePenumbraMultiToggleConflictHint => IsChine
            ? "与同规则内其他 Penumbra 行动冲突：同一选项组内的子选项设置了相反的开关状态"
            : "Conflicts with another Penumbra action in this rule: opposite toggle states for the same sub-option";
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
            ? "每条规则可添加多个行动；每个行动可独立选择类型（Glamourer / Penumbra / Honorific / Moodles）。"
            : "Each rule can include multiple actions; each action independently chooses Glamourer, Penumbra, Honorific or Moodles.";
        public static string ActionTypeHint => IsChine
            ? "Glamourer 应用外观设计；Penumbra 修改模组选项；Honorific 设置称号；Moodles 应用状态。"
            : "Glamourer applies designs; Penumbra modifies mod options; Honorific sets titles; Moodles applies statuses.";
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
        public static string PenumbraOptionEnabledHint => IsChine
            ? "多选开关组：勾选表示匹配规则时启用该选项，取消勾选表示关闭该选项（保留同组其他开关状态）"
            : "Multi-toggle group: checked enables this option when the rule matches; unchecked turns it off (other toggles in the group are kept)";
        public static string PenumbraMultiToggleListLabel => IsChine ? "子选项开关" : "Sub-option toggles";
        public static string PenumbraMultiToggleListHint => IsChine
            ? "匹配规则时按下方勾选状态设置该 Option 组：勾选的开启，未勾选的关闭"
            : "When the rule matches, enabled options below are turned on and unchecked ones are turned off";
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
        
        // 基准行动系统
        public static string BaselineActions => IsChine ? "基准行动" : "Baseline Actions";
        public static string BaselineActionsDesc => IsChine 
            ? "在应用规则行动前管理各参数的默认状态（Penumbra / Glamourer / Moodles / Honorific）；当前规则已操作的参数会跳过" 
            : "Manage default parameter states before rule actions (Penumbra / Glamourer / Moodles / Honorific); skips parameters the matched rule handles";
        public static string BaselineActionsEnable => IsChine ? "启用基准行动系统" : "Enable Baseline Actions";
        public static string BaselineActionsEnableTooltip => IsChine 
            ? "启用后，在应用规则行动前先应用基准状态；当前匹配规则已操作的参数会跳过基准，仅对其余参数生效" 
            : "When enabled, baseline states are applied before rule actions; parameters already handled by the matched rule are skipped";
        public static string BaselineNoParameters => IsChine 
            ? "（未检测到参数，创建规则后会自动扫描）" 
            : "(No parameters detected, will auto-scan after creating rules)";
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
            ? "重新扫描当前规则集中的所有参数" 
            : "Re-scan all parameters in current rule set";
        public static string BaselinePenumbraMod => IsChine ? "Penumbra Mod" : "Penumbra Mod";
        public static string BaselineGlamourerDesign => IsChine ? "Glamourer 设计" : "Glamourer Design";
        public static string BaselineMoodle => IsChine ? "Moodles 状态" : "Moodles Status";
        public static string BaselineHonorific => IsChine ? "Honorific 称号" : "Honorific Title";
    }
}
