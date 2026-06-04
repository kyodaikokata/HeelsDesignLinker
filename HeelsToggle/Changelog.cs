namespace HeelsToggle
{
    public readonly record struct ChangelogEntry(string Version, string Date, string[] ItemsZh, string[] ItemsEn);

    public static class Changelog
    {
        public const string CurrentVersion = "1.0.3";

        public static readonly ChangelogEntry[] Entries =
        [
            new(
                "1.0.3",
                "2026-06-05",
                [
                    "修复进游戏时 Glamourer 报错「&lt;me&gt; 无法解析」并误套用设计：需本地角色就绪后再等待 3 秒才自动 apply",
                ],
                [
                    "Fixed Glamourer \"<me>\" placeholder errors and wrong designs on login: auto-apply waits until local player is valid, then 3s startup delay",
                ]),
            new(
                "1.0.2",
                "2026-06-05",
                [
                    "修复新开游戏时偶发无法识别 SimpleHeels / Glamourer 的问题：启动后周期性重检依赖插件与 SimpleHeels IPC，就绪后自动恢复，无需手动重新开关插件",
                ],
                [
                    "Fixed occasional failure to detect SimpleHeels/Glamourer on fresh launch: periodic dependency and IPC readiness checks; recovers automatically without toggling the plugin",
                ]),
            new(
                "1.0.1",
                "2026-06-04",
                [
                    "快捷键改为 /heelsdesign，并新增 /hdl 别名",
                    "新增「更新履历」标签页，可在插件内查看版本历史",
                    "修复加载顺序：LoadPriority 设为 -10，启动时不再早于 Glamourer / SimpleHeels 加载",
                    "当前匹配的高度规则以绿色高亮显示，便于确认生效规则",
                    "界面支持中英文本地化（随系统语言切换）",
                    "默认高度规则调整为：-1～-0.01、-0.009～0.03、0.031～1.0",
                    "Penumbra 模式默认 Collection 为 Default，Mod / Option 默认为空",
                ],
                [
                    "Command changed to /heelsdesign; added /hdl alias",
                    "Added Changelog tab to view version history in-game",
                    "Fixed load order: LoadPriority -10 so dependencies load first",
                    "Green highlight for the currently matched height rule",
                    "UI localization (Chinese / English based on system language)",
                    "Default height rules: -1～-0.01, -0.009～0.03, 0.031～1.0",
                    "Penumbra defaults: Collection Default; Mod / Option empty",
                ]),
            new(
                "1.0.0",
                "2026-06-04",
                [
                    "首次发布",
                    "Glamourer 模式：按 SimpleHeels 高度自动应用设计",
                    "Penumbra 模式：按高度自动切换 Mod 子选项",
                    "SimpleHeels IPC 集成（DefaultOffset 解析）",
                    "可配置高度规则与小数精度",
                    "设置 / 调试双标签页界面",
                ],
                [
                    "Initial release",
                    "Glamourer mode: auto-apply designs by SimpleHeels height",
                    "Penumbra mode: auto-switch mod settings by height",
                    "SimpleHeels IPC (DefaultOffset parsing)",
                    "Configurable height rules and decimal precision",
                    "Settings and Debug tabs",
                ]),
        ];
    }
}
