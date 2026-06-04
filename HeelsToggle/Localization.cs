using System.Globalization;

namespace HeelsToggle
{
    public static class Localization
    {
        private static string CurrentLanguage => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        
        public static bool IsChine => CurrentLanguage == "zh";
        
        // 插件元数据
        public static string PluginName => IsChine ? "Heels Design Linker" : "Heels Design Linker";
        public static string PluginPunchline => IsChine 
            ? "根据 SimpleHeels 高度自动切换 Glamourer/Penumbra 设置" 
            : "Auto-switch Glamourer designs or Penumbra mod settings based on SimpleHeels height";
        
        // 命令帮助
        public static string CommandHelp => IsChine 
            ? "打开高跟鞋设计联动器配置" 
            : "Open Heels Design Linker configuration";
        
        // 窗口标题
        public static string WindowTitle => $"{PluginName} v{Changelog.CurrentVersion}";
        
        // 标签页
        public static string TabSettings => IsChine ? "设置" : "Settings";
        public static string TabDebug => IsChine ? "调试" : "Debug";
        public static string TabChangelog => IsChine ? "更新履历" : "Changelog";
        public static string ChangelogVersion => IsChine ? "版本" : "Version";
        public static string ChangelogDate => IsChine ? "日期" : "Date";
        
        // 设置标签页
        public static string CurrentHeight => IsChine ? "当前高度" : "Current Height";
        public static string Mode => IsChine ? "模式" : "Mode";
        public static string GlamourerMode => IsChine ? "Glamourer 模式" : "Glamourer Mode";
        public static string PenumbraMode => IsChine ? "Penumbra 模式" : "Penumbra Mode";
        
        public static string PenumbraSettings => IsChine ? "Penumbra 全局设置" : "Penumbra Global Settings";
        public static string Collection => IsChine ? "Collection（集合）" : "Collection";
        public static string ModName => IsChine ? "Mod Name（模组名称）" : "Mod Name";
        public static string Option => IsChine ? "Option（选项）" : "Option";
        
        public static string DecimalPrecision => IsChine ? "小数精度" : "Decimal Precision";
        public static string Places => IsChine ? "位" : "places";
        
        public static string HeightRules => IsChine ? "高度规则" : "Height Rules";
        public static string MinHeight => IsChine ? "最小高度" : "Min Height";
        public static string MaxHeight => IsChine ? "最大高度" : "Max Height";
        public static string DesignName => IsChine ? "设计名称" : "Design Name";
        public static string OptionName => IsChine ? "选项名称" : "Option Name";
        public static string Active => IsChine ? "启用" : "Active";
        public static string Delete => IsChine ? "删除" : "Delete";
        public static string AddNewRule => IsChine ? "添加新规则" : "Add New Rule";
        public static string Save => IsChine ? "保存" : "Save";
        
        public static string CurrentMatchedRule => IsChine ? "当前匹配规则" : "Current Matched Rule";
        
        // 调试标签页
        public static string PluginStatus => IsChine ? "插件状态" : "Plugin Status";
        public static string SimpleHeelsStatus => IsChine ? "SimpleHeels 状态" : "SimpleHeels Status";
        public static string GlamourerStatus => IsChine ? "Glamourer 状态" : "Glamourer Status";
        public static string PenumbraStatus => IsChine ? "Penumbra 状态" : "Penumbra Status";
        public static string Available => IsChine ? "可用" : "Available";
        public static string NotAvailable => IsChine ? "不可用" : "Not Available";
        
        public static string CurrentMode => IsChine ? "当前模式" : "Current Mode";
        public static string LastApplied => IsChine ? "最后应用" : "Last Applied";
        
        public static string ErrorInfo => IsChine ? "错误信息" : "Error Info";
        public static string TestIPC => IsChine ? "测试 SimpleHeels IPC" : "Test SimpleHeels IPC";
        public static string RawIPCData => IsChine ? "原始 IPC 数据" : "Raw IPC Data";
        
        public static string CommandExamples => IsChine ? "命令示例" : "Command Examples";
        public static string GlamourerFormat => IsChine ? "Glamourer 格式提示" : "Glamourer Format Hint";
        public static string PenumbraFormat => IsChine ? "Penumbra 格式提示" : "Penumbra Format Hint";
        
        public static string GlamourerHint => IsChine 
            ? "设计名称不需要加引号，直接输入设计名即可" 
            : "Design name without quotes, just enter the design name";
        public static string PenumbraHint => IsChine 
            ? "确保 Collection、Mod Name 和 Option 名称与游戏内一致" 
            : "Ensure Collection, Mod Name and Option names match in-game";
        
        // 状态消息
        public static string NoRuleMatched => IsChine ? "无匹配规则" : "No rule matched";
        public static string RuleMatched(int index) => IsChine 
            ? $"匹配规则 #{index + 1}" 
            : $"Matched Rule #{index + 1}";
    }
}
