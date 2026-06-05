using System.Globalization;

namespace HeelsToggle
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
        public static string SimpleHeelsHeightDefaultTooltip => IsChine
            ? "使用 SimpleHeels IPC 的 DefaultOffset 匹配规则"
            : "Match rules using SimpleHeels IPC DefaultOffset";
        public static string SimpleHeelsHeightActualTooltip => IsChine
            ? "使用受 TempOffset 影响的实际高度；无临时偏移时与默认高度相同"
            : "Match rules using actual height affected by TempOffset; equals default when no temp offset";
        public static string SimpleHeelsTempActiveSuffix => IsChine ? "，含临时偏移" : ", temp active";
        public static string Mode => IsChine ? "模式" : "Mode";
        public static string GlamourerMode => IsChine ? "Glamourer 模式" : "Glamourer Mode";
        public static string PenumbraMode => IsChine ? "Penumbra 模式" : "Penumbra Mode";
        public static string ModeSeparateRulesHint => IsChine
            ? "Glamourer 与 Penumbra 各自维护独立规则（数量、顺序、高度条件、行动均可不同）。同索引规则的 Honorific / Moodles 会互相同步。"
            : "Glamourer and Penumbra each have independent rules (count, order, conditions, and actions may differ). Honorific / Moodles sync by matching rule index.";
        
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
            ? "同一规则需持续匹配该时长后才会执行行动，可减轻切图/加载时高度抖动误触发；0 = 不等待（默认 0.1 秒）"
            : "Actions run only after the same rule stays matched for this long; reduces false triggers during zone loads. 0 = no wait (default 0.1s)";
        public static string RuleMatchStableWait(double remainingSeconds) => IsChine
            ? $"规则匹配稳定等待 {remainingSeconds:F2}s"
            : $"Rule match stable wait {remainingSeconds:F2}s";
        public static string RestoreDefaults => IsChine ? "恢复默认配置" : "Restore Defaults";
        public static string RestoreDefaultsTooltip => IsChine
            ? "将所有规则与设置恢复为出厂默认值"
            : "Reset all rules and settings to factory defaults";
        public static string RestoreDefaultsConfirmMessage => IsChine
            ? "将清除全部规则与设置（含 Glamourer / Penumbra 两套规则），并恢复为默认值。此操作不可撤销，请再次点击「确定恢复」以继续。"
            : "This clears all rules and settings (both Glamourer and Penumbra rule sets) and restores defaults. This cannot be undone. Click \"Confirm Restore\" again to proceed.";
        public static string RestoreDefaultsConfirm => IsChine ? "确定恢复" : "Confirm Restore";
        public static string Cancel => IsChine ? "取消" : "Cancel";
        
        public static string HeightRules => IsChine ? "高度规则" : "Height Rules";
        public static string HeightRulesOrderHint => IsChine
            ? "规则按从上到下的顺序匹配：命中第一条后不再检查后续规则（如果 / 否则如果 / 否则）。最后一条可设为「否则」兜底；「否则」后不能再有规则。可拖拽左侧握把调整顺序。"
            : "Rules are evaluated top to bottom (if / else if / else). The last rule may be an else fallback; rules after else are unreachable. Drag the left handle to reorder.";
        public static string HeightValue => IsChine ? "高度" : "Height";
        public static string RuleDragHandle => "::";
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
            ? "该规则永远不会被匹配：前面规则已覆盖所有可能高度，或无法执行到此"
            : "This rule can never match: earlier rules cover all heights or block execution.";
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
            ? "每条规则可添加多个行动；匹配时按顺序应用所有 Glamourer 设计。"
            : "Each rule can include multiple actions; all Glamourer designs are applied in order when matched.";
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
            ? "每条规则可额外勾选 Honorific 称号或 Moodles 状态/预设，可与 Glamourer/Penumbra 组合，也可单独使用。"
            : "Each rule may optionally apply an Honorific title or Moodles status/preset, combined with Glamourer/Penumbra or on its own.";

        public static string MoodlesRemoteApplyHint => IsChine
            ? "Moodles 远程应用需在 Moodles 设置中开启 Allow Remote Apply。"
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
        public static string LastAppliedPrimary => IsChine ? "最后应用（主输出）" : "Last applied (primary)";
        public static string LastAppliedHonorific => IsChine ? "最后应用（Honorific）" : "Last applied (Honorific)";
        public static string LastAppliedMoodle => IsChine ? "最后应用（Moodles）" : "Last applied (Moodles)";
        public static string HonorificApplied => IsChine ? "已应用" : "Applied";
        
        // 状态消息
        public static string NoRuleMatched => IsChine ? "无匹配规则" : "No rule matched";
        public static string RuleMatched(int index) => IsChine 
            ? $"匹配规则 #{index + 1}" 
            : $"Matched Rule #{index + 1}";
    }
}
