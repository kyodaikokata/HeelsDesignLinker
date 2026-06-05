using System.Numerics;
using System.Text.RegularExpressions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Configuration;
using Dalamud.Bindings.ImGui;
using Dalamud.Utility;
using Newtonsoft.Json.Linq;

namespace HeelsToggle
{
    public enum PluginMode
    {
        Glamourer = 0,
        Penumbra = 1
    }

    public enum SimpleHeelsHeightMode
    {
        Default = 0,
        Actual = 1,
    }

    public enum HeightComparison
    {
        GreaterThan = 0,
        GreaterThanOrEqual = 1,
        LessThan = 2,
        LessThanOrEqual = 3,
        Equal = 4,
    }

    public enum RuleBranchKind
    {
        ElseIf = 0,
        Else = 1,
    }

    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        /// <summary>Glamourer 模式专用规则列表。</summary>
        public List<HeelsRule> GlamourerRules { get; set; } = new();

        /// <summary>Penumbra 模式专用规则列表。</summary>
        public List<HeelsRule> PenumbraRules { get; set; } = new();

        /// <summary>旧版共享规则列表，仅用于迁移。</summary>
        public List<HeelsRule> Rules { get; set; } = new();
        public int DecimalPrecision { get; set; } = 4;
        public PluginMode Mode { get; set; } = PluginMode.Glamourer; // 默认 Glamourer 模式
        
        /// <summary>两次自动应用之间的最短间隔（秒），0 表示不限制。</summary>
        public float ApplyCooldownSeconds { get; set; } = 1f;

        /// <summary>规则持续匹配多久后才开始执行行动（秒），0 表示不等待。</summary>
        public float RuleMatchStableSeconds { get; set; } = 0.1f;

        /// <summary>界面语言；System 跟随系统 UI 语言。</summary>
        public UiLanguagePreference UiLanguage { get; set; } = UiLanguagePreference.System;

        /// <summary>规则匹配使用的 SimpleHeels 高度来源。</summary>
        public SimpleHeelsHeightMode SimpleHeelsHeightMode { get; set; } = SimpleHeelsHeightMode.Default;

        /// <summary>配置窗口宽度（像素）。</summary>
        public float WindowWidth { get; set; } = 600f;

        /// <summary>配置窗口高度（像素）。</summary>
        public float WindowHeight { get; set; } = 450f;

