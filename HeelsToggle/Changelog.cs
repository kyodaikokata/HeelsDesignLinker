namespace HeelsDesignLinker
{
    public readonly record struct ChangelogEntry(string Version, string Date, string[] ItemsZh, string[] ItemsEn);

    public static class Changelog
    {
        public const string CurrentVersion = "1.2.0.9";

        public static readonly ChangelogEntry[] Entries =
        [
            new(
                "1.2.0.9",
                "2026-06-07",
                [
                    "恢复设计意图：登录保护期内完全不进行规则匹配与 apply，保护结束后才走 RuleMatchStableSeconds",
                    "移除首次 apply 即结束登录保护、以及登录期内提前规则评估（IsRuleEvaluationAllowed）",
                    "装备条件：Glamourer 与背包不一致视为未同步；修复稳定计时 restore 逻辑跳过匹配延迟",
                    "首次进入游戏若 Login 事件未触发，补全 ResetApplyState 初始化",
                ],
                [
                    "Restore intent: no rule matching/apply during login protection; RuleMatchStableSeconds after it ends",
                    "Remove ending login protection on first apply and in-login early rule evaluation",
                    "Equipment Glamourer vs inventory mismatch unsynced; fix stability timer restore skipping delay",
                    "Reset apply state when login timestamp is first set if Login event did not fire",
                ]),
            new(
                "1.2.0.8",
                "2026-06-07",
                [
                    "修复与 Glamourer 联动导致登录裸体：登录预热完成前不再评估规则/轮询 GetState",
                    "装备条件在 Glamourer 状态未就绪时视为不满足，避免误匹配「全裸」规则",
                    "等待 Glamourer 本地投影信号后再评估装备条件；基准 Glamourer revert 登录后 90s 内禁止",
                ],
                [
                    "Fix naked login with Glamourer: defer rule evaluation and GetState polling until session warmup",
                    "Equipment conditions fail closed while Glamourer state is not ready",
                    "Wait for local Glamourer projection signal before equipment checks; block baseline revert for 90s",
                ]),
            new(
                "1.2.0.7",
                "2026-06-07",
                [
                    "登录外观就绪改为主手背包槽锚点，不受身体/腿部皇帝套或 Penumbra 投影干扰",
                    "移除 Glamourer StateFinalized 自动 apply 门控（该事件常不触发导致长期卡在「等待 Glamourer 完成本地投影」）",
                    "StateFinalized 不再重置外观稳定计时",
                ],
                [
                    "Login appearance readiness now uses equipped main-hand inventory anchor",
                    "Removed Glamourer StateFinalized auto-apply gate that often never fired",
                    "StateFinalized no longer resets appearance stabilization timer",
                ]),
            new(
                "1.2.0.6",
                "2026-06-07",
                [
                    "修复 Penumbra/Glamourer 投影或皇帝的新衣场景下长期卡在「等待无装备外观同步」",
                    "外观就绪改为以身体/腿部 DrawData 稳定为准，不再要求背包装备与可见模型一致",
                    "登录保护期内门控通过后允许规则 Glamourer apply（基准行动仍禁止）",
                ],
                [
                    "Fix indefinite 'waiting for unequipped appearance sync' with Penumbra/Glamourer projection or emperor's new gear",
                    "Appearance readiness now uses stable body/legs DrawData instead of inventory vs render mismatch",
                    "Allow rule Glamourer apply during login protection once the gate passes (baseline still blocked)",
                ]),
            new(
                "1.2.0.5",
                "2026-06-07",
                [
                    "修复登录观察周期后首次 apply 误执行基准 /glamour revert 与 Penumbra 关 Mod 导致「一进游戏就全裸」",
                    "登录保护期仅限时生效：结束后恢复正常 apply 速度，不再长期卡在门控",
                    "登录保护期间禁止基准行动；等待装备 DrawData 首次呈现 + Glamourer StateFinalized",
                    "登录保护结束后再延迟 5s 才允许基准行动，避免紧接 revert 剥掉刚加载的投影",
                ],
                [
                    "Fix baseline revert / Penumbra mod-disable firing right after login observation cycle (naked on enter)",
                    "Scope login protection to a limited post-login window; normal gameplay apply speed restored afterward",
                    "Block baseline during login protection; wait for first equipped DrawData + Glamourer StateFinalized",
                    "Delay baseline actions 5s after login protection ends before allowing revert",
                ]),
            new(
                "1.2.0.4",
                "2026-06-07",
                [
                    "进一步加强登录后外观就绪检测，减少进游戏「全裸」投影",
                    "区分「登录加载中」与「稳定无装备」：真正没穿装备时不会永久卡住自动 apply",
                    "登录预热 + 首次 apply 周期跳过规则 Glamourer apply 与 Penumbra 关 Mod",
                    "发版脚本同步 legacy pluginmaster.json 版本，避免 manifest 与 zip 不一致",
                ],
                [
                    "Stronger post-login appearance readiness checks to reduce naked projection on enter",
                    "Distinguish loading vs stable unequipped so truly gearless characters are not blocked forever",
                    "Login warmup plus first apply cycle skips rule Glamourer apply and Penumbra mod-disable",
                    "Publish script syncs legacy pluginmaster.json version to match the zip",
                ]),
            new(
                "1.2.0.3",
                "2026-06-06",
                [
                    "基准行动 UI：保存主区块与各类型分组（Penumbra / Glamourer / Moodles / Honorific）的展开/折叠状态",
                    "复制规则集时一并复制基准行动 UI 展开状态",
                ],
                [
                    "Baseline Actions UI: persist expand/collapse for the main section and each type group",
                    "Copying a RuleSet also copies baseline UI expand state",
                ]),
            new(
                "1.2.0.2",
                "2026-06-06",
                [
                    "修复首次登录后过早自动 apply 导致角色外观呈「全裸」投影的问题",
                    "登录后等待装备 DrawData 稳定再应用；首次 apply 跳过基准 Glamourer revert 与 Penumbra 关 Mod",
                    "发行路径统一为 plugins/HeelsDesignLinker；国服/国际服分别使用 pluginmaster.cn.json / pluginmaster.global.json",
                    "新增 CN / Global 双构建与 sync-to-release 发布脚本",
                ],
                [
                    "Fixed naked equipment projection when auto-apply ran too early after first login",
                    "Wait for stable equipment DrawData before apply; skip baseline Glamourer revert and Penumbra mod-disable on first apply",
                    "Catalog zip paths unified under plugins/HeelsDesignLinker; separate pluginmaster.cn.json and pluginmaster.global.json",
                    "Added CN / Global dual-build and sync-to-release publish scripts",
                ]),
            new(
                "1.2.0.1",
                "2026-06-06",
                [
                    "移除不必要的 xllog 调试输出，正常运行时日志更安静",
                    "保留 IPC 失败、配置迁移错误等有助于排错的 Warning / Error 日志",
                ],
                [
                    "Removed unnecessary xllog debug output for quieter normal operation",
                    "Kept Warning / Error logs for IPC failures, migration errors, and other troubleshooting",
                ]),
            new(
                "1.2.0",
                "2026-06-06",
                [
                    "规则集（RuleSet）系统：支持多套独立规则配置并可自由切换活动规则集",
                    "SimpleHeels 配置下沉到规则集级别：每个规则集可独立选择是否使用 SimpleHeels 及高度模式",
                    "基准行动系统：支持 Penumbra / Glamourer / Moodles / Honorific，三档模式（自动 / 手动 / 忽略）",
                    "基准行动 UI：按类型折叠分组、新参数黄色高亮、一键忽略提醒；Penumbra 手动选项限高滚动",
                    "删除规则或行动后，基准配置保留在数据中但从 UI 隐藏且不执行；重新添加相同参数时恢复设置",
                    "修复规则行动 UI 读写废弃列表导致与当前规则集不同步的问题（如 Penumbra Enable Mod 不生效）",
                    "不可达规则检测修复：「否则」后的规则、含装备条件的前置规则场景均可正确报警",
                    "不可达 / 无效「否则」警告改为黄色；脚部 Glamourer 装备警告仍为红色最高优先级",
                    "插件独立性增强：Glamourer 不可用时，Penumbra / Moodles / Honorific 仍可正常工作",
                    "规则验证优化：未启用的规则不参与 else 后续规则的可达性检查，减少误报",
                    "UI 改进：规则折叠时显示条件类型与行动数量，启用和折叠按钮合并到同一行",
                    "移除 Customize+ 相关描述（已不再支持）",
                ],
                [
                    "RuleSet system: multiple independent rule configurations with switchable active RuleSet",
                    "SimpleHeels settings per RuleSet: each RuleSet can independently enable SimpleHeels and choose height mode",
                    "Baseline Actions: Penumbra / Glamourer / Moodles / Honorific with Auto / Manual / Ignore modes",
                    "Baseline UI: grouped by type, yellow highlights for new parameters, dismiss-all; scrollable Penumbra manual options",
                    "Deleted rules/actions keep baseline config on disk but hide from UI and skip execution; settings restore when re-added",
                    "Fixed rule action UI writing to legacy Rules list instead of active RuleSet (e.g. Penumbra Enable Mod not applying)",
                    "Unreachable-rule detection fix: rules after else and mixed equipment/height chains now warn correctly",
                    "Unreachable / invalid-else warnings are yellow; feet Glamourer equipment warnings stay red (highest priority)",
                    "Enhanced independence: Penumbra / Moodles / Honorific work when Glamourer is unavailable",
                    "Validation: disabled rules no longer participate in else-reachability checks, reducing false warnings",
                    "UI: collapsed rules show condition types and action count; enable and collapse buttons on one line",
                    "Removed Customize+ descriptions (no longer supported)",
                ]),
            new(
                "1.1.0",
                "2026-06-05",
                [
                    "高度规则改为 if / else-if / else 链式匹配，支持比较符、拖拽排序与不可达规则警告",
                    "每条规则支持多个行动；Penumbra 的 Collection / 模组 / Option 组移至各行动内配置",
                    "Glamourer 设计下拉与脚部装备提示；Penumbra IPC 下拉、模组搜索、Glamourer 接管检测与清除",
                    "可选 Honorific 与 Moodles 联动；移除 Glamourer.Api / Penumbra.Api，改用 Dalamud 原生 IPC",
                    "默认小数精度 4 位",
                ],
                [
                    "Height rules use if / else-if / else chains with comparators, drag reorder, and unreachable-rule warnings",
                    "Multiple actions per rule; Penumbra Collection / mod / option group configured per action",
                    "Glamourer design dropdown and feet-equipment notice; Penumbra IPC dropdowns, mod search, Glamourer takeover detect/clear",
                    "Optional Honorific and Moodles; removed Glamourer.Api / Penumbra.Api in favor of native Dalamud IPC",
                    "Default decimal precision is 4",
                ]),
            new(
                "1.0.4",
                "2026-06-05",
                [
                    "Penumbra 模式改为调用 Penumbra IPC 切换 Mod 选项，不再执行 /penumbra mod setting，避免聊天窗口刷屏",
                    "新增可配置「应用冷却」，默认 1 秒，减轻高度抖动时的频繁切换",
                ],
                [
                    "Penumbra mode uses Penumbra IPC for mod settings instead of slash commands, preventing chat log spam",
                    "Configurable apply cooldown (default 1s) to reduce rapid switches when height fluctuates",
                ]),
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
