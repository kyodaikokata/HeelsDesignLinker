namespace HeelsDesignLinker
{
    public readonly record struct ChangelogEntry(string Version, string Date, string[] ItemsZh, string[] ItemsEn);

    public static class Changelog
    {
        public const string CurrentVersion = "1.4.2.19";

        public static readonly ChangelogEntry[] Entries =
        [
            new(
                "1.4.2.19",
                "2026-06-10",
                [
                    "登录/中途启用保护结束后强制完整 apply 规则一次：清除外观指纹与行动去重，避免预热期被其它插件重置的临时状态无法恢复",
                ],
                [
                    "Force one full rule apply after login/enable protection ends; clears fingerprint and dedup so temp state reset by other plugins during warmup is restored",
                ]),
            new(
                "1.4.2.18",
                "2026-06-10",
                [
                    "移除插件重载/登录后「命中相同则跳过整段 apply」的启动保护；仍恢复上次关闭时的去重状态，Moodles 由 dedup 避免重复 apply",
                ],
                [
                    "Remove startup skip when restored match equals last shutdown; dedup snapshot still restored so Moodles avoid duplicate apply",
                ]),
            new(
                "1.4.2.17",
                "2026-06-10",
                [
                    "修复试穿清除后 Moodles 不重新 apply：外观 skip 时仍执行 Moodles/Honorific/SoundMixer；DrawObject 模型变化时清除 Moodles 去重",
                ],
                [
                    "Fix Moodles not re-applying after try-on clear: run non-appearance actions on appearance skip; clear Moodles dedup on DrawObject model change",
                ]),
            new(
                "1.4.2.16",
                "2026-06-10",
                [
                    "修复跨规则试穿被覆盖：skip 先于 Penumbra 临时层清理；规则目标已在 DrawObject/Penumbra 达标时跳过整段 apply",
                    "Penumbra 在 IPC 状态已匹配时不再因 dedup 清空而重复 batch apply",
                ],
                [
                    "Fix cross-rule try-on overwrite: skip before Penumbra temp clear; skip when DrawObject/Penumbra targets met",
                    "Penumbra skips batch apply when IPC state already matches after dedup reset",
                ]),
            new(
                "1.4.2.15",
                "2026-06-10",
                [
                    "外观 apply 改以 DrawObject 为基准：指纹跳过与 Glamourer apply 前比对渲染槽位，画面已一致则不再改写",
                    "规则签名变化不再单独清空外观指纹",
                ],
                [
                    "Appearance apply uses DrawObject baseline: fingerprint skip and pre-Glamourer slot compare",
                    "Rule signature change no longer clears appearance fingerprint alone",
                ]),
            new(
                "1.4.2.14",
                "2026-06-10",
                [
                    "外观指纹移除 DrawObject 渲染槽，仅跟踪 Glamourer、背包装备与 DrawData 投影",
                ],
                [
                    "Appearance fingerprint drops DrawObject render slots; tracks Glamourer, inventory, and DrawData projection only",
                ]),
            new(
                "1.4.2.13",
                "2026-06-10",
                [
                    "调试页各栏目展开/折叠状态持久化到配置",
                ],
                [
                    "Persist debug tab section expand/collapse state in config",
                ]),
            new(
                "1.4.2.12",
                "2026-06-10",
                [
                    "新增外观指纹：装备渲染/背包/Glamourer 未变且规则命中未变时跳过 apply（含武具投影与任意槽位变化检测）",
                    "移除 DrawObject 模型类型门控，保留 TransformationId 变身期间暂停 apply",
                ],
                [
                    "Appearance fingerprint: skip apply when equip render/inventory/Glamourer unchanged and same rule match",
                    "Remove DrawObject model-type gate; keep TransformationId transform pause",
                ]),
            new(
                "1.4.2.11",
                "2026-06-10",
                [
                    "变身/非 Human DrawObject 期间暂停 Glamourer/Penumbra/基准等外观 apply，避免打断变身效果",
                    "变身期间跳过装备条件评估；变身结束后重新稳定匹配并 apply",
                    "调试页显示 TransformationId 与 DrawObject 模型类型",
                ],
                [
                    "Pause appearance apply during transforms / non-Human DrawObject",
                    "Skip equipment evaluation while transformed; re-match after transform ends",
                    "Debug panel shows TransformationId and DrawObject model type",
                ]),
            new(
                "1.4.2.10",
                "2026-06-10",
                [
                    "修复 Penumbra 行动组未显示 SFW 参与开关的问题；SFW 过滤按行动组父项生效",
                    "Glamourer/Moodles 等行动的 SFW 开关移至标题行，折叠时也可操作",
                ],
                [
                    "Fix missing SFW toggle on Penumbra action groups; SFW filter uses parent action",
                    "Move SFW toggle to action header row for all non-Penumbra action types",
                ]),
            new(
                "1.4.2.9",
                "2026-06-10",
                [
                    "基准行动支持手动添加与删除；刷新扫描不会移除手动添加的基准项",
                ],
                [
                    "Baseline actions: manual add/delete; refresh scan never removes manual entries",
                ]),
            new(
                "1.4.2.8",
                "2026-06-10",
                [
                    "行动新增「SFW 模式参与」开关（默认开启）；SFW 激活时关闭的行动不参与应用",
                ],
                [
                    "Actions: per-action SFW participation toggle (default on); skipped for apply when SFW is active",
                ]),
            new(
                "1.4.2.7",
                "2026-06-10",
                [
                    "回退 1.4.2.6 基准 Moodles 独占逻辑（会导致规则 Moodles 无法生效）",
                    "修复每次更新插件时从废弃 Rules 列表合并行动、凭空多出 Moodles 行动（如穿鞋规则露出全身）的问题",
                    "校验配置时跳过已存在的同 GUID Moodles 拆分；迁移清空废弃 Rules 列表并去重",
                ],
                [
                    "Revert 1.4.2.6 baseline Moodle ownership skip (broke rule Moodle apply)",
                    "Fix phantom Moodle actions added on each plugin update from stale legacy Rules list",
                    "Sanitize skips duplicate Moodle GUID splits; migration clears legacy Rules and dedupes",
                ]),
            new(
                "1.4.2.6",
                "2026-06-10",
                [
                    "修复规则已声明的 Moodles 仍被基准层重复 apply（如穿鞋规则露出全身 preset 每次更新插件叠一层）的问题",
                    "设置中新增「基准→规则 Moodles 延迟」（默认 0.1s，可调 0.1～3s）",
                    "废弃 Rules 列表同步到规则集时跳过已存在的等价行动，并迁移去重规则内重复 Moodles 行动",
                ],
                [
                    "Fix baseline re-applying Moodles already owned by a rule action (e.g. stacked preset on each plugin update)",
                    "Settings: Baseline→Rule Moodles delay (default 0.1s, range 0.1–3s)",
                    "Legacy Rules sync skips equivalent actions; migration dedupes duplicate Moodle actions per rule",
                ]),
            new(
                "1.4.2.5",
                "2026-06-10",
                [
                    "修复命中规则集合变化（如 1+3→1+4）时，重叠规则因去重键残留而未被 re-apply、仅新增规则生效的问题；变化后整组规则行动各 apply 一次",
                ],
                [
                    "Fix overlapping rules skipped on set change (e.g. 1+3→1+4) when dedup keys lingered — all matched rules now apply once after the change",
                ]),
            new(
                "1.4.2.4",
                "2026-06-10",
                [
                    "基准 Moodles 在本周期成功 apply 后，同周期内清除规则层 Moodles 去重，使规则行动再执行一次（不跨周期反复）",
                ],
                [
                    "After baseline Moodles apply in a cycle, rule Moodle dedup is cleared so rule actions run once more that cycle only",
                ]),
            new(
                "1.4.2.3",
                "2026-06-10",
                [
                    "修复多规则同时命中且各有 Moodles 行动时，因去重键只能记住最后一个 Moodle 而导致每个冷却周期反复 re-apply 的问题",
                    "Moodles 下拉列表每次展开时自动刷新；若当前选中项已在 Moodles 插件中删除则自动清空",
                ],
                [
                    "Fix Moodles re-applying every cooldown when multiple matched rules each have a different Moodle action",
                    "Moodles dropdown refreshes on each open; stale selections removed from Moodles are cleared automatically",
                ]),
            new(
                "1.4.2.2",
                "2026-06-10",
                [
                    "插件启用时自动校验配置：将错放在非对应类型行动上的字段（如 Honorific 行动中的 GlamourerDesign）拆分为独立行动并保存",
                    "Glamourer 应用仅限 Glamourer 类型行动，避免误填字段被当作设计应用",
                    "Penumbra 行动组仅剩一条子行动时，删除该子行动会移除整个 Penumbra 行动组（修复此前确认后无法删除的问题）",
                ],
                [
                    "On plugin load, validate config and split misplaced fields (e.g. GlamourerDesign on Honorific actions) into separate actions",
                    "Glamourer apply is limited to Glamourer-type actions so stray fields are not applied as designs",
                    "Deleting the only Penumbra sub-action now removes the whole Penumbra action group (fixes confirm dialog with no effect)",
                ]),
            new(
                "1.4.2.1",
                "2026-06-10",
                [
                    "关闭插件或退出游戏时保存命中规则与 apply 去重状态；再次启用插件或登录进游戏后，若命中与上次相同则跳过基准行动与规则 re-apply",
                    "避免重启后基准「穿鞋」Moodle 等清 buff 再与规则高跟鞋互相覆盖",
                ],
                [
                    "Persist matched rules and apply dedup on plugin unload or logout; on reload or login, skip baseline and rule re-apply when the match is unchanged",
                    "Avoid baseline shoe Moodle clearing buffs and fighting rule heel Moodle after restart",
                ]),
            new(
                "1.4.2.0",
                "2026-06-10",
                [
                    "规则条件组新增「SFW 模式参与」开关：关闭后该规则在全局 SFW 模式激活时不参与匹配与应用",
                    "开关显示在首个条件组标题行；规则折叠后摘要与否则分支条件区也会显示 [SFW:参与] / [SFW:跳过] 状态",
                ],
                [
                    "Rules: \"Active in SFW mode\" toggle on the first condition-group header; when off, the rule is skipped while global SFW is active",
                    "SFW participation badge [SFW:on/off] shown in the collapsed rule summary and on else-branch condition rows",
                ]),
            new(
                "1.4.1.0",
                "2026-06-10",
                [
                    "规则分组：「同一分组 / 新分组」连接符定义 if/否则如果/否则 链；多分组可同时命中并叠加应用",
                    "分组 UI：可命名、整体折叠（显示规则条数）、整组拖拽排序，分组间双分割线；分组首条自动为「如果」",
                    "规则/行动/分组拖拽支持上下半区判断，放置到目标前或后",
                    "多规则命中时后匹配覆盖前匹配；状态栏显示最后一条匹配规则",
                    "Glamourer 行动可设装备槽优先级，按优先级解决槽位冲突；UI 三色提示（未解决红 / 已解决浅蓝 / 无冲突白）",
                    "Penumbra 多规则命中时合并同 Mod 选项；未指定的单选项不再传入空值覆盖已设选项",
                    "Penumbra Glamourer 接管警告：检测 temp key，自有来源且无冲突时不警告；冲突时提示对应行动编号；符号改为 !",
                    "修复规则与行动（含 Penumbra 行动组）折叠/展开状态重启后不能正确恢复的问题",
                ],
                [
                    "Rule groups: \"same group / new group\" connectors define if/elseif/else chains; multiple groups can match and stack",
                    "Group UI: naming, whole-group collapse with rule count, group drag-reorder, double separators; group first rule is always \"if\"",
                    "Rule/action/group drag uses upper/lower half to drop before or after the target",
                    "Later matched rules override earlier ones; status bar shows the last matched rule",
                    "Glamourer actions: per-slot priority and conflict resolution; tri-color UI (unresolved red / resolved light blue / none white)",
                    "Penumbra: merge options per mod across matched rules; omit empty single-select values so they do not clobber prior settings",
                    "Penumbra Glamourer takeover warning: temp-key aware; suppress when self-owned and non-conflicting; show conflicting action numbers; use ! glyph",
                    "Fix rule and action (including Penumbra groups) collapse/expand state not restoring correctly after restart",
                ]),
            new(
                "1.4.0.0",
                "2026-06-07",
                [
                    "新增全局 SFW 模式：独立「SFW 模式」标签页，可设置模组状态与详细选项",
                    "SFW 使用独立临时层 key -1210（在规则 -1211 之后 apply）；与 SFW 列表相同 Mod 的规则 Penumbra 行动会被拦截",
                    "关闭 SFW（/hdl nsfw 或界面切换）清除 -1210 并强制重新 apply 规则 Penumbra",
                    "主界面与 DTR「HDL SFW」一键切换；/hdl sfw 启用 SFW",
                    "SfwModeActive 写入配置持久化",
                ],
                [
                    "Global SFW mode: dedicated tab; allows setting mod activation and options",
                    "SFW uses separate temp key -1210 (applied after rules -1211); rule Penumbra on the same mod is skipped",
                    "Disabling SFW (/hdl nsfw or UI) clears -1210 and forces rule Penumbra re-apply",
                    "Main panel and DTR \"HDL SFW\" one-click toggle; /hdl sfw enables SFW",
                    "SfwModeActive persisted in config",
                ]),
            new(
                "1.3.0.4",
                "2026-06-07",
                [
                    "修复规则切换后上一规则临时启用的 Penumbra Mod 未被清除的问题",
                    "规则切换后 batch apply 不再合并旧 -1211 选项组，避免同 Mod 残留设置",
                    "修复游戏内中途启用插件后须打开配置面板 Penumbra 临时设置才生效的问题",
                ],
                [
                    "Fix Penumbra mods auto-enabled by the previous rule staying on after a rule switch",
                    "Skip merging stale -1211 option groups on rule switch batch apply",
                    "Fix Penumbra temporary apply requiring the config window once after enabling the plugin in-game",
                ]),
            new(
                "1.3.0.3",
                "2026-06-07",
                [
                    "规则内行动支持 : : 拖拽排序",
                    "添加行动改为类型菜单；新建 Penumbra 行动默认选中当前激活的 Collection",
                    "修复 DTR 状态栏在插件重载时重复注册导致报错",
                ],
                [
                    "Drag-reorder actions within a rule via : : handle",
                    "Add-action menu by type; new Penumbra actions default to the active Collection",
                    "Fix DTR status bar crash when the entry already exists after plugin reload",
                ]),
            new(
                "1.3.0.2",
                "2026-06-07",
                [
                    "修复 Penumbra 选项在规则稳定时约每 20 秒重复 apply 的问题",
                    "游戏顶部服务器信息栏（DTR）显示当前高度与匹配规则，点击可打开配置",
                    "设置页可关闭 DTR 状态栏显示",
                ],
                [
                    "Fix Penumbra options re-applying about every 20s while the matched rule is stable",
                    "Show current height and matched rule in the game server info bar (DTR); click to open settings",
                    "Settings option to disable DTR status bar",
                ]),
            new(
                "1.3.0.1",
                "2026-06-07",
                [
                    "装备物品名搜索结果显示对应游戏图标",
                    "装备条件 ModelId 预览旁显示物品图标",
                    "无匹配物品或图标无效时静默跳过，不报错",
                ],
                [
                    "Show game item icons in equipment name search results",
                    "Show item icon beside equipment condition ModelId preview",
                    "Skip icon draw silently when no item match or invalid icon id",
                ]),
            new(
                "1.3.0.0",
                "2026-06-07",
                [
                    "不限于高跟鞋：规则可同时按 SimpleHeels 高度与当前渲染装备外观（DrawObject）匹配",
                    "装备条件：必须装备/未装备、任意装备或特定 ModelId；物品名搜索与读取当前外观",
                    "物品名对照支持变体精确匹配；调试页 DrawObject 装备表与独立滚动区",
                    "规则装备判定迁离 Glamourer，改读 DrawObject Human（Glamourer 仍用于 apply/设计）",
                    "条件组内矛盾检测（且逻辑下高度/同槽位装备冲突）红色提示",
                    "Tab 上方常驻状态条：显示高度与当前匹配规则（绿色）",
                    "启动与登录保护流程简化；EquipSlotCategory 映射与物品名对照修复",
                    "UI：条件分割线、Debug 页滚动修复、Moodles 预设排序、Penumbra 默认 Collection 等 QoL",
                ],
                [
                    "Not just for heels: rules can match SimpleHeels height and/or rendered equipment (DrawObject)",
                    "Equipment conditions: must be equipped/unequipped, any gear, or specific ModelId; item name search and read current appearance",
                    "Item lookup matches equipment variants; Debug DrawObject table with dedicated scroll area",
                    "Rule equipment evaluation moved from Glamourer to DrawObject Human (Glamourer still used for apply/design)",
                    "Conflict hints for contradictory AND conditions (height ranges / same-slot equipment)",
                    "Persistent status bar above tabs: height and matched rule (green)",
                    "Simpler startup/login gates; EquipSlotCategory mapping and item name lookup fixes",
                    "UI QoL: condition separators, Debug scroll fix, Moodles preset order, Penumbra default collection, and more",
                ]),
            new(
                "1.2.1.3",
                "2026-06-06",
                [
                    "完善 UI 本地化：Plugin 内联中英文字符串统一迁入 Localization",
                    "新增开发者文档：架构说明、已知问题、按插件拆分的 IPC 参考（docs/）",
                ],
                [
                    "Complete UI localization: move inline Plugin strings into Localization",
                    "Add developer docs: architecture, known issues, per-plugin IPC reference (docs/)",
                ]),
            new(
                "1.2.1.2",
                "2026-06-07",
                [
                    "基准行动 Penumbra：按 action 具体参数判断冲突，不再按 Mod/选项组整组跳过",
                    "规则仅设选项时基准不再关 Mod；多选仅跳过规则已控制的子选项",
                    "规则 Penumbra：仅设选项时自动启用 Mod；合并同规则内 Penumbra 行动再 apply",
                    "切换/修改基准行动配置时清除 apply 去重；调试页显示 Penumbra 具体选项而非仅 Mod 名",
                    "修复「最后应用」误报：仅记录本周期实际 IPC 成功的行动；去重前校验 Penumbra 当前状态，漂移时强制重 apply",
                    "移除 NothingChanged 状态误校验导致的刷屏警告；读不到当前状态时信任 Penumbra 返回值",
                    "修复去重锁死：改进 GetCurrentModSettings 解析；未验证状态时不写入去重并限频重试",
                    "修复基准单选选项「未启用」时仍错误应用；修复旧版 Penumbra 选项名迁移到多选时默认为关",
                    "缩短登录准备等待（合并重复门控）：准备阶段仍不匹配/不 apply，进游戏后规则 apply 更快",
                    "修复 Penumbra 自动启用 Mod 反复 apply：读不到状态时不再清除去重，NothingChanged 不再刷新冷却",
                    "修复顶部偶尔闪烁：Penumbra 选项组缺失时不判漂移；成功 apply 后信任去重；漂移需连续两次确认",
                    "Penumbra 状态移至调试页「插件状态」常驻显示（正常白字/异常红字），移除窗口顶部状态条",
                    "Penumbra 状态防抖：加载/IPC 抖动宽限期 + 持续 2 秒异常才变红，避免报错一闪而过",
                    "修复快速切换 heel↔bare 反应慢：规则变化时清除去重/信任并重置冷却；信任窗口内若状态不符仍重 apply",
                    "规则行动新增 SoundMixer：支持临时预设切换与临时单组音量（IPC tag: HeelsDesignLinker）",
                    "行动类型下拉：各类型名称按对应插件独立显示白字/灰字",
                    "设置页新增 Penumbra：默认临时写入（key -1211）；关闭前弹窗警告永久改写模组设定",
                    "设置页可选全局自动覆盖 Glamourer 临时设置；临时模式下隐藏/跳过 Penumbra 基准行动",
                    "Penumbra 行动：Glamourer 冲突时黄色警告；未自动覆盖时 per-action 覆盖开关；自动覆盖时 apply 前清除 GLM",
                    "调试页常驻显示 SoundMixer IPC 连接状态、API 版本与 gate 探测结果",
                    "修复 Penumbra 临时 apply 返回值解析（Success=0 被误判失败），消除调试页 Item1:0 误报",
                    "修复 Penumbra 临时 apply IPC 签名（SetTemporaryModSettingsPlayer.V5 使用 6 参数元组 gate，消除 8!=6 调用失败）",
                    "修复规则切换后 Footsteps 短暂生效又回退：仅以匹配高度变化触发重匹配；TempOffset 存在时用 actual 高度；临时模式读 WithTemp 状态",
                    "修复 Penumbra 重复 Enable (auto) 与回退 Shoes：取消独立 auto-enable IPC，临时模式按 Mod 批量写入（enabled+选项一次完成）；去重信任不再因读不到状态而跳过重 apply",
                    "修复约每 2 秒重复 Penumbra apply：信任窗口内 WithTemp 暂不可读时不再周期性重试",
                    "规则切换立即清除临时层；关闭临时模式时清除 -1211；GLM 清除需双 key 均成功；DisableMod+选项同 Mod UI 警告",
                    "修复 drawData 抖动导致每秒重复 Penumbra Enable apply：仅规则索引变化时清去重，不再因稳定计时重置而清 dedup",
                    "修复延迟数秒后每秒 Enable Success 日志：短暂无匹配只清临时层、延迟确认无匹配后再改 lastMatched；Mod 启用/禁用信任窗口内无条件跳过重 apply",
                ],
                [
                    "Baseline Penumbra: conflict check uses action parameters, not whole mod/option group",
                    "Skip baseline mod-disable when rule only sets options; multi-toggle skips rule-owned sub-options only",
                    "Rule Penumbra: auto-enable mod when only setting options; batch Penumbra actions per rule",
                    "Clear apply dedup when baseline settings change; debug shows Penumbra option details not mod name only",
                    "Fix false \"last applied\" UI: only log actions that actually ran; verify Penumbra state before dedup skip and re-apply on drift",
                    "Stop spamming NothingChanged state-mismatch warnings; trust Penumbra when current settings cannot be read",
                    "Fix dedup lock-in: parse GetCurrentModSettings correctly; unverified NothingChanged retries with throttle",
                    "Fix baseline single-select applying when disabled; fix legacy option name migrating as off",
                    "Shorter login prep waits (merged gates): still no match/apply during prep, faster first rule apply",
                    "Fix Penumbra auto-enable loop: keep dedup when state unreadable; NothingChanged no longer resets cooldown",
                    "Fix occasional top-bar flash: missing option group is unknown not drift; trust dedup after apply; drift needs 2 confirmations",
                    "Penumbra status always shown in Debug Plugin Status (white ok / red error); removed top window banner",
                    "Penumbra status debounce: load/IPC grace period + 2s sustained error before red, stops error flash",
                    "Fix slow heel↔bare switching: clear dedup/trust and cooldown on rule change; trust window still re-applies on mismatch",
                    "Rule actions: SoundMixer temporary preset switch and temporary single-group volume (IPC tag: HeelsDesignLinker)",
                    "Action type dropdown: each type name colored independently by its plugin availability",
                    "Settings: Penumbra temporary apply by default (key -1211); confirm dialog before permanent apply mode",
                    "Settings: optional global auto-overwrite of Glamourer temp settings; hide/skip Penumbra baseline in temp mode",
                    "Penumbra actions: yellow warning on Glamourer conflict; per-action overwrite toggle; auto-clear GLM when global overwrite on",
                    "Debug tab: always show SoundMixer IPC link status, API version, and gate probe results",
                    "Fix Penumbra temporary apply return parsing (Success=0 no longer treated as failure); stops Item1:0 false errors",
                    "Fix Penumbra temporary apply IPC signature (SetTemporaryModSettingsPlayer.V5 6-arg tuple gate; stops 8!=6 call failures)",
                    "Fix Footsteps reverting after rule switch: match on height delta only; use actual height when TempOffset present; WithTemp reads in temp mode",
                    "Fix repeated Enable (auto) and Shoes revert: remove separate auto-enable IPC; temp mode batches enabled+options per mod; dedup trust no longer skips re-apply on unreadable state",
                    "Fix ~2s Penumbra apply spam: trust window treats unreadable WithTemp as OK; no periodic retry",
                    "Clear temp on rule index change; clear -1211 when disabling temp mode; GLM clear requires both keys; UI warn DisableMod+options",
                    "Fix ~1s Penumbra Enable spam from drawData flicker: clear dedup only on rule index change, not stability timer reset",
                ]),
            new(
                "1.2.1.1",
                "2026-06-07",
                [
                    "基准行动 UI 改用 Dalamud HelpMarker 显示说明",
                    "基准行动：匹配规则已使用的参数跳过基准，避免妨碍规则 apply",
                    "更新基准行动相关中英文说明文案",
                ],
                [
                    "Baseline UI: use Dalamud HelpMarker for inline help tooltips",
                    "Baseline: skip parameters handled by the matched rule to avoid blocking rule apply",
                    "Update baseline-related localization strings (ZH/EN)",
                ]),
            new(
                "1.2.1.0",
                "2026-06-07",
                [
                    "登录保护预热缩短至 0.5s（启动延迟、主手锚点），加快首次进入游戏后的规则 apply",
                    "移除登录期等待 Glamourer 本地投影事件的门控（装备条件 fail-closed 与背包交叉校验仍保留）",
                    "基准行动：当匹配规则已使用同一参数时跳过基准，避免 Penumbra 关 Mod / Glamourer revert 等妨碍规则 apply",
                    "修复 Release 包 manifest 缺少 IconUrl，安装后 Dalamud 插件列表不显示图标",
                    "发布流程增加 zip manifest IconUrl 校验，并同步 images/icon.png 至 Release 仓库",
                ],
                [
                    "Shorten login protection warmup to 0.5s (startup, main-hand anchor)",
                    "Remove login Glamourer local-projection event gate (equipment fail-closed + inventory cross-check remain)",
                    "Baseline: skip parameters the matched rule will apply, avoiding Penumbra mod-disable / Glamourer revert blocking rules",
                    "Fix missing IconUrl in Release manifest so installed plugin shows icon in Dalamud",
                    "Publish pipeline validates IconUrl in zip and syncs images/icon.png to Release repo",
                ]),
            new(
                "1.2.0.9",
                "2026-06-07",
                [
                    "恢复设计意图：登录保护期内完全不进行规则匹配与 apply，保护结束后才走 RuleMatchStableSeconds",
                    "移除首次 apply 即结束登录保护、以及登录期内提前规则评估（IsRuleEvaluationAllowed）",
                    "装备条件：Glamourer 与背包不一致视为未同步；修复稳定计时 restore 逻辑跳过匹配延迟",
                    "首次进入游戏若 Login 事件未触发，补全 ResetApplyState 初始化",
                    "缩短登录保护等待（预热/主手/Glamourer 稳定均为 0.5s），匹配后仍使用 RuleMatchStableSeconds",
                    "修复 Release 包 manifest 缺少 IconUrl，安装后 Dalamud 插件列表不显示图标",
                ],
                [
                    "Restore intent: no rule matching/apply during login protection; RuleMatchStableSeconds after it ends",
                    "Remove ending login protection on first apply and in-login early rule evaluation",
                    "Equipment Glamourer vs inventory mismatch unsynced; fix stability timer restore skipping delay",
                    "Reset apply state when login timestamp is first set if Login event did not fire",
                    "Shorter login protection waits (0.5s warmup/anchor/glam settle); RuleMatchStableSeconds unchanged",
                    "Fix missing IconUrl in Release manifest so installed plugin shows icon in Dalamud",
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