        /// <summary>旧版 Penumbra 全局设置，仅用于迁移。</summary>
        public string PenumbraCollection { get; set; } = "Default";
        public string PenumbraModName { get; set; } = "";
        public string PenumbraOption { get; set; } = "";
    }

    public class HeelsRuleAction
    {
        public string GlamourerDesign { get; set; } = "";

        public string PenumbraCollection { get; set; } = "Default";
        public string PenumbraModName { get; set; } = "";
        public string PenumbraOption { get; set; } = "";
        public string PenumbraOptionName { get; set; } = "";
        public bool PenumbraOptionEnabled { get; set; } = true;

        /// <summary>多选开关组：子选项名 → 匹配规则时是否开启。</summary>
        public Dictionary<string, bool> PenumbraMultiToggleStates { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>规则 UI 中是否折叠该行动详情。</summary>
        public bool IsActionCollapsed { get; set; }
    }

    public class HeelsRule
    {
        public RuleBranchKind BranchKind { get; set; } = RuleBranchKind.ElseIf;
        public HeightComparison HeightComparison { get; set; } = HeightComparison.LessThanOrEqual;
        public float HeightValue { get; set; }

        /// <summary>旧版区间配置，仅用于迁移。</summary>
        public float MinHeight { get; set; } = 0.01f;
        public float MaxHeight { get; set; } = 99.0f;

        public List<HeelsRuleAction> Actions { get; set; } = new();

        /// <summary>旧版单行动配置，仅用于迁移。</summary>
        public string GlamourerDesign { get; set; } = "";
        public string PenumbraOptionName { get; set; } = "";
        public bool PenumbraOptionEnabled { get; set; } = true;

        /// <summary>可选：按规则应用 Honorific 称号。</summary>
        public bool EnableHonorific { get; set; }
        public string HonorificTitleJson { get; set; } = "";

        /// <summary>可选：按规则应用 Moodles 状态或预设。</summary>
        public bool EnableMoodles { get; set; }
        public string MoodleGuid { get; set; } = "";
        public bool MoodleIsPreset { get; set; }
        
        public bool IsActive { get; set; } = true;
    }

    public class Plugin : IDalamudPlugin
    {
        public string Name => "Heels Glamourer Linker (Universal IPC)";

        [PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
        [PluginService] public static IFramework Framework { get; private set; } = null!;
        [PluginService] public static IObjectTable ObjectTable { get; private set; } = null!;
        [PluginService] public static IClientState ClientState { get; private set; } = null!;
        [PluginService] public static IPluginLog PluginLog { get; private set; } = null!;

        private Configuration Configuration { get; set; }
        private bool drawConfigUi = false;
        private readonly HashSet<string> lastAppliedActionKeys = new(StringComparer.Ordinal);
        private string lastAppliedHonorificJson = "";
        private string lastAppliedMoodleKey = "";
        private int lastMatchedRuleIndex = -1;
        private float currentHeelsHeight = 0f;
        private float currentHeelsDefaultHeight = 0f;
        private float currentHeelsActualHeight = 0f;
        private bool currentHeelsHasTempOffset = false;
        private int currentMatchedRuleIndex = -1; // 当前匹配的规则索引，-1 表示无匹配

        private bool isGlamourerAvailable = false;
        private bool isSimpleHeelsAvailable = false;
        private bool isSimpleHeelsIpcReady = false;
        private bool isPenumbraIpcReady = false;
        private bool isMoodlesIpcReady = false;
        private bool isHonorificIpcReady = false;

        private readonly PenumbraInterop _penumbraInterop;
        private readonly GlamourerInterop _glamourerInterop;
        private readonly MoodlesInterop _moodlesInterop;
        private readonly HonorificInterop _honorificInterop;
        
        private DateTime lastDependencyCheckUtc = DateTime.MinValue;
        private static readonly TimeSpan DependencyRecheckWhenMissing = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan DependencyRecheckWhenReady = TimeSpan.FromSeconds(30);

        /// <summary>本地玩家对象稳定就绪后，再等待一段时间才自动 apply，避免 &lt;me&gt; 未解析。</summary>
        private static readonly TimeSpan AutoApplyStartupDelay = TimeSpan.FromSeconds(3);
        private DateTime? localPlayerStableSinceUtc;
        private DateTime lastApplyUtc = DateTime.MinValue;
        private string applyGateStatus = "";
        
        // 调试信息
        private string lastError = "";
        private string lastIpcData = "";
        private string penumbraModSearchFilter = "";
        private string feetWarningDismissedSignature = "";
        private string lastFeetWarningSignature = "";
        private int? _ruleDragSourceIndex;
        private int? _ruleDragTargetIndex;
        private bool restoreDefaultsPending;
        private bool wasSettingsTabActive;
        private const string KoFiUrl = "https://ko-fi.com/kokatakyodai";
        private const int ConfigSchemaVersion = 10;
        private int stableTrackingRuleIndex = -1;
        private DateTime? ruleMatchStableSinceUtc;
        private const float DefaultWindowWidth = 600f;
        private const float DefaultWindowHeight = 450f;

        public Plugin()
        {
            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            _penumbraInterop = new PenumbraInterop(PluginInterface);
            _glamourerInterop = new GlamourerInterop(PluginInterface);
            _moodlesInterop = new MoodlesInterop(PluginInterface);
            _honorificInterop = new HonorificInterop(PluginInterface);

            if (Configuration.Version < ConfigSchemaVersion)
            {
                if (Configuration.Version < 2 && Configuration.ApplyCooldownSeconds <= 0f)
                    Configuration.ApplyCooldownSeconds = 1f;
                if (Configuration.Version < 6)
                    MigrateRulesToHeightComparison();
                if (Configuration.Version < 7)
                    MigrateRulesToBranchKind();
                if (Configuration.Version < 8)
                    MigrateRulesToPerRuleActions();
                if (Configuration.Version < 9)
                    MigrateRulesToPerModeLists();
                if (Configuration.Version < 10 && Configuration.RuleMatchStableSeconds <= 0f)
                    Configuration.RuleMatchStableSeconds = 0.1f;
                Configuration.Version = ConfigSchemaVersion;
                PluginInterface.SavePluginConfig(Configuration);
            }

            Localization.SetLanguagePreference(Configuration.UiLanguage);
            
            if (Configuration.GlamourerRules.Count == 0 && Configuration.PenumbraRules.Count == 0)
            {
                Configuration.GlamourerRules = CreateDefaultRuleSet(PluginMode.Glamourer);
                Configuration.PenumbraRules = CreateDefaultRuleSet(PluginMode.Penumbra);
                SaveConfig();
            }
            
            CommandManager.AddHandler("/hdl", new CommandInfo(OnCommand) { HelpMessage = Localization.CommandHelp });
            CommandManager.AddHandler("/heelsdesign", new CommandInfo(OnCommand) { HelpMessage = Localization.CommandHelp });
            
            RefreshDependencies(force: true);
            Framework.Update += OnFrameworkUpdate;
            ClientState.Logout += OnLogout;
            
            // 🎯 核心修正 2：注册标准界面渲染回调
            PluginInterface.UiBuilder.Draw += DrawConfigurationUI;
            
            // 🎯 核心修正 3：必须注册这个，告诉卫月当玩家在插件列表点击“设置”时应该呼出哪个 UI
            PluginInterface.UiBuilder.OpenConfigUi += OpenConfigurationUi;
            PluginInterface.UiBuilder.OpenMainUi += OpenConfigurationUi;
        }

        private void OnCommand(string command, string args)
        {
            drawConfigUi = !drawConfigUi;
            if (drawConfigUi)
                OpenConfigurationUi();
        }

        private void OpenConfigurationUi()
        {
            drawConfigUi = true;
            RefreshDependencies(force: true);
            _glamourerInterop.RefreshDesignList(force: true);
            _penumbraInterop.RefreshData(force: true);
            _moodlesInterop.RefreshList(force: true);
            _honorificInterop.RefreshTitleList(ObjectTable.LocalPlayer, force: true);
        }

        private static bool ActionUsesGlamourer(HeelsRuleAction action) =>
            !string.IsNullOrWhiteSpace(action.GlamourerDesign);

        private bool ActionUsesPenumbra(HeelsRuleAction action)
        {
            if (string.IsNullOrWhiteSpace(action.PenumbraCollection)
                || string.IsNullOrWhiteSpace(action.PenumbraModName)
                || string.IsNullOrWhiteSpace(action.PenumbraOption))
            {
                return false;
            }

            var groupType = _penumbraInterop.GetOptionGroupType(
                action.PenumbraModName,
                action.PenumbraOption);
            if (PenumbraInterop.UsesBoolOptionValue(groupType))
                return true;

            return !string.IsNullOrWhiteSpace(action.PenumbraOptionName);
        }

        private bool RuleUsesGlamourer(HeelsRule rule) =>
            GetRuleActions(rule).Any(ActionUsesGlamourer);

        private bool RuleUsesPenumbra(HeelsRule rule) =>
            GetRuleActions(rule).Any(ActionUsesPenumbra);

        private List<HeelsRule> GetActiveRules() =>
            Configuration.Mode == PluginMode.Glamourer
                ? Configuration.GlamourerRules
                : Configuration.PenumbraRules;

        private static List<HeelsRule> GetRulesForMode(Configuration config, PluginMode mode) =>
            mode == PluginMode.Glamourer ? config.GlamourerRules : config.PenumbraRules;

        private static PluginMode GetOtherMode(PluginMode mode) =>
            mode == PluginMode.Glamourer ? PluginMode.Penumbra : PluginMode.Glamourer;

        private List<HeelsRule> GetOtherModeRules() =>
            GetRulesForMode(Configuration, GetOtherMode(Configuration.Mode));

        private static HeelsRuleAction CreateDefaultRuleActionForMode(PluginMode mode) =>
            mode == PluginMode.Glamourer
                ? new HeelsRuleAction()
                : new HeelsRuleAction { PenumbraCollection = "Default" };

        private static HeelsRule CreateEmptyRule(PluginMode mode) => new()
        {
            Actions = [CreateDefaultRuleActionForMode(mode)],
        };

        /// <summary>将同索引规则的 Honorific / Moodles 设置同步到另一模式（若存在对应规则）。</summary>
        private void SyncOptionalAddonsToOtherMode(int ruleIndex)
        {
            var activeRules = GetActiveRules();
            var otherRules = GetOtherModeRules();
            if (ruleIndex < 0 || ruleIndex >= activeRules.Count || ruleIndex >= otherRules.Count)
                return;

            var source = activeRules[ruleIndex];
            var target = otherRules[ruleIndex];
            target.EnableHonorific = source.EnableHonorific;
            target.HonorificTitleJson = source.HonorificTitleJson ?? "";
            target.EnableMoodles = source.EnableMoodles;
            target.MoodleGuid = source.MoodleGuid ?? "";
            target.MoodleIsPreset = source.MoodleIsPreset;
        }

        private void OnModeChanged(PluginMode newMode)
        {
            if (Configuration.Mode == newMode)
                return;

            Configuration.Mode = newMode;
            FixMisplacedElseBranches(Configuration.GlamourerRules);
            FixMisplacedElseBranches(Configuration.PenumbraRules);
            InvalidateApplyStateForHeightSourceChange();
            SaveConfig();
        }

        private static List<HeelsRule> CreateDefaultRuleSet(PluginMode mode) =>
        [
            new HeelsRule
            {
                HeightComparison = HeightComparison.LessThanOrEqual,
                HeightValue = -0.01f,
                Actions =
                [
                    mode == PluginMode.Glamourer
                        ? new HeelsRuleAction { GlamourerDesign = "SFX_Barefoot" }
                        : new HeelsRuleAction { PenumbraCollection = "Default", PenumbraOptionName = "Barefoot" },
                ],
                IsActive = true,
            },
            new HeelsRule
            {
                HeightComparison = HeightComparison.LessThanOrEqual,
                HeightValue = 0.03f,
                Actions =
                [
                    mode == PluginMode.Glamourer
                        ? new HeelsRuleAction { GlamourerDesign = "SFX_Shoes" }
                        : new HeelsRuleAction { PenumbraCollection = "Default", PenumbraOptionName = "Shoes" },
                ],
                IsActive = true,
            },
            new HeelsRule
            {
                BranchKind = RuleBranchKind.Else,
                Actions =
                [
                    mode == PluginMode.Glamourer
                        ? new HeelsRuleAction { GlamourerDesign = "SFX_Heels" }
                        : new HeelsRuleAction { PenumbraCollection = "Default", PenumbraOptionName = "Heels" },
                ],
                IsActive = true,
            },
        ];

        private List<HeelsRuleAction> GetRuleActions(HeelsRule rule)
        {
            if (rule.Actions is { Count: > 0 })
                return rule.Actions;

            if (string.IsNullOrWhiteSpace(rule.GlamourerDesign)
                && string.IsNullOrWhiteSpace(rule.PenumbraOptionName))
            {
                return [];
            }

            return
            [
                new HeelsRuleAction
                {
                    GlamourerDesign = rule.GlamourerDesign ?? "",
                    PenumbraOptionName = rule.PenumbraOptionName ?? "",
                    PenumbraOptionEnabled = rule.PenumbraOptionEnabled,
                },
            ];
        }

        private void EnsureRuleHasActions(int ruleIndex)
        {
            var rules = GetActiveRules();
            if (ruleIndex < 0 || ruleIndex >= rules.Count)
                return;

            var rule = rules[ruleIndex];
            if (rule.Actions is { Count: > 0 })
                return;

            rule.Actions =
            [
                new HeelsRuleAction
                {
                    GlamourerDesign = rule.GlamourerDesign ?? "",
                    PenumbraOptionName = rule.PenumbraOptionName ?? "",
                    PenumbraOptionEnabled = rule.PenumbraOptionEnabled,
                },
            ];
            SaveConfig();
        }

        private static HeelsRuleAction CreateDefaultRuleAction() =>
            CreateDefaultRuleActionForMode(PluginMode.Glamourer);

        private static string BuildGlamourerActionKey(HeelsRuleAction action) =>
            $"G:{action.GlamourerDesign.Trim()}";

        private string BuildPenumbraActionKey(HeelsRuleAction action)
        {
            var groupType = _penumbraInterop.GetOptionGroupType(
                action.PenumbraModName ?? "",
                action.PenumbraOption ?? "");
            if (PenumbraInterop.UsesBoolOptionValue(groupType))
            {
                var enabledNames = GetPenumbraMultiToggleEnabledNames(action);
                return $"P:{action.PenumbraCollection}|{action.PenumbraModName}|{action.PenumbraOption}|M:{string.Join(",", enabledNames)}";
            }

            return $"P:{action.PenumbraCollection}|{action.PenumbraModName}|{action.PenumbraOption}|{PenumbraInterop.BuildApplyStateKey(action.PenumbraOptionName, groupType, action.PenumbraOptionEnabled)}";
        }

        private static IReadOnlyList<string> GetPenumbraMultiToggleEnabledNames(HeelsRuleAction action) =>
            action.PenumbraMultiToggleStates
                .Where(pair => pair.Value)
                .Select(pair => pair.Key.Trim())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

        private bool SyncPenumbraMultiToggleStates(HeelsRuleAction action, IReadOnlyList<string> options)
        {
            action.PenumbraMultiToggleStates ??= new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            var changed = false;

            if (!string.IsNullOrWhiteSpace(action.PenumbraOptionName))
            {
                var legacyName = action.PenumbraOptionName.Trim();
                var legacyOption = options.FirstOrDefault(option =>
                    string.Equals(option, legacyName, StringComparison.OrdinalIgnoreCase));
                if (legacyOption != null)
                    action.PenumbraMultiToggleStates[legacyOption] = action.PenumbraOptionEnabled;
                action.PenumbraOptionName = "";
                changed = true;
            }

            var valid = new HashSet<string>(options, StringComparer.OrdinalIgnoreCase);
            foreach (var staleKey in action.PenumbraMultiToggleStates.Keys
                         .Where(key => !valid.Contains(key))
                         .ToList())
            {
                action.PenumbraMultiToggleStates.Remove(staleKey);
                changed = true;
            }

            foreach (var option in options)
            {
                if (action.PenumbraMultiToggleStates.ContainsKey(option))
                    continue;

                action.PenumbraMultiToggleStates[option] = false;
                changed = true;
            }

            return changed;
        }

        private static string BuildPenumbraGroupKey(HeelsRuleAction action) =>
            $"{action.PenumbraCollection}|{action.PenumbraModName}|{action.PenumbraOption}";

        private static string FormatGlamourerDesignArgument(string designName)
        {
            var trimmed = designName.Trim();
            if (trimmed.Contains(' ') || trimmed.Contains('"'))
                return $"\"{trimmed.Replace("\"", "\\\"")}\"";

            return trimmed;
        }

        private void OnMatchedRuleIndexChanged(int newRuleIndex, HeelsRule? newRule, IPlayerCharacter? localPlayer)
        {
            if (newRuleIndex == lastMatchedRuleIndex)
                return;

            var hadHonorific = !string.IsNullOrEmpty(lastAppliedHonorificJson);
            var wantsHonorific = newRule != null && RuleUsesHonorific(newRule);
            if (hadHonorific
                && !wantsHonorific
                && localPlayer != null
                && localPlayer.IsValid())
            {
                _honorificInterop.TryClearLocalTitle(localPlayer, out _);
            }

            lastAppliedActionKeys.Clear();
            lastAppliedHonorificJson = "";
            lastAppliedMoodleKey = "";
            lastMatchedRuleIndex = newRuleIndex;
        }

        private static void FixMisplacedElseBranches(List<HeelsRule> rules)
        {
            for (var i = 0; i < rules.Count - 1; i++)
            {
                if (rules[i].BranchKind == RuleBranchKind.Else)
                    rules[i].BranchKind = RuleBranchKind.ElseIf;
            }
        }

        private bool RuleUsesHonorific(HeelsRule rule) =>
            rule.EnableHonorific && !string.IsNullOrWhiteSpace(rule.HonorificTitleJson);

        private bool RuleUsesMoodles(HeelsRule rule) =>
            rule.EnableMoodles
            && Guid.TryParse(rule.MoodleGuid, out var id)
            && id != Guid.Empty;

        private bool ConfigurationUsesGlamourer() =>
            GetActiveRules().Any(r => r.IsActive && RuleUsesGlamourer(r));

        private bool ConfigurationUsesPenumbra() =>
            GetActiveRules().Any(r => r.IsActive && RuleUsesPenumbra(r));

        private bool ConfigurationUsesHonorific() =>
            GetActiveRules().Any(r => r.IsActive && RuleUsesHonorific(r));

        private bool ConfigurationUsesMoodles() =>
            GetActiveRules().Any(r => r.IsActive && RuleUsesMoodles(r));

        private bool HasConfiguredOutput() =>
            ConfigurationUsesGlamourer()
            || ConfigurationUsesPenumbra()
            || ConfigurationUsesHonorific()
            || ConfigurationUsesMoodles();

        /// <summary>
        /// 依赖插件可能晚于本插件完成加载/注册 IPC，因此在就绪前周期性重检。
        /// </summary>
        private bool IsReadyForWork()
        {
            if (!isSimpleHeelsAvailable || !isSimpleHeelsIpcReady)
                return false;

            if (!HasConfiguredOutput())
                return false;

            if (ConfigurationUsesGlamourer() && !isGlamourerAvailable)
                return false;

            if (ConfigurationUsesPenumbra() && !isPenumbraIpcReady)
                return false;

            if (ConfigurationUsesMoodles() && !isMoodlesIpcReady)
                return false;

            if (ConfigurationUsesHonorific() && !isHonorificIpcReady)
                return false;

            return true;
        }

        private void RefreshDependenciesIfNeeded()
        {
            var interval = IsReadyForWork() ? DependencyRecheckWhenReady : DependencyRecheckWhenMissing;
            var now = DateTime.UtcNow;
            if ((now - lastDependencyCheckUtc) < interval)
                return;

            RefreshDependencies(force: false);
        }

        private void RefreshDependencies(bool force)
        {
            if (!force)
            {
                var interval = IsReadyForWork() ? DependencyRecheckWhenReady : DependencyRecheckWhenMissing;
                var now = DateTime.UtcNow;
                if ((now - lastDependencyCheckUtc) < interval)
                    return;
            }

            lastDependencyCheckUtc = DateTime.UtcNow;
            var wasReady = IsReadyForWork();

            isGlamourerAvailable = PluginInterface.InstalledPlugins.Any(p =>
                p.InternalName == "Glamourer" && p.IsLoaded);
            isSimpleHeelsAvailable = PluginInterface.InstalledPlugins.Any(p =>
                p.InternalName == "SimpleHeels" && p.IsLoaded);

            isSimpleHeelsIpcReady = false;
            if (isSimpleHeelsAvailable)
            {
                try
                {
                    var provider = PluginInterface.GetIpcSubscriber<string>("SimpleHeels.GetLocalPlayer");
                    provider.InvokeFunc();
                    isSimpleHeelsIpcReady = true;
                }
                catch (Exception ex)
                {
                    PluginLog.Debug($"SimpleHeels IPC not ready yet: {ex.Message}");
                }
            }

            isPenumbraIpcReady = _penumbraInterop.IsIpcAvailable();
            isMoodlesIpcReady = _moodlesInterop.IsIpcAvailable();
            isHonorificIpcReady = _honorificInterop.IsIpcAvailable();

            var nowReady = IsReadyForWork();
            if (nowReady && !wasReady)
            {
                PluginLog.Info("Heels Design Linker: dependencies ready for configured outputs.");
            }
            else if (!nowReady && wasReady)
            {
                PluginLog.Warning("Heels Design Linker: dependency plugin(s) became unavailable.");
            }
        }

        private void OnLogout(int type, int code)
        {
            ResetApplyState();
        }

        private void ResetApplyState()
        {
            localPlayerStableSinceUtc = null;
            lastApplyUtc = DateTime.MinValue;
            lastAppliedActionKeys.Clear();
            lastAppliedHonorificJson = "";
            lastAppliedMoodleKey = "";
            lastMatchedRuleIndex = -1;
            stableTrackingRuleIndex = -1;
            ruleMatchStableSinceUtc = null;
        }

        private void UpdateRuleMatchStability(int matchedRuleIndex)
        {
            if (matchedRuleIndex == stableTrackingRuleIndex)
                return;

            stableTrackingRuleIndex = matchedRuleIndex;
            ruleMatchStableSinceUtc = matchedRuleIndex >= 0 ? DateTime.UtcNow : null;
        }

        private bool IsRuleMatchStableElapsed(out string status)
        {
            var delay = Math.Max(0f, Configuration.RuleMatchStableSeconds);
            if (stableTrackingRuleIndex < 0)
            {
                status = "";
                return false;
            }

            if (delay <= 0f)
            {
                status = "";
                return true;
            }

            if (ruleMatchStableSinceUtc == null)
            {
                status = "";
                return false;
            }

            var elapsed = (DateTime.UtcNow - ruleMatchStableSinceUtc.Value).TotalSeconds;
            if (elapsed >= delay)
            {
                status = "";
                return true;
            }

            status = Localization.RuleMatchStableWait(delay - elapsed);
            return false;
        }

        private static string BuildMoodleKey(HeelsRule rule) =>
            $"{rule.MoodleGuid}|{(rule.MoodleIsPreset ? "preset" : "status")}";

        private bool IsApplyCooldownElapsed(out string status)
        {
            var cooldown = Math.Max(0f, Configuration.ApplyCooldownSeconds);
            if (cooldown <= 0f)
            {
                status = "";
                return true;
            }

            if (lastApplyUtc == DateTime.MinValue)
            {
                status = "";
                return true;
            }

            var elapsed = (DateTime.UtcNow - lastApplyUtc).TotalSeconds;
            if (elapsed >= cooldown)
            {
                status = "";
                return true;
            }

            var remaining = cooldown - elapsed;
            status = Localization.IsChine
                ? $"应用冷却 {remaining:F1}s"
                : $"Apply cooldown {remaining:F1}s";
            return false;
        }

        private bool IsLocalPlayerReady()
        {
            if (!ClientState.IsLoggedIn)
                return false;

            var localPlayer = ObjectTable.LocalPlayer;
            return localPlayer != null && localPlayer.IsValid();
        }

        private bool CanAutoApply(out string gateStatus)
        {
            if (!ClientState.IsLoggedIn)
            {
                localPlayerStableSinceUtc = null;
                gateStatus = Localization.IsChine ? "未登录" : "Not logged in";
                return false;
            }

            if (!IsLocalPlayerReady())
            {
                localPlayerStableSinceUtc = null;
                gateStatus = Localization.IsChine ? "等待本地角色对象" : "Waiting for local player object";
                return false;
            }

            var now = DateTime.UtcNow;
            localPlayerStableSinceUtc ??= now;

            var elapsed = now - localPlayerStableSinceUtc.Value;
            if (elapsed < AutoApplyStartupDelay)
            {
                var remaining = AutoApplyStartupDelay - elapsed;
                gateStatus = Localization.IsChine
                    ? $"启动延迟 {remaining.TotalSeconds:F1}s"
                    : $"Startup delay {remaining.TotalSeconds:F1}s";
                return false;
            }

            gateStatus = Localization.IsChine ? "可自动应用" : "Ready to apply";
            return true;
        }

        private void OnFrameworkUpdate(IFramework framework)
        {
            RefreshDependenciesIfNeeded();
            if (!IsReadyForWork())
                return;

            if (!ClientState.IsLoggedIn)
            {
                applyGateStatus = Localization.IsChine ? "未登录" : "Not logged in";
                ResetApplyState();
                return;
            }
            
            try
            {
                // 📡 使用 SimpleHeels 的 IPC 获取本地玩家高度信息
                var getLocalPlayerProvider = PluginInterface.GetIpcSubscriber<string>("SimpleHeels.GetLocalPlayer");
                var localPlayerData = getLocalPlayerProvider.InvokeFunc();
                
                lastIpcData = localPlayerData ?? "NULL";
                
                // 解析返回的数据获取高度值
                if (string.IsNullOrEmpty(localPlayerData))
                {
                    lastError = "SimpleHeels 返回空数据";
                    return;
                }

                if (!TryParseHeelsHeights(localPlayerData, out var defaultHeight, out var actualHeight, out var hasTempOffset))
                    return;

                currentHeelsDefaultHeight = defaultHeight;
                currentHeelsActualHeight = actualHeight;
                currentHeelsHasTempOffset = hasTempOffset;
                currentHeelsHeight = SelectHeelsHeightForMode(defaultHeight, actualHeight);
                lastError = "";
            }
            catch (Exception ex)
            {
                lastError = $"IPC 错误: {ex.Message}";
                PluginLog.Error($"SimpleHeels IPC failed: {ex}");
                return;
            }

            HeelsRule? matchedRule = null;
            currentMatchedRuleIndex = -1;

            var activeRules = GetActiveRules();
            for (int i = 0; i < activeRules.Count; i++)
            {
                var rule = activeRules[i];
                if (TryMatchRule(rule, i, currentHeelsHeight))
                {
                    currentMatchedRuleIndex = i;
                    matchedRule = rule;
                    break;
                }
            }

            applyGateStatus = "";
            UpdateRuleMatchStability(currentMatchedRuleIndex);

            if (!CanAutoApply(out applyGateStatus))
                return;

            var localPlayer = ObjectTable.LocalPlayer;

            if (matchedRule == null)
            {
                OnMatchedRuleIndexChanged(-1, null, localPlayer);
                return;
            }

            if (!IsRuleMatchStableElapsed(out var stableStatus))
            {
                applyGateStatus = stableStatus;
                return;
            }

            if (!IsApplyCooldownElapsed(out var cooldownStatus))
            {
                applyGateStatus = cooldownStatus;
                return;
            }

            if (localPlayer == null || !localPlayer.IsValid())
                return;

            OnMatchedRuleIndexChanged(currentMatchedRuleIndex, matchedRule, localPlayer);

            var appliedAnything = false;
            var configDirty = false;

            if (Configuration.Mode == PluginMode.Glamourer)
                ApplyGlamourerActionsForRule(matchedRule, ref appliedAnything);
            else
                configDirty = ApplyPenumbraActionsForRule(matchedRule, ref appliedAnything);

            if (configDirty)
                SaveConfig();

            if (RuleUsesHonorific(matchedRule))
            {
                var titleJson = matchedRule.HonorificTitleJson;
                if (titleJson != lastAppliedHonorificJson)
                {
                    if (_honorificInterop.TrySetLocalTitle(localPlayer, titleJson, out var honorificError))
                    {
                        lastAppliedHonorificJson = titleJson;
                        appliedAnything = true;
                        lastError = "";
                        PluginLog.Info("Applied Honorific title (IPC).");
                    }
                    else
                    {
                        lastError = honorificError;
                        PluginLog.Warning($"Honorific IPC apply failed: {honorificError}");
                    }
                }
            }

            if (RuleUsesMoodles(matchedRule))
            {
                var moodleKey = BuildMoodleKey(matchedRule);
                if (moodleKey != lastAppliedMoodleKey
                    && Guid.TryParse(matchedRule.MoodleGuid, out var moodleId))
                {
                    if (_moodlesInterop.TryApply(localPlayer, moodleId, matchedRule.MoodleIsPreset, out var moodlesError))
                    {
                        lastAppliedMoodleKey = moodleKey;
                        appliedAnything = true;
                        lastError = "";
                        PluginLog.Info(matchedRule.MoodleIsPreset
                            ? $"Applied Moodles preset (IPC): {moodleId}"
                            : $"Applied Moodles status (IPC): {moodleId}");
                    }
                    else
                    {
                        lastError = moodlesError;
                        PluginLog.Warning($"Moodles IPC apply failed: {moodlesError}");
                    }
                }
            }

            if (appliedAnything)
                lastApplyUtc = DateTime.UtcNow;
        }

        private void ApplyGlamourerActionsForRule(HeelsRule rule, ref bool appliedAnything)
        {
            foreach (var action in GetRuleActions(rule))
            {
                if (!ActionUsesGlamourer(action))
                    continue;

                var applyKey = BuildGlamourerActionKey(action);
                if (lastAppliedActionKeys.Contains(applyKey))
                    continue;

                try
                {
                    var designArg = FormatGlamourerDesignArgument(action.GlamourerDesign);
                    CommandManager.ProcessCommand($"/glamour apply {designArg} | <me>");
                    lastAppliedActionKeys.Add(applyKey);
                    appliedAnything = true;
                    PluginLog.Info($"Applied Glamourer design: {action.GlamourerDesign}");
                }
                catch (Exception ex)
                {
                    PluginLog.Error($"Failed to apply Glamourer design: {ex.Message}");
                }
            }
        }

        private bool ApplyPenumbraActionsForRule(HeelsRule rule, ref bool appliedAnything)
        {
            var configDirty = false;
            var penumbraActions = GetRuleActions(rule).Where(ActionUsesPenumbra).ToList();
            if (penumbraActions.Count == 0)
                return false;

            var mergedMultiToggles = new Dictionary<string, (HeelsRuleAction Action, Dictionary<string, bool> Toggles)>(
                StringComparer.OrdinalIgnoreCase);
            var singleSelectByGroup = new Dictionary<string, HeelsRuleAction>(StringComparer.OrdinalIgnoreCase);

            foreach (var action in penumbraActions)
            {
                var groupType = _penumbraInterop.GetOptionGroupType(
                    action.PenumbraModName ?? "",
                    action.PenumbraOption ?? "");
                var groupKey = BuildPenumbraGroupKey(action);

                if (PenumbraInterop.UsesBoolOptionValue(groupType))
                {
                    var optionNames = _penumbraInterop.GetOptionNames(
                        action.PenumbraModName ?? "",
                        action.PenumbraOption ?? "");
                    if (SyncPenumbraMultiToggleStates(action, optionNames))
                        configDirty = true;

                    if (!mergedMultiToggles.TryGetValue(groupKey, out var merged))
                    {
                        merged = (action, new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase));
                        foreach (var option in optionNames)
                            merged.Toggles[option] = false;
                        mergedMultiToggles[groupKey] = merged;
                    }

                    foreach (var (optionName, enabled) in action.PenumbraMultiToggleStates)
                    {
                        if (enabled)
                            mergedMultiToggles[groupKey].Toggles[optionName] = true;
                    }
                }
                else
                {
                    singleSelectByGroup[groupKey] = action;
                }
            }

            foreach (var (_, merged) in mergedMultiToggles)
            {
                var action = merged.Action;
                var enabledNames = merged.Toggles
                    .Where(pair => pair.Value)
                    .Select(pair => pair.Key.Trim())
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var applyKey =
                    $"P:{action.PenumbraCollection}|{action.PenumbraModName}|{action.PenumbraOption}|M:{string.Join(",", enabledNames)}";
                if (lastAppliedActionKeys.Contains(applyKey))
                    continue;

                if (_penumbraInterop.TryApplyMultiToggleSettings(
                        action.PenumbraCollection,
                        action.PenumbraModName,
                        action.PenumbraOption,
                        enabledNames,
                        out var multiResult,
                        out var multiError))
                {
                    lastAppliedActionKeys.Add(applyKey);
                    appliedAnything = true;
                    lastError = "";
                    if (multiResult != PenumbraIpcEc.NothingChanged)
                        PluginLog.Info($"Applied Penumbra multi-toggle (IPC): {action.PenumbraOption}");
                }
                else
                {
                    lastError = multiError;
                    PluginLog.Warning($"Penumbra IPC apply failed: {multiError}");
                }
            }

            foreach (var action in singleSelectByGroup.Values)
            {
                var applyKey = BuildPenumbraActionKey(action);
                if (lastAppliedActionKeys.Contains(applyKey))
                    continue;

                if (_penumbraInterop.TryApplyModSetting(
                        action.PenumbraCollection,
                        action.PenumbraModName,
                        action.PenumbraOption,
                        action.PenumbraOptionName,
                        action.PenumbraOptionEnabled,
                        out var singleResult,
                        out var singleError))
                {
                    lastAppliedActionKeys.Add(applyKey);
                    appliedAnything = true;
                    lastError = "";
                    if (singleResult != PenumbraIpcEc.NothingChanged)
                        PluginLog.Info($"Applied Penumbra setting (IPC): {action.PenumbraOptionName}");
                }
                else
                {
                    lastError = singleError;
                    PluginLog.Warning($"Penumbra IPC apply failed: {singleError}");
                }
            }

            return configDirty;
        }

        private float SelectHeelsHeightForMode(float defaultHeight, float actualHeight) =>
            Configuration.SimpleHeelsHeightMode == SimpleHeelsHeightMode.Actual
                ? actualHeight
                : defaultHeight;

        private void InvalidateApplyStateForHeightSourceChange()
        {
            lastAppliedActionKeys.Clear();
            lastAppliedHonorificJson = "";
            lastAppliedMoodleKey = "";
            lastMatchedRuleIndex = -1;
            stableTrackingRuleIndex = -1;
            ruleMatchStableSinceUtc = null;
        }

        private bool TryParseHeelsHeights(string data, out float defaultHeight, out float actualHeight, out bool hasTempOffset)
        {
            defaultHeight = currentHeelsDefaultHeight;
            actualHeight = currentHeelsActualHeight;
            hasTempOffset = currentHeelsHasTempOffset;

            try
            {
                var json = JObject.Parse(data);

                if (json["DefaultOffset"] is JToken defaultToken)
                {
                    defaultHeight = defaultToken.Value<float>();
                }
                else
                {
                    var match = Regex.Match(data, @"""DefaultOffset"":\s*(-?\d+\.?\d*)");
                    if (!match.Success || !float.TryParse(match.Groups[1].Value, out defaultHeight))
                    {
                        lastError = Localization.IsChine ? "无法找到 DefaultOffset 字段" : "DefaultOffset field not found";
                        return false;
                    }
                }

                hasTempOffset = false;
                if (json["TempOffset"] is JObject tempOffset
                    && tempOffset["Y"] is JToken tempY)
                {
                    actualHeight = tempY.Value<float>();
                    hasTempOffset = true;
                }
                else
                {
                    actualHeight = defaultHeight;
                }

                PluginLog.Debug(
                    $"Parsed SimpleHeels heights: default={defaultHeight}, actual={actualHeight}, temp={hasTempOffset}");
                return true;
            }
            catch (Exception ex)
            {
                lastError = Localization.IsChine ? $"解析错误: {ex.Message}" : $"Parse error: {ex.Message}";
                PluginLog.Error($"Failed to parse height: {ex}");
                return false;
            }
        }

        // UI 渲染
        private void DrawConfigurationUI()
        {
            if (!drawConfigUi)
            {
                _ruleDragSourceIndex = null;
                _ruleDragTargetIndex = null;
                restoreDefaultsPending = false;
                wasSettingsTabActive = false;
                return;
            }

            ImGui.SetNextWindowSize(GetSavedWindowSize(), ImGuiCond.Appearing);
            if (ImGui.Begin(Localization.WindowTitle, ref drawConfigUi))
            {
                TrySaveWindowSize();
                DrawSimpleHeelsHeightModeBar();
                ImGui.Separator();

                if (ConfigurationUsesGlamourer() && !isGlamourerAvailable)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.3f, 0.3f, 1.0f));
                    ImGui.TextWrapped($"{Localization.GlamourerStatus}: {Localization.NotAvailable}");
                    ImGui.PopStyleColor();
                    ImGui.Separator();
                }

                if (ConfigurationUsesPenumbra() && !isPenumbraIpcReady)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.3f, 0.3f, 1.0f));
                    var penumbraDetail = !PenumbraInterop.IsPenumbraLoaded(PluginInterface)
                        ? Localization.NotAvailable
                        : (Localization.IsChine ? "已加载，IPC 未就绪" : "Loaded, IPC not ready");
                    ImGui.TextWrapped($"{Localization.PenumbraStatus}: {penumbraDetail}");
                    ImGui.PopStyleColor();
                    ImGui.Separator();
                }

                if (ConfigurationUsesMoodles() && !isMoodlesIpcReady)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.3f, 0.3f, 1.0f));
                    var moodlesDetail = !MoodlesInterop.IsMoodlesLoaded(PluginInterface)
                        ? Localization.NotAvailable
                        : (Localization.IsChine ? "已加载，IPC 未就绪" : "Loaded, IPC not ready");
                    ImGui.TextWrapped($"{Localization.MoodlesStatus}: {moodlesDetail}");
                    ImGui.PopStyleColor();
                    ImGui.Separator();
                }

                if (ConfigurationUsesHonorific() && !isHonorificIpcReady)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.3f, 0.3f, 1.0f));
                    var honorificDetail = !HonorificInterop.IsHonorificLoaded(PluginInterface)
                        ? Localization.NotAvailable
                        : (Localization.IsChine ? "已加载，IPC 未就绪" : "Loaded, IPC not ready");
                    ImGui.TextWrapped($"{Localization.HonorificStatus}: {honorificDetail}");
                    ImGui.PopStyleColor();
                    ImGui.Separator();
                }
                
                if (!isSimpleHeelsAvailable || !isSimpleHeelsIpcReady)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.3f, 0.3f, 1.0f));
                    var shDetail = !isSimpleHeelsAvailable
                        ? Localization.NotAvailable
                        : (Localization.IsChine ? "已加载，IPC 未就绪" : "Loaded, IPC not ready");
                    ImGui.TextWrapped($"{Localization.SimpleHeelsStatus}: {shDetail}");
                    ImGui.PopStyleColor();
                    ImGui.Separator();
                }

                var settingsTabActive = false;
                if (ImGui.BeginTabBar("MainTabs"))
                {
                    if (ImGui.BeginTabItem(Localization.TabRules))
                    {
                        DrawRulesTab();
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem(Localization.TabSettings))
                    {
                        settingsTabActive = true;
                        DrawSettingsTab();
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem(Localization.TabDebug))
                    {
                        DrawDebugTab();
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem(Localization.TabChangelog))
                    {
                        DrawChangelogTab();
                        ImGui.EndTabItem();
                    }

                    ImGui.EndTabBar();
                }

                if (wasSettingsTabActive && !settingsTabActive)
                    restoreDefaultsPending = false;
                wasSettingsTabActive = settingsTabActive;
            }
            ImGui.End();
        }
        
        private Vector2 GetSavedWindowSize() =>
            new(
                Configuration.WindowWidth > 0f ? Configuration.WindowWidth : DefaultWindowWidth,
                Configuration.WindowHeight > 0f ? Configuration.WindowHeight : DefaultWindowHeight);

        private void TrySaveWindowSize()
        {
            var size = ImGui.GetWindowSize();
            if (size.X <= 0f || size.Y <= 0f)
                return;

            if (MathF.Abs(Configuration.WindowWidth - size.X) < 1f
                && MathF.Abs(Configuration.WindowHeight - size.Y) < 1f)
            {
                return;
            }

            Configuration.WindowWidth = size.X;
            Configuration.WindowHeight = size.Y;
            SaveConfig();
        }

        private void DrawSimpleHeelsHeightModeBar()
        {
            var format = $"F{Configuration.DecimalPrecision}";

            ImGui.Text($"{Localization.SimpleHeelsHeightSource}:");
            ImGui.SameLine();

            var heightMode = (int)Configuration.SimpleHeelsHeightMode;
            if (ImGui.RadioButton(Localization.SimpleHeelsHeightDefault, heightMode == (int)SimpleHeelsHeightMode.Default))
            {
                if (Configuration.SimpleHeelsHeightMode != SimpleHeelsHeightMode.Default)
                {
                    Configuration.SimpleHeelsHeightMode = SimpleHeelsHeightMode.Default;
                    currentHeelsHeight = SelectHeelsHeightForMode(currentHeelsDefaultHeight, currentHeelsActualHeight);
                    InvalidateApplyStateForHeightSourceChange();
                    SaveConfig();
                }
            }

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Localization.SimpleHeelsHeightDefaultTooltip);

            ImGui.SameLine();
            if (ImGui.RadioButton(Localization.SimpleHeelsHeightActual, heightMode == (int)SimpleHeelsHeightMode.Actual))
            {
                if (Configuration.SimpleHeelsHeightMode != SimpleHeelsHeightMode.Actual)
                {
                    Configuration.SimpleHeelsHeightMode = SimpleHeelsHeightMode.Actual;
                    currentHeelsHeight = SelectHeelsHeightForMode(currentHeelsDefaultHeight, currentHeelsActualHeight);
                    InvalidateApplyStateForHeightSourceChange();
                    SaveConfig();
                }
            }

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Localization.SimpleHeelsHeightActualTooltip);

            ImGui.SameLine();
            ImGui.TextDisabled(
                $"({Localization.SimpleHeelsHeightDefault}: {currentHeelsDefaultHeight.ToString(format)} | {Localization.SimpleHeelsHeightActual}: {currentHeelsActualHeight.ToString(format)}{(currentHeelsHasTempOffset ? Localization.SimpleHeelsTempActiveSuffix : "")})");
        }

        private void DrawRulesTab()
        {
            ImGui.TextDisabled($"{Localization.CurrentHeight}: {currentHeelsHeight.ToString($"F{Configuration.DecimalPrecision}")}");

            if (currentMatchedRuleIndex >= 0)
            {
                ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.0f, 1.0f, 0.0f, 1.0f));
                ImGui.Text($"[{Localization.RuleMatched(currentMatchedRuleIndex)}]");
                ImGui.PopStyleColor();
            }
            else
            {
                ImGui.SameLine();
                ImGui.TextDisabled($"[{Localization.NoRuleMatched}]");
            }

            ImGui.Separator();
            ImGui.Text($"{Localization.Mode}:");
            ImGui.SameLine();

            int currentMode = (int)Configuration.Mode;
            if (ImGui.RadioButton(Localization.GlamourerMode, currentMode == 0))
                OnModeChanged(PluginMode.Glamourer);
            ImGui.SameLine();
            if (ImGui.RadioButton(Localization.PenumbraMode, currentMode == 1))
                OnModeChanged(PluginMode.Penumbra);

            ImGui.Separator();
            ImGui.TextDisabled(Localization.ModeSeparateRulesHint);
            ImGui.Spacing();

            if (Configuration.Mode == PluginMode.Glamourer)
                ImGui.TextWrapped(Localization.GlamourerHint);
            else
                ImGui.TextWrapped(Localization.PenumbraHint);

            ImGui.TextWrapped(Localization.OptionalAddonsHint);
            ImGui.TextDisabled(Localization.MoodlesRemoteApplyHint);
            
            ImGui.Separator();
            ImGui.TextWrapped(Localization.HeightRulesOrderHint);

            if (ImGui.Button(Localization.AddNewRule))
            {
                GetActiveRules().Add(CreateEmptyRule(Configuration.Mode));
                SaveConfig();
            }

            ImGui.BeginChild("RulesList", new Vector2(0, -30), true, ImGuiWindowFlags.None);
            DrawGlamourerFeetWarningBanner();

            var activeRules = GetActiveRules();
            for (int i = 0; i < activeRules.Count; i++)
            {
                if (i > 0)
                {
                    ImGui.Spacing();
                    ImGui.Separator();
                    ImGui.Spacing();
                }

                ImGui.PushID(i);
                DrawRuleRow(i);
                ImGui.PopID();
            }

            ProcessRuleDragReorder();
            ImGui.EndChild();

            if (ImGui.Button(Localization.Save)) SaveConfig();
        }

        private void DrawSettingsTab()
        {
            ImGui.SetNextItemWidth(220);
            if (ImGui.BeginCombo(Localization.LanguageLabel, Localization.LanguagePreview(Configuration.UiLanguage)))
            {
                if (ImGui.Selectable(Localization.LanguageSystem, Configuration.UiLanguage == UiLanguagePreference.System))
                {
                    Configuration.UiLanguage = UiLanguagePreference.System;
                    Localization.SetLanguagePreference(Configuration.UiLanguage);
                    SaveConfig();
                }

                if (ImGui.Selectable(Localization.LanguageChinese, Configuration.UiLanguage == UiLanguagePreference.Chinese))
                {
                    Configuration.UiLanguage = UiLanguagePreference.Chinese;
                    Localization.SetLanguagePreference(Configuration.UiLanguage);
                    SaveConfig();
                }

                if (ImGui.Selectable(Localization.LanguageEnglish, Configuration.UiLanguage == UiLanguagePreference.English))
                {
                    Configuration.UiLanguage = UiLanguagePreference.English;
                    Localization.SetLanguagePreference(Configuration.UiLanguage);
                    SaveConfig();
                }

                ImGui.EndCombo();
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.SetNextItemWidth(200);
            int precision = Configuration.DecimalPrecision;
            if (ImGui.SliderInt($"{Localization.DecimalPrecision} ({Localization.Places})", ref precision, 0, 5))
            {
                Configuration.DecimalPrecision = precision;
                SaveConfig();
            }

            ImGui.SetNextItemWidth(200);
            var cooldown = Configuration.ApplyCooldownSeconds;
            if (ImGui.SliderFloat(Localization.ApplyCooldown, ref cooldown, 0f, 10f, "%.1f s"))
            {
                Configuration.ApplyCooldownSeconds = cooldown;
                SaveConfig();
            }
            ImGui.TextDisabled(Localization.ApplyCooldownHint);

            ImGui.SetNextItemWidth(200);
            var matchStableDelay = Configuration.RuleMatchStableSeconds;
            if (ImGui.SliderFloat(Localization.RuleMatchStableDelay, ref matchStableDelay, 0f, 3f, "%.2f s"))
            {
                Configuration.RuleMatchStableSeconds = matchStableDelay;
                SaveConfig();
            }
            ImGui.TextDisabled(Localization.RuleMatchStableDelayHint);

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            DrawRestoreDefaultsSection();
        }

        private void DrawRestoreDefaultsSection()
        {
            if (!restoreDefaultsPending)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.72f, 0.22f, 0.22f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.82f, 0.28f, 0.28f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.62f, 0.18f, 0.18f, 1.0f));
                if (ImGui.Button(Localization.RestoreDefaults))
                    restoreDefaultsPending = true;
                ImGui.PopStyleColor(3);

                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(Localization.RestoreDefaultsTooltip);
                return;
            }

            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.55f, 0.25f, 1.0f));
            ImGui.TextWrapped(Localization.RestoreDefaultsConfirmMessage);
            ImGui.PopStyleColor();
            ImGui.Spacing();

            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.85f, 0.25f, 0.15f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.95f, 0.32f, 0.2f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.75f, 0.2f, 0.1f, 1.0f));
            if (ImGui.Button(Localization.RestoreDefaultsConfirm))
            {
                RestoreDefaultConfiguration();
                restoreDefaultsPending = false;
            }
            ImGui.PopStyleColor(3);

            ImGui.SameLine();
            if (ImGui.Button(Localization.Cancel))
                restoreDefaultsPending = false;
        }

        private void RestoreDefaultConfiguration()
        {
            Configuration.GlamourerRules = CreateDefaultRuleSet(PluginMode.Glamourer);
            Configuration.PenumbraRules = CreateDefaultRuleSet(PluginMode.Penumbra);
            Configuration.Rules = [];
            Configuration.Mode = PluginMode.Glamourer;
            Configuration.DecimalPrecision = 4;
            Configuration.ApplyCooldownSeconds = 1f;
            Configuration.RuleMatchStableSeconds = 0.1f;
            Configuration.UiLanguage = UiLanguagePreference.System;
            Configuration.SimpleHeelsHeightMode = SimpleHeelsHeightMode.Default;
            Configuration.WindowWidth = DefaultWindowWidth;
            Configuration.WindowHeight = DefaultWindowHeight;
            Configuration.PenumbraCollection = "Default";
            Configuration.PenumbraModName = "";
            Configuration.PenumbraOption = "";
            Configuration.Version = ConfigSchemaVersion;

            Localization.SetLanguagePreference(Configuration.UiLanguage);
            feetWarningDismissedSignature = "";
            lastFeetWarningSignature = "";
            penumbraModSearchFilter = "";
            ResetApplyState();
            SaveConfig();
            PluginLog.Info("Heels Design Linker: configuration restored to defaults.");
        }

        private void DrawRuleRow(int ruleIndex)
        {
            var currentRule = GetActiveRules()[ruleIndex];

            if (ruleIndex == currentMatchedRuleIndex)
            {
                ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.0f, 0.4f, 0.0f, 0.3f));
                ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(0.0f, 0.5f, 0.0f, 0.4f));
                ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(0.0f, 0.6f, 0.0f, 0.5f));
            }

            var deleteWidth = ImGui.CalcTextSize(Localization.Delete).X
                + ImGui.GetStyle().FramePadding.X * 2f;
            var dragWidth = ImGui.CalcTextSize(Localization.RuleDragHandle).X
                + ImGui.GetStyle().FramePadding.X * 2f;
            var switchColumnWidth = Math.Max(deleteWidth, Math.Max(dragWidth, ImGui.GetFrameHeight())) + 8f;

            if (ImGui.BeginTable(
                    $"RuleRowTable{ruleIndex}",
                    2,
                    ImGuiTableFlags.NoSavedSettings
                        | ImGuiTableFlags.BordersInnerV
                        | ImGuiTableFlags.SizingStretchProp
                        | ImGuiTableFlags.NoPadOuterX))
            {
                ImGui.TableSetupColumn("RuleSwitch", ImGuiTableColumnFlags.WidthFixed, switchColumnWidth);
                ImGui.TableSetupColumn("RuleOptions", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                DrawRuleSwitchColumn(ruleIndex, currentRule);

                ImGui.TableNextColumn();
                DrawRuleOptionsColumn(ruleIndex, currentRule);

                ImGui.EndTable();
            }

            UpdateRuleDragTarget(ruleIndex);

            if (_ruleDragSourceIndex.HasValue
                && _ruleDragTargetIndex == ruleIndex
                && _ruleDragSourceIndex != ruleIndex)
            {
                var rowMin = ImGui.GetItemRectMin();
                var rowMax = ImGui.GetItemRectMax();
                ImGui.GetWindowDrawList().AddRect(
                    rowMin,
                    rowMax,
                    ImGui.GetColorU32(ImGuiCol.DragDropTarget),
                    0f,
                    0,
                    2f);
            }

            if (ruleIndex == currentMatchedRuleIndex)
                ImGui.PopStyleColor(3);
        }

        private void DrawRuleSwitchColumn(int ruleIndex, HeelsRule currentRule)
        {
            var columnWidth = Math.Max(0f, ImGui.GetContentRegionAvail().X);

            var isActive = currentRule.IsActive;
            if (ImGui.Checkbox($"##{Localization.Active}", ref isActive))
            {
                GetActiveRules()[ruleIndex].IsActive = isActive;
                SaveConfig();
            }

            DrawRuleDragHandle(ruleIndex, columnWidth);

            var deleteWidth = ImGui.CalcTextSize(Localization.Delete).X
                + ImGui.GetStyle().FramePadding.X * 2f;
            if (ImGui.Button(Localization.Delete, new Vector2(Math.Max(deleteWidth, columnWidth), 0)))
            {
                GetActiveRules().RemoveAt(ruleIndex);
                SaveConfig();
            }
        }

        private void DrawRuleDragHandle(int ruleIndex, float columnWidth)
        {
            if (ImGui.Button(
                    $"{Localization.RuleDragHandle}##RuleDrag{ruleIndex}",
                    new Vector2(Math.Max(0f, columnWidth), ImGui.GetFrameHeight())))
            {
            }

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Localization.RuleDragHandleTooltip);

            if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                _ruleDragSourceIndex ??= ruleIndex;
        }

        private void UpdateRuleDragTarget(int ruleIndex)
        {
            if (!_ruleDragSourceIndex.HasValue || !ImGui.IsMouseDown(ImGuiMouseButton.Left))
                return;

            var mousePos = ImGui.GetIO().MousePos;
            var rowMin = ImGui.GetItemRectMin();
            var rowMax = ImGui.GetItemRectMax();
            if (mousePos.X < rowMin.X || mousePos.X > rowMax.X
                || mousePos.Y < rowMin.Y || mousePos.Y > rowMax.Y)
            {
                return;
            }

            _ruleDragTargetIndex = ruleIndex;
        }

        private void ProcessRuleDragReorder()
        {
            if (!_ruleDragSourceIndex.HasValue)
                return;

            if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                ImGui.SetTooltip(Localization.RuleDragPreview(_ruleDragSourceIndex.Value + 1));
                return;
            }

            if (_ruleDragTargetIndex.HasValue
                && _ruleDragSourceIndex.Value != _ruleDragTargetIndex.Value)
            {
                ReorderRule(_ruleDragSourceIndex.Value, _ruleDragTargetIndex.Value);
            }

            _ruleDragSourceIndex = null;
            _ruleDragTargetIndex = null;
        }

        private bool IsHonorificRuleToggleAvailable() =>
            HonorificInterop.IsHonorificLoaded(PluginInterface) && isHonorificIpcReady;

        private bool IsMoodlesRuleToggleAvailable() =>
            MoodlesInterop.IsMoodlesLoaded(PluginInterface) && isMoodlesIpcReady;

        private void DrawRuleOptionsColumn(int ruleIndex, HeelsRule currentRule)
        {
            var formatString = $"%.{Configuration.DecimalPrecision}f";
            var tolerance = MathF.Pow(10f, -Math.Clamp(Configuration.DecimalPrecision, 0, 5));
            var isUnreachable = RuleHeightAnalysis.IsUnreachable(GetActiveRules(), ruleIndex, tolerance);

            ImGui.AlignTextToFramePadding();
            DrawRuleBranchSelector(ruleIndex, currentRule, isUnreachable);
            ImGui.SameLine();

            var isElseBranch = ruleIndex > 0 && currentRule.BranchKind == RuleBranchKind.Else;
            if (!isElseBranch)
            {
                ImGui.SetNextItemWidth(52);
                DrawHeightComparisonSelector(ruleIndex, currentRule, isUnreachable);

                ImGui.SameLine();
                ImGui.SetNextItemWidth(72);
                var heightValue = currentRule.HeightValue;
                if (isUnreachable)
                    ImGui.BeginDisabled();

                if (ImGui.DragFloat(Localization.HeightValue, ref heightValue, 0.01f, -10f, 10f, formatString))
                {
                    GetActiveRules()[ruleIndex].HeightValue = heightValue;
                    SaveConfig();
                }

                if (isUnreachable)
                {
                    if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                        ImGui.SetTooltip(Localization.RuleUnreachableHint);
                    ImGui.EndDisabled();
                }
            }
            else
            {
                if (isUnreachable)
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.3f, 0.3f, 1.0f));

                ImGui.TextDisabled(Localization.RuleElseNoCondition);

                if (isUnreachable)
                {
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip(Localization.RuleUnreachableHint);
                    ImGui.PopStyleColor();
                }
            }

            DrawRuleActionsList(ruleIndex, currentRule);

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            DrawOptionalAddonToggle(
                ruleIndex,
                IsHonorificRuleToggleAvailable(),
                Localization.EnableHonorific,
                Localization.EnableHonorificDisabled,
                currentRule.EnableHonorific,
                enabled =>
                {
                    GetActiveRules()[ruleIndex].EnableHonorific = enabled;
                    SyncOptionalAddonsToOtherMode(ruleIndex);
                    SaveConfig();
                });

            if (currentRule.EnableHonorific && IsHonorificRuleToggleAvailable())
                DrawHonorificSelector(ruleIndex, currentRule);

            DrawOptionalAddonToggle(
                ruleIndex,
                IsMoodlesRuleToggleAvailable(),
                Localization.EnableMoodles,
                Localization.EnableMoodlesDisabled,
                currentRule.EnableMoodles,
                enabled =>
                {
                    GetActiveRules()[ruleIndex].EnableMoodles = enabled;
                    SyncOptionalAddonsToOtherMode(ruleIndex);
                    SaveConfig();
                });

            if (currentRule.EnableMoodles && IsMoodlesRuleToggleAvailable())
                DrawMoodleSelector(ruleIndex, currentRule);
        }

        private void DrawRuleBranchSelector(int ruleIndex, HeelsRule rule, bool isUnreachable)
        {
            if (ruleIndex == 0)
            {
                if (isUnreachable)
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.3f, 0.3f, 1.0f));

                ImGui.TextDisabled(Localization.RuleOrderIf);

                if (isUnreachable)
                {
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip(Localization.RuleUnreachableHint);
                    ImGui.PopStyleColor();
                }

                return;
            }

            var isInvalidElse = rule.BranchKind == RuleBranchKind.Else
                && ruleIndex < GetActiveRules().Count - 1;
            if (isInvalidElse || isUnreachable)
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.3f, 0.3f, 1.0f));

            ImGui.SetNextItemWidth(96);
            var branch = rule.BranchKind;
            var preview = Localization.RuleBranchLabel(branch);
            if (ImGui.BeginCombo($"##RuleBranch{ruleIndex}", preview))
            {
                if (ImGui.Selectable(
                        Localization.RuleBranchLabel(RuleBranchKind.ElseIf),
                        branch == RuleBranchKind.ElseIf))
                {
                    GetActiveRules()[ruleIndex].BranchKind = RuleBranchKind.ElseIf;
                    SaveConfig();
                }

                if (ImGui.Selectable(
                        Localization.RuleBranchLabel(RuleBranchKind.Else),
                        branch == RuleBranchKind.Else))
                {
                    GetActiveRules()[ruleIndex].BranchKind = RuleBranchKind.Else;
                    SaveConfig();
                }

                ImGui.EndCombo();
            }

            if (isInvalidElse && ImGui.IsItemHovered())
                ImGui.SetTooltip(Localization.RuleBranchElseInvalidHint);
            else if (isUnreachable && ImGui.IsItemHovered())
                ImGui.SetTooltip(Localization.RuleUnreachableHint);

            if (isInvalidElse || isUnreachable)
                ImGui.PopStyleColor();
        }

        private void DrawHeightComparisonSelector(int ruleIndex, HeelsRule rule, bool isUnreachable)
        {
            var comparison = rule.HeightComparison;
            var preview = Localization.HeightComparisonSymbol(comparison);

            if (isUnreachable)
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.3f, 0.3f, 1.0f));

            if (ImGui.BeginCombo($"##HeightComparison{ruleIndex}", preview))
            {
                foreach (HeightComparison candidate in Enum.GetValues<HeightComparison>())
                {
                    if (ImGui.Selectable(
                            Localization.HeightComparisonSymbol(candidate),
                            candidate == comparison))
                    {
                        GetActiveRules()[ruleIndex].HeightComparison = candidate;
                        SaveConfig();
                    }

                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip(Localization.HeightComparisonTooltip(candidate));
                }

                ImGui.EndCombo();
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(isUnreachable
                    ? Localization.RuleUnreachableHint
                    : Localization.HeightComparisonTooltip(comparison));
            }

            if (isUnreachable)
                ImGui.PopStyleColor();
        }

        private static void DrawOptionalAddonToggle(
            int ruleIndex,
            bool isAvailable,
            string enabledLabel,
            string disabledLabel,
            bool value,
            Action<bool> onChanged)
        {
            var label = isAvailable ? enabledLabel : disabledLabel;
            if (!isAvailable)
                ImGui.BeginDisabled();

            var toggle = value;
            if (ImGui.Checkbox($"{label}##OptionalAddon{ruleIndex}{enabledLabel}", ref toggle))
                onChanged(toggle);

            if (!isAvailable)
            {
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    ImGui.SetTooltip(Localization.OptionalAddonDisabledTooltip);
                ImGui.EndDisabled();
            }
        }

        private void DrawRuleActionsList(int ruleIndex, HeelsRule rule)
        {
            EnsureRuleHasActions(ruleIndex);
            var actions = GetActiveRules()[ruleIndex].Actions;

            for (var actionIndex = 0; actionIndex < actions.Count; actionIndex++)
            {
                if (actionIndex > 0)
                {
                    ImGui.Spacing();
                    ImGui.Separator();
                    ImGui.Spacing();
                }

                ImGui.PushID(actionIndex);
                DrawRuleActionRow(ruleIndex, actionIndex, actions[actionIndex], actions.Count > 1, out var deleted);
                ImGui.PopID();
                if (deleted)
                    break;
            }

            if (ImGui.SmallButton($"{Localization.AddRuleAction}##Rule{ruleIndex}"))
            {
                actions.Add(CreateDefaultRuleActionForMode(Configuration.Mode));
                SaveConfig();
            }
        }

        private void DrawRuleActionRow(
            int ruleIndex,
            int actionIndex,
            HeelsRuleAction action,
            bool canDelete,
            out bool deleted)
        {
            deleted = false;
            ImGui.AlignTextToFramePadding();

            var expanded = !action.IsActionCollapsed;
            if (ImGui.ArrowButton(
                    $"##ActionCollapse{ruleIndex}{actionIndex}",
                    expanded ? ImGuiDir.Down : ImGuiDir.Right))
            {
                action.IsActionCollapsed = !action.IsActionCollapsed;
                SaveConfig();
            }

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Localization.RuleActionCollapseTooltip);

            ImGui.SameLine();
            ImGui.TextDisabled(Localization.RuleActionLabel(actionIndex + 1));

            if (canDelete)
            {
                ImGui.SameLine();
                if (ImGui.SmallButton($"{Localization.DeleteRuleAction}##Rule{ruleIndex}Action{actionIndex}"))
                {
                    GetActiveRules()[ruleIndex].Actions.RemoveAt(actionIndex);
                    SaveConfig();
                    deleted = true;
                    return;
                }
            }

            if (action.IsActionCollapsed)
                return;

            ImGui.Indent();
            if (Configuration.Mode == PluginMode.Glamourer)
            {
                ImGui.SetNextItemWidth(-1);
                DrawGlamourerDesignSelector(ruleIndex, actionIndex, action);
                ImGui.Unindent();
                return;
            }

            if (!_penumbraInterop.IsIpcAvailable())
            {
                ImGui.TextDisabled(Localization.PenumbraListUnavailable);
                ImGui.Unindent();
                return;
            }

            var idSuffix = $"R{ruleIndex}A{actionIndex}";
            ImGui.SetNextItemWidth(-1);
            DrawPenumbraCollectionSelector(idSuffix, action);
            ImGui.SetNextItemWidth(-1);
            DrawPenumbraModSelector(idSuffix, action);
            DrawPenumbraGlamourerTakeoverControls(action);
            ImGui.SetNextItemWidth(-1);
            DrawPenumbraOptionGroupSelector(idSuffix, action);

            var groupType = _penumbraInterop.GetOptionGroupType(
                action.PenumbraModName ?? "",
                action.PenumbraOption ?? "");
            if (PenumbraInterop.UsesBoolOptionValue(groupType))
                DrawPenumbraMultiToggleList(idSuffix, action);
            else
            {
                ImGui.SetNextItemWidth(-1);
                DrawPenumbraOptionCombo(idSuffix, action);
            }

            ImGui.Unindent();
        }

        private void DrawPenumbraGlamourerTakeoverControls(HeelsRuleAction action)
        {
            var modDirectory = action.PenumbraModName ?? "";
            if (string.IsNullOrWhiteSpace(modDirectory))
                return;

            var localPlayer = ObjectTable.LocalPlayer;
            if (localPlayer == null || !localPlayer.IsValid())
                return;

            if (!_penumbraInterop.IsModUnderGlamourerTakeover(localPlayer.ObjectIndex, modDirectory))
                return;

            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.85f, 0.55f, 0.1f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.95f, 0.65f, 0.15f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.75f, 0.45f, 0.05f, 1.0f));

            if (ImGui.Button($"{Localization.ClearGlamourerTempSettings}##Takeover{modDirectory}"))
            {
                if (_penumbraInterop.TryRemoveGlamourerTemporaryModSettings(
                        modDirectory,
                        localPlayer.ObjectIndex,
                        out var clearError))
                {
                    lastError = "";
                    PluginLog.Info($"Cleared Glamourer temporary Penumbra settings for mod: {modDirectory}");
                }
                else
                {
                    lastError = clearError;
                    PluginLog.Warning($"Failed to clear Glamourer temporary settings: {clearError}");
                }
            }

            ImGui.PopStyleColor(3);

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Localization.ClearGlamourerTempSettingsTooltipActive);

            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.78f, 0.2f, 1.0f));
            ImGui.Text(Localization.PenumbraModGlamourerTakeover);
            ImGui.PopStyleColor();
        }

        private void DrawPenumbraCollectionSelector(string idSuffix, HeelsRuleAction action)
        {
            var collections = _penumbraInterop.GetCollectionNames();
            var selected = action.PenumbraCollection ?? "";
            var preview = string.IsNullOrWhiteSpace(selected)
                ? Localization.SelectPenumbraCollection
                : selected;

            if (!collections.Any(c => string.Equals(c, selected, StringComparison.OrdinalIgnoreCase))
                && !string.IsNullOrWhiteSpace(selected))
            {
                preview = Localization.PenumbraValueNotInList(selected);
            }

            if (ImGui.BeginCombo($"##PenumbraCollectionCombo{idSuffix}", preview))
            {
                if (collections.Count == 0)
                {
                    ImGui.TextDisabled(Localization.PenumbraCollectionListEmpty);
                }
                else
                {
                    foreach (var name in collections)
                    {
                        if (ImGui.Selectable(name, string.Equals(name, selected, StringComparison.OrdinalIgnoreCase)))
                        {
                            action.PenumbraCollection = name;
                            SaveConfig();
                        }
                    }
                }

                ImGui.EndCombo();
            }

            ImGui.SameLine();
            ImGui.TextDisabled(Localization.Collection);
        }

        private void DrawPenumbraModSelector(string idSuffix, HeelsRuleAction action)
        {
            var mods = _penumbraInterop.GetMods();
            var selectedDirectory = action.PenumbraModName ?? "";
            var selectedMod = mods.FirstOrDefault(m =>
                string.Equals(m.Directory, selectedDirectory, StringComparison.OrdinalIgnoreCase));

            var preview = selectedMod != null
                ? Localization.PenumbraModLabel(selectedMod.DisplayName, selectedMod.Directory)
                : string.IsNullOrWhiteSpace(selectedDirectory)
                    ? Localization.SelectPenumbraMod
                    : Localization.PenumbraValueNotInList(selectedDirectory);

            if (ImGui.BeginCombo($"##PenumbraModCombo{idSuffix}", preview))
            {
                if (mods.Count == 0)
                {
                    ImGui.TextDisabled(Localization.PenumbraModListEmpty);
                }
                else
                {
                    ImGui.SetNextItemWidth(-1);
                    if (ImGui.IsWindowAppearing())
                        ImGui.SetKeyboardFocusHere();
                    if (ImGui.InputTextWithHint("##PenumbraModSearch", Localization.PenumbraModSearchHint, ref penumbraModSearchFilter, 128))
                    {
                    }

                    var filteredMods = FilterPenumbraMods(mods, penumbraModSearchFilter).ToList();
                    if (filteredMods.Count == 0)
                    {
                        ImGui.TextDisabled(Localization.PenumbraModSearchNoResults);
                    }
                    else
                    {
                        if (string.IsNullOrWhiteSpace(penumbraModSearchFilter)
                            && ImGui.Selectable(Localization.SelectPenumbraMod, string.IsNullOrWhiteSpace(selectedDirectory)))
                        {
                            action.PenumbraModName = "";
                            action.PenumbraOption = "";
                            penumbraModSearchFilter = "";
                            SaveConfig();
                        }

                        foreach (var mod in filteredMods)
                        {
                            var label = Localization.PenumbraModLabel(mod.DisplayName, mod.Directory);
                            if (ImGui.Selectable(label, string.Equals(mod.Directory, selectedDirectory, StringComparison.OrdinalIgnoreCase)))
                            {
                                if (!string.Equals(mod.Directory, selectedDirectory, StringComparison.OrdinalIgnoreCase))
                                {
                                    action.PenumbraModName = mod.Directory;
                                    action.PenumbraOption = "";
                                    penumbraModSearchFilter = "";
                                    _penumbraInterop.InvalidateModSettingsCache();
                                    SaveConfig();
                                }
                            }
                        }
                    }
                }

                ImGui.EndCombo();
            }

            ImGui.SameLine();
            ImGui.TextDisabled(Localization.ModName);
        }

        private static IEnumerable<PenumbraModEntry> FilterPenumbraMods(
            IReadOnlyList<PenumbraModEntry> mods,
            string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return mods;

            var terms = keyword.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return mods.Where(mod => terms.All(term =>
                mod.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || mod.Directory.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        private void DrawPenumbraOptionGroupSelector(string idSuffix, HeelsRuleAction action)
        {
            var modDirectory = action.PenumbraModName ?? "";
            var groups = _penumbraInterop.GetOptionGroups(modDirectory);
            var selected = action.PenumbraOption ?? "";
            var preview = string.IsNullOrWhiteSpace(selected)
                ? Localization.SelectPenumbraOptionGroup
                : selected;

            if (!groups.Any(g => string.Equals(g, selected, StringComparison.OrdinalIgnoreCase))
                && !string.IsNullOrWhiteSpace(selected))
            {
                preview = Localization.PenumbraValueNotInList(selected);
            }

            if (ImGui.BeginCombo($"##PenumbraOptionGroupCombo{idSuffix}", preview))
            {
                if (ImGui.IsWindowAppearing()
                    && groups.Count == 0
                    && !string.IsNullOrWhiteSpace(modDirectory))
                    groups = _penumbraInterop.GetOptionGroups(modDirectory, force: true);

                if (string.IsNullOrWhiteSpace(modDirectory))
                {
                    ImGui.TextDisabled(Localization.SelectPenumbraModFirst);
                }
                else if (groups.Count == 0)
                {
                    ImGui.TextDisabled(Localization.PenumbraOptionGroupListEmpty);
                }
                else
                {
                    foreach (var group in groups)
                    {
                        var groupType = _penumbraInterop.GetOptionGroupType(modDirectory, group);
                        var label = $"{group} [{Localization.PenumbraGroupTypeLabel(groupType)}]";
                        if (ImGui.Selectable(label, string.Equals(group, selected, StringComparison.OrdinalIgnoreCase)))
                        {
                            if (!string.Equals(group, selected, StringComparison.OrdinalIgnoreCase))
                            {
                                action.PenumbraOption = group;
                                action.PenumbraOptionName = "";
                                action.PenumbraMultiToggleStates.Clear();
                                SaveConfig();
                            }
                        }

                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip(Localization.PenumbraOptionGroupTypeHint(groupType));
                    }
                }

                ImGui.EndCombo();
            }

            if (!string.IsNullOrWhiteSpace(modDirectory) && !string.IsNullOrWhiteSpace(selected))
            {
                var selectedType = _penumbraInterop.GetOptionGroupType(modDirectory, selected);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(Localization.PenumbraOptionGroupTypeHint(selectedType));
            }

            ImGui.SameLine();
            ImGui.TextDisabled(Localization.Option);
        }

        private void DrawPenumbraOptionCombo(string idSuffix, HeelsRuleAction action)
        {
            var modDirectory = action.PenumbraModName ?? "";
            var optionGroup = action.PenumbraOption ?? "";
            var optionName = action.PenumbraOptionName ?? "";
            var options = _penumbraInterop.GetOptionNames(modDirectory, optionGroup);

            var preview = string.IsNullOrWhiteSpace(optionName)
                ? Localization.SelectPenumbraOptionName
                : optionName;

            if (!options.Any(o => string.Equals(o, optionName, StringComparison.OrdinalIgnoreCase))
                && !string.IsNullOrWhiteSpace(optionName))
            {
                preview = Localization.PenumbraValueNotInList(optionName);
            }

            if (ImGui.BeginCombo($"##PenumbraRuleOptionCombo{idSuffix}", preview))
            {
                if (ImGui.IsWindowAppearing()
                    && options.Count == 0
                    && !string.IsNullOrWhiteSpace(modDirectory)
                    && !string.IsNullOrWhiteSpace(optionGroup))
                {
                    options = _penumbraInterop.GetOptionNames(modDirectory, optionGroup, force: true);
                }

                if (string.IsNullOrWhiteSpace(modDirectory) || string.IsNullOrWhiteSpace(optionGroup))
                {
                    ImGui.TextDisabled(Localization.SelectPenumbraOptionGroupFirst);
                }
                else if (options.Count == 0)
                {
                    ImGui.TextDisabled(Localization.PenumbraOptionNameListEmpty);
                }
                else
                {
                    if (ImGui.Selectable(Localization.SelectPenumbraOptionName, string.IsNullOrWhiteSpace(optionName)))
                    {
                        action.PenumbraOptionName = "";
                        SaveConfig();
                    }

                    foreach (var option in options)
                    {
                        if (ImGui.Selectable(option, string.Equals(option, optionName, StringComparison.OrdinalIgnoreCase)))
                        {
                            action.PenumbraOptionName = option;
                            SaveConfig();
                        }
                    }
                }

                ImGui.EndCombo();
            }

            ImGui.SameLine();
            ImGui.TextDisabled(Localization.OptionName);
        }

        private void DrawPenumbraMultiToggleList(string idSuffix, HeelsRuleAction action)
        {
            var modDirectory = action.PenumbraModName ?? "";
            var optionGroup = action.PenumbraOption ?? "";
            if (string.IsNullOrWhiteSpace(modDirectory) || string.IsNullOrWhiteSpace(optionGroup))
            {
                ImGui.TextDisabled(Localization.SelectPenumbraOptionGroupFirst);
                return;
            }

            var options = _penumbraInterop.GetOptionNames(modDirectory, optionGroup);
            if (options.Count == 0)
            {
                ImGui.TextDisabled(Localization.PenumbraOptionNameListEmpty);
                return;
            }

            if (SyncPenumbraMultiToggleStates(action, options))
                SaveConfig();

            ImGui.TextDisabled(Localization.PenumbraMultiToggleListLabel);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Localization.PenumbraMultiToggleListHint);

            ImGui.Indent();
            foreach (var option in options)
            {
                var enabled = action.PenumbraMultiToggleStates.TryGetValue(option, out var state) && state;
                if (ImGui.Checkbox($"{option}##PenumbraMultiToggle{idSuffix}{option}", ref enabled))
                {
                    action.PenumbraMultiToggleStates[option] = enabled;
                    SaveConfig();
                }
            }

            ImGui.Unindent();
        }

        private void DrawGlamourerDesignSelector(int ruleIndex, int actionIndex, HeelsRuleAction action)
        {
            var name = action.GlamourerDesign ?? "";
            if (!_glamourerInterop.IsIpcAvailable())
            {
                if (ImGui.InputTextWithHint($"##DesignInput{ruleIndex}A{actionIndex}", Localization.GlamourerDesignManualFallback, ref name, 256))
                {
                    _glamourerInterop.InvalidateCache(action.GlamourerDesign);
                    action.GlamourerDesign = name;
                    SaveConfig();
                }

                DrawGlamourerFeetApplyMark(name);
                return;
            }

            var designNames = _glamourerInterop.GetDesignNames();
            var preview = string.IsNullOrWhiteSpace(name)
                ? Localization.SelectGlamourerDesign
                : name;

            var inList = designNames.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
            if (!inList && !string.IsNullOrWhiteSpace(name))
                preview = Localization.GlamourerDesignNotInList(name);

            if (ImGui.BeginCombo($"##DesignCombo{ruleIndex}A{actionIndex}", preview))
            {
                if (designNames.Count == 0)
                {
                    ImGui.TextDisabled(Localization.GlamourerDesignListEmpty);
                }
                else
                {
                    if (ImGui.Selectable(Localization.SelectGlamourerDesign, string.IsNullOrWhiteSpace(name)))
                    {
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            _glamourerInterop.InvalidateCache(name);
                            action.GlamourerDesign = "";
                            SaveConfig();
                        }
                    }

                    foreach (var listedName in designNames)
                    {
                        if (ImGui.Selectable(listedName, string.Equals(listedName, name, StringComparison.OrdinalIgnoreCase)))
                        {
                            if (!string.Equals(listedName, name, StringComparison.OrdinalIgnoreCase))
                            {
                                _glamourerInterop.InvalidateCache(name);
                                action.GlamourerDesign = listedName;
                                SaveConfig();
                            }
                        }
                    }
                }

                ImGui.EndCombo();
            }

            DrawGlamourerFeetApplyMark(name);
        }

        private bool AnyRuleShowsFeetEquipmentWarning()
        {
            if (Configuration.Mode != PluginMode.Glamourer)
                return false;

            return Configuration.GlamourerRules.Any(rule =>
                GetRuleActions(rule).Any(action =>
                    !string.IsNullOrWhiteSpace(action.GlamourerDesign)
                    && _glamourerInterop.DesignAppliesFeet(action.GlamourerDesign) == true));
        }

        private string BuildFeetWarningSignature()
        {
            if (Configuration.Mode != PluginMode.Glamourer)
                return "";

            var parts = new List<string>();
            for (int i = 0; i < Configuration.GlamourerRules.Count; i++)
            {
                var actionIndex = 0;
                foreach (var action in GetRuleActions(Configuration.GlamourerRules[i]))
                {
                    var design = action.GlamourerDesign ?? "";
                    if (!string.IsNullOrWhiteSpace(design)
                        && _glamourerInterop.DesignAppliesFeet(design) == true)
                    {
                        parts.Add($"{i}:{actionIndex}:{design}");
                    }

                    actionIndex++;
                }
            }

            return string.Join("|", parts);
        }

        private void DrawGlamourerFeetWarningBanner()
        {
            var signature = BuildFeetWarningSignature();

            // 脚部设计集合变化时（含切走再切回）清除隐藏状态，以便再次提示
            if (!string.IsNullOrEmpty(signature)
                && !string.Equals(signature, lastFeetWarningSignature, StringComparison.Ordinal))
            {
                feetWarningDismissedSignature = "";
            }

            lastFeetWarningSignature = signature;

            if (!AnyRuleShowsFeetEquipmentWarning())
                return;

            if (string.Equals(signature, feetWarningDismissedSignature, StringComparison.Ordinal))
                return;

            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.3f, 0.3f, 1.0f));
            ImGui.TextWrapped(Localization.GlamourerFeetWarningBanner);
            ImGui.PopStyleColor();

            if (ImGui.Button(Localization.HideFeetWarningBanner))
                feetWarningDismissedSignature = signature;

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
        }

        private void DrawGlamourerFeetApplyMark(string designName)
        {
            if (_glamourerInterop.DesignAppliesFeet(designName) != true)
                return;

            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.78f, 0.2f, 1.0f));
            ImGui.Text(Localization.GlamourerFeetApplyMark);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Localization.GlamourerFeetApplyTooltip);
            ImGui.PopStyleColor();
        }

        private void DrawHonorificSelector(int ruleIndex, HeelsRule rule)
        {
            ImGui.SetNextItemWidth(-1);
            var titleJson = rule.HonorificTitleJson ?? "";
            if (!_honorificInterop.IsIpcAvailable())
            {
                if (ImGui.InputTextWithHint($"##HonorificManual{ruleIndex}", Localization.HonorificManualFallback, ref titleJson, 4096))
                {
                    GetActiveRules()[ruleIndex].HonorificTitleJson = titleJson;
                    SyncOptionalAddonsToOtherMode(ruleIndex);
                    SaveConfig();
                }
                return;
            }

            var options = _honorificInterop.GetTitleOptions(ObjectTable.LocalPlayer);
            var selected = options.FirstOrDefault(o => string.Equals(o.Json, titleJson, StringComparison.Ordinal));
            var preview = selected?.Display
                ?? (string.IsNullOrWhiteSpace(titleJson)
                    ? Localization.SelectHonorificTitle
                    : Localization.HonorificNotInList);

            if (ImGui.BeginCombo($"##HonorificCombo{ruleIndex}", preview))
            {
                if (options.Count == 0)
                {
                    ImGui.TextDisabled(Localization.HonorificListEmpty);
                }
                else
                {
                    if (ImGui.Selectable(Localization.SelectHonorificTitle, string.IsNullOrWhiteSpace(titleJson)))
                    {
                        GetActiveRules()[ruleIndex].HonorificTitleJson = "";
                        SyncOptionalAddonsToOtherMode(ruleIndex);
                        SaveConfig();
                    }

                    foreach (var option in options)
                    {
                        if (ImGui.Selectable(option.Display, string.Equals(option.Json, titleJson, StringComparison.Ordinal)))
                        {
                            GetActiveRules()[ruleIndex].HonorificTitleJson = option.Json;
                            SyncOptionalAddonsToOtherMode(ruleIndex);
                            SaveConfig();
                        }
                    }
                }

                ImGui.EndCombo();
            }
        }

        private void DrawMoodleSelector(int ruleIndex, HeelsRule rule)
        {
            ImGui.SetNextItemWidth(-1);
            var moodleGuid = rule.MoodleGuid ?? "";
            if (!_moodlesInterop.IsIpcAvailable())
            {
                if (ImGui.InputTextWithHint($"##MoodleManual{ruleIndex}", Localization.MoodleManualFallback, ref moodleGuid, 64))
                {
                    GetActiveRules()[ruleIndex].MoodleGuid = moodleGuid;
                    SyncOptionalAddonsToOtherMode(ruleIndex);
                    SaveConfig();
                }
                return;
            }

            var items = _moodlesInterop.GetItems();
            MoodleListItem? selected = null;
            if (Guid.TryParse(moodleGuid, out var selectedId))
            {
                selected = items.FirstOrDefault(x =>
                    x.Id == selectedId && x.IsPreset == rule.MoodleIsPreset);
            }

            var preview = selected != null
                ? (selected.IsPreset
                    ? Localization.MoodlePresetLabel(selected.Title)
                    : Localization.MoodleStatusLabel(selected.Title))
                : (string.IsNullOrWhiteSpace(moodleGuid)
                    ? Localization.SelectMoodle
                    : Localization.MoodleNotInList);

            if (ImGui.BeginCombo($"##MoodleCombo{ruleIndex}", preview))
            {
                if (items.Count == 0)
                {
                    ImGui.TextDisabled(Localization.MoodleListEmpty);
                }
                else
                {
                    if (ImGui.Selectable(Localization.SelectMoodle, string.IsNullOrWhiteSpace(moodleGuid)))
                    {
                        GetActiveRules()[ruleIndex].MoodleGuid = "";
                        SyncOptionalAddonsToOtherMode(ruleIndex);
                        SaveConfig();
                    }

                    foreach (var item in items)
                    {
                        var label = item.IsPreset
                            ? Localization.MoodlePresetLabel(item.Title)
                            : Localization.MoodleStatusLabel(item.Title);
                        var isSelected = selected != null
                            && item.Id == selected.Id
                            && item.IsPreset == selected.IsPreset;

                        if (ImGui.Selectable(label, isSelected))
                        {
                            GetActiveRules()[ruleIndex].MoodleGuid = item.Id.ToString();
                            GetActiveRules()[ruleIndex].MoodleIsPreset = item.IsPreset;
                            SyncOptionalAddonsToOtherMode(ruleIndex);
                            SaveConfig();
                        }
                    }
                }

                ImGui.EndCombo();
            }
        }
        
        private void DrawDebugTab()
        {
            if (!string.IsNullOrEmpty(lastError))
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.3f, 0.3f, 1.0f));
                ImGui.TextWrapped($"{Localization.ErrorInfo}: {lastError}");
                ImGui.PopStyleColor();
                ImGui.Separator();
            }
            
            // IPC 测试按钮
            if (ImGui.Button(Localization.TestIPC))
            {
                try
                {
                    var provider = PluginInterface.GetIpcSubscriber<string>("SimpleHeels.GetLocalPlayer");
                    lastIpcData = provider.InvokeFunc();
                    PluginLog.Info($"IPC Data: {lastIpcData}");
                }
                catch (Exception ex)
                {
                    lastError = $"{Localization.TestIPC} {Localization.ErrorInfo}: {ex.Message}";
                    PluginLog.Error($"IPC test failed: {ex}");
                }
            }
            
            ImGui.Separator();
            
            // 状态信息
            var simpleHeelsStatus = !isSimpleHeelsAvailable
                ? $"✗ {Localization.NotAvailable}"
                : isSimpleHeelsIpcReady
                    ? $"✓ {Localization.Available}"
                    : (Localization.IsChine ? "△ 已加载 / IPC 等待中" : "△ Loaded / IPC pending");
            ImGui.Text($"{Localization.SimpleHeelsStatus}: {simpleHeelsStatus}");
            
            if (ConfigurationUsesGlamourer())
            {
                ImGui.Text($"{Localization.GlamourerStatus}: {(isGlamourerAvailable ? $"✓ {Localization.Available}" : $"✗ {Localization.NotAvailable}")}");
            }

            if (ConfigurationUsesPenumbra())
            {
                var penumbraStatus = !PenumbraInterop.IsPenumbraLoaded(PluginInterface)
                    ? $"✗ {Localization.NotAvailable}"
                    : isPenumbraIpcReady
                        ? $"✓ {Localization.PenumbraIpcReady}"
                        : (Localization.IsChine ? "△ 已加载 / IPC 等待中" : "△ Loaded / IPC pending");
                ImGui.Text($"{Localization.PenumbraStatus}: {penumbraStatus}");
            }

            if (ConfigurationUsesMoodles())
            {
                var moodlesStatus = !MoodlesInterop.IsMoodlesLoaded(PluginInterface)
                    ? $"✗ {Localization.NotAvailable}"
                    : isMoodlesIpcReady
                        ? $"✓ {Localization.Available}"
                        : (Localization.IsChine ? "△ 已加载 / IPC 等待中" : "△ Loaded / IPC pending");
                ImGui.Text($"{Localization.MoodlesStatus}: {moodlesStatus}");
            }

            if (ConfigurationUsesHonorific())
            {
                var honorificStatus = !HonorificInterop.IsHonorificLoaded(PluginInterface)
                    ? $"✗ {Localization.NotAvailable}"
                    : isHonorificIpcReady
                        ? $"✓ {Localization.Available}"
                        : (Localization.IsChine ? "△ 已加载 / IPC 等待中" : "△ Loaded / IPC pending");
                ImGui.Text($"{Localization.HonorificStatus}: {honorificStatus}");
            }
            
            ImGui.Text($"{Localization.LastAppliedPrimary}: {(lastAppliedActionKeys.Count == 0 ? "-" : string.Join(", ", lastAppliedActionKeys))}");
            ImGui.Text($"{Localization.LastAppliedHonorific}: {(string.IsNullOrEmpty(lastAppliedHonorificJson) ? "-" : Localization.HonorificApplied)}");
            ImGui.Text($"{Localization.LastAppliedMoodle}: {(string.IsNullOrEmpty(lastAppliedMoodleKey) ? "-" : lastAppliedMoodleKey)}");
            ImGui.Text($"{Localization.ApplyGate}: {applyGateStatus}");

            if (!string.IsNullOrEmpty(lastIpcData))
            {
                ImGui.Separator();
                if (ImGui.CollapsingHeader(Localization.RawIPCData))
                    ImGui.TextWrapped(lastIpcData);
            }
        }

        private void DrawChangelogTab()
        {
            ImGui.BeginChild("ChangelogScroll", new Vector2(0, 0), false, ImGuiWindowFlags.None);

            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.95f, 0.45f, 0.2f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1.0f, 0.55f, 0.28f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.85f, 0.38f, 0.15f, 1.0f));
            if (ImGui.Button(Localization.KoFiSupportButton))
                Util.OpenLink(KoFiUrl);
            ImGui.PopStyleColor(3);

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Localization.KoFiSupportTooltip);

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            foreach (var entry in Changelog.Entries)
            {
                var isCurrent = entry.Version == Changelog.CurrentVersion;
                if (isCurrent)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.4f, 1.0f, 0.5f, 1.0f));
                }
                
                ImGui.Text($"{Localization.ChangelogVersion} {entry.Version}  ·  {Localization.ChangelogDate} {entry.Date}");
                if (isCurrent)
                {
                    ImGui.PopStyleColor();
                }
                
                var items = Localization.IsChine ? entry.ItemsZh : entry.ItemsEn;
                foreach (var item in items)
                {
                    ImGui.BulletText(item);
                }
                
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
            }
            
            ImGui.EndChild();
        }

        private void MigrateRulesToHeightComparison()
        {
            foreach (var rule in Configuration.Rules)
            {
                if (rule.MinHeight >= 0.02f)
                {
                    rule.HeightComparison = HeightComparison.GreaterThanOrEqual;
                    rule.HeightValue = rule.MinHeight;
                }
                else
                {
                    rule.HeightComparison = HeightComparison.LessThanOrEqual;
                    rule.HeightValue = rule.MaxHeight;
                }
            }
        }

        private void MigrateRulesToBranchKind()
        {
            foreach (var rule in Configuration.Rules)
                rule.BranchKind = RuleBranchKind.ElseIf;
        }

        private void MigrateRulesToPerRuleActions()
        {
            var globalCollection = Configuration.PenumbraCollection ?? "Default";
            var globalMod = Configuration.PenumbraModName ?? "";
            var globalOption = Configuration.PenumbraOption ?? "";

            foreach (var rule in Configuration.Rules)
            {
                if (rule.Actions is { Count: > 0 })
                    continue;

                rule.Actions =
                [
                    new HeelsRuleAction
                    {
                        GlamourerDesign = rule.GlamourerDesign ?? "",
                        PenumbraCollection = globalCollection,
                        PenumbraModName = globalMod,
                        PenumbraOption = globalOption,
                        PenumbraOptionName = rule.PenumbraOptionName ?? "",
                        PenumbraOptionEnabled = rule.PenumbraOptionEnabled,
                    },
                ];
            }
        }

        private bool TryMatchRule(HeelsRule rule, int ruleIndex, float height)
        {
            if (!rule.IsActive)
                return false;

            if (ruleIndex > 0 && rule.BranchKind == RuleBranchKind.Else)
                return true;

            return RuleMatchesHeightCondition(rule, height);
        }

        private bool RuleMatchesHeightCondition(HeelsRule rule, float height)
        {
            var tolerance = MathF.Pow(10f, -Math.Clamp(Configuration.DecimalPrecision, 0, 5));
            return rule.HeightComparison switch
            {
                HeightComparison.GreaterThan => height > rule.HeightValue,
                HeightComparison.GreaterThanOrEqual => height >= rule.HeightValue - tolerance,
                HeightComparison.LessThan => height < rule.HeightValue,
                HeightComparison.LessThanOrEqual => height <= rule.HeightValue + tolerance,
                HeightComparison.Equal => MathF.Abs(height - rule.HeightValue) <= tolerance,
                _ => false,
            };
        }

        private void ReorderRule(int fromIndex, int toIndex)
        {
            var rules = GetActiveRules();
            if (fromIndex < 0
                || toIndex < 0
                || fromIndex >= rules.Count
                || toIndex >= rules.Count
                || fromIndex == toIndex)
            {
                return;
            }

            ReorderRulesInList(rules, fromIndex, toIndex);
            FixMisplacedElseBranches(rules);
            SaveConfig();
        }

        private static void ReorderRulesInList(List<HeelsRule> rules, int fromIndex, int toIndex)
        {
            var rule = rules[fromIndex];
            rules.RemoveAt(fromIndex);
            rules.Insert(toIndex, rule);
        }

        private void MigrateRulesToPerModeLists()
        {
            if (Configuration.GlamourerRules.Count > 0 || Configuration.PenumbraRules.Count > 0)
                return;

            var source = Configuration.Rules;
            if (source == null || source.Count == 0)
                return;

            Configuration.GlamourerRules = CloneRulesForMode(source, PluginMode.Glamourer);
            Configuration.PenumbraRules = CloneRulesForMode(source, PluginMode.Penumbra);
        }

        private List<HeelsRule> CloneRulesForMode(List<HeelsRule> source, PluginMode mode)
        {
            var result = new List<HeelsRule>();
            foreach (var rule in source)
            {
                var cloned = new HeelsRule
                {
                    BranchKind = rule.BranchKind,
                    HeightComparison = rule.HeightComparison,
                    HeightValue = rule.HeightValue,
                    IsActive = rule.IsActive,
                    EnableHonorific = rule.EnableHonorific,
                    HonorificTitleJson = rule.HonorificTitleJson ?? "",
                    EnableMoodles = rule.EnableMoodles,
                    MoodleGuid = rule.MoodleGuid ?? "",
                    MoodleIsPreset = rule.MoodleIsPreset,
                    Actions = [],
                };

                foreach (var action in GetRuleActions(rule))
                    cloned.Actions.Add(CloneActionForMode(action, mode));

                if (cloned.Actions.Count == 0)
                    cloned.Actions.Add(CreateDefaultRuleActionForMode(mode));

                result.Add(cloned);
            }

            return result;
        }

        private static HeelsRuleAction CloneActionForMode(HeelsRuleAction source, PluginMode mode)
        {
            if (mode == PluginMode.Glamourer)
            {
                return new HeelsRuleAction
                {
                    GlamourerDesign = source.GlamourerDesign ?? "",
                    IsActionCollapsed = source.IsActionCollapsed,
                };
            }

            return new HeelsRuleAction
            {
                PenumbraCollection = string.IsNullOrWhiteSpace(source.PenumbraCollection)
                    ? "Default"
                    : source.PenumbraCollection,
                PenumbraModName = source.PenumbraModName ?? "",
                PenumbraOption = source.PenumbraOption ?? "",
                PenumbraOptionName = source.PenumbraOptionName ?? "",
                PenumbraOptionEnabled = source.PenumbraOptionEnabled,
                PenumbraMultiToggleStates = source.PenumbraMultiToggleStates != null
                    ? new Dictionary<string, bool>(source.PenumbraMultiToggleStates, StringComparer.OrdinalIgnoreCase)
                    : new(StringComparer.OrdinalIgnoreCase),
                IsActionCollapsed = source.IsActionCollapsed,
            };
        }

        private void SaveConfig() => PluginInterface.SavePluginConfig(Configuration);

        public void Dispose()
        {
            ClientState.Logout -= OnLogout;
            Framework.Update -= OnFrameworkUpdate;
            CommandManager.RemoveHandler("/hdl");
            CommandManager.RemoveHandler("/heelsdesign");
        }
    }
}