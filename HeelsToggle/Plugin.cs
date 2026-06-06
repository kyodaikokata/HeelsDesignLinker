using System.Numerics;
using System.Text.RegularExpressions;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Configuration;
using Dalamud.Bindings.ImGui;
using Dalamud.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace HeelsDesignLinker
{
    /// <summary>皇帝套物品 ID 和模型 ID（隐形装备，应视为未装备）</summary>
    public static class EmperorsNewItems
    {
        /// <summary>皇帝套物品 ID（Item ID，国际服）</summary>
        public static readonly HashSet<uint> ItemIds = new()
        {
            10032, // The Emperor's New Hat (头)
            10033, // The Emperor's New Robe (身体)
            10034, // The Emperor's New Gloves (手)
            10035, // The Emperor's New Breeches (腿)
            10036, // The Emperor's New Boots (脚)
            10037, // The Emperor's New Earrings (耳环)
            10038, // The Emperor's New Necklace (项链)
            10039, // The Emperor's New Bracelet (手镯)
            10040, // The Emperor's New Ring (戒指)
            40940, // Smallclothes (无装备/内衣)
            // 13775 The Emperor's New Fists 故意不在此列表：登录主手锚点仍视为已装备
        };
        
        /// <summary>皇帝套模型 ID（Model ID，国际服和国服通用）</summary>
        public static readonly HashSet<ushort> ModelIds = new()
        {
            279,  // 皇帝套装备的模型 ID（头/身体/手/腿/脚等）
            0,    // ModelId=0 通常也表示无装备
        };
        
        /// <summary>检查物品 ID 是否为皇帝套/无装备（隐形装备或Smallclothes）</summary>
        public static bool IsEmperorsNewByItemId(uint itemId)
        {
            // 方法1: 检查已知的特殊 ItemId
            if (ItemIds.Contains(itemId))
                return true;
            
            // 方法2: 检查负数（游戏内部用负数表示特殊物品）
            // uint 范围：0 ~ 4294967295 (2^32 - 1)
            // 负数转换后会变成很大的 uint（> 2^31 = 2147483648）
            // 例如：-141 → 4294967155, -385 → 4294966911
            // 使用更低的阈值来捕获所有负数
            if (itemId > 2147483648u)  // 大于 2^31，认为是负数
                return true;
            
            return false;
        }
        
        /// <summary>检查模型 ID 是否为皇帝套（隐形装备）</summary>
        public static bool IsEmperorsNewByModelId(ushort modelId) => ModelIds.Contains(modelId);
    }
    public enum ActionType
    {
        Glamourer = 0,
        Penumbra = 1,
        // CustomizePlus = 2,  // 已移除：Customize+ 不提供所需的 IPC 方法
        Honorific = 3,
        Moodles = 4
    }

    public enum SimpleHeelsHeightMode
    {
        Default = 0,
        Actual = 1,
        Manual = 2,
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
    
    /// <summary>Penumbra 操作类型</summary>
    public enum PenumbraActionKind
    {
        /// <summary>设置 Mod 选项（Option）</summary>
        SetModOption = 0,
        /// <summary>启用 Mod</summary>
        EnableMod = 1,
        /// <summary>禁用 Mod</summary>
        DisableMod = 2,
    }
    
    /// <summary>基准行动模式</summary>
    public enum BaselineMode
    {
        /// <summary>自动：使用推荐默认设置</summary>
        Auto = 0,
        /// <summary>手动：展开详细设置</summary>
        Manual = 1,
        /// <summary>忽略：不管理此参数</summary>
        Ignore = 2,
    }

    /// <summary>基准手动状态（Glamourer / Moodles / Honorific）</summary>
    public enum BaselineManualState
    {
        Disabled = 0,
        Enabled = 1,
    }

    /// <summary>Penumbra 基准手动选项设置</summary>
    public class BaselinePenumbraOptionSetting
    {
        public string PenumbraOption { get; set; } = "";
        public string PenumbraOptionName { get; set; } = "";
        public bool PenumbraOptionEnabled { get; set; } = true;
        public Dictionary<string, bool> PenumbraMultiToggleStates { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>基准参数标识符</summary>
    public class BaselineParameterId
    {
        /// <summary>参数类型</summary>
        public ActionType Type { get; set; }
        
        /// <summary>Penumbra: Collection名称</summary>
        public string? PenumbraCollection { get; set; }
        
        /// <summary>Penumbra: Mod名称</summary>
        public string? PenumbraModName { get; set; }
        
        /// <summary>Glamourer: Design GUID</summary>
        public string? GlamourerDesign { get; set; }
        
        /// <summary>Moodles: Moodle GUID</summary>
        public string? MoodleGuid { get; set; }
        
        /// <summary>Honorific: Title JSON</summary>
        public string? HonorificTitleJson { get; set; }

        /// <summary>生成唯一键</summary>
        public string GetKey()
        {
            return Type switch
            {
                ActionType.Penumbra => $"P:{PenumbraCollection}|{PenumbraModName}",
                ActionType.Glamourer => $"G:{GlamourerDesign}",
                ActionType.Moodles => $"M:{MoodleGuid}",
                ActionType.Honorific => $"H:{HonorificTitleJson}",
                _ => ""
            };
        }
        
        public override bool Equals(object? obj)
        {
            if (obj is BaselineParameterId other)
                return GetKey() == other.GetKey();
            return false;
        }
        
        public override int GetHashCode() => GetKey().GetHashCode();
    }

    /// <summary>基准行动配置项</summary>
    public class BaselineActionConfig
    {
        /// <summary>参数标识</summary>
        public BaselineParameterId ParameterId { get; set; } = new();
        
        /// <summary>基准模式</summary>
        public BaselineMode Mode { get; set; } = BaselineMode.Auto;
        
        /// <summary>Penumbra 手动：Mod 是否启用</summary>
        public bool ManualModEnabled { get; set; }
        
        /// <summary>Penumbra 手动：选项详细设置</summary>
        public List<BaselinePenumbraOptionSetting> ManualPenumbraOptions { get; set; } = new();
        
        /// <summary>Glamourer / Moodles / Honorific 手动：启用或禁用</summary>
        public BaselineManualState ManualState { get; set; } = BaselineManualState.Disabled;
        
        /// <summary>是否是新检测到的参数（用于UI高亮）</summary>
        [JsonIgnore]
        public bool IsNew { get; set; }
    }

    /// <summary>条件类型枚举</summary>
    public enum ConditionType
    {
        Height = 0,           // 基于高度的条件
        Equipment = 1,        // 基于装备的条件
    }
    
    /// <summary>逻辑运算符</summary>
    public enum LogicOperator
    {
        And = 0,              // 所有条件都必须满足
        Or = 1,               // 任一条件满足即可
    }
    
    /// <summary>装备槽位枚举</summary>
    public enum EquipSlot
    {
        MainHand = 0,
        OffHand = 1,
        Head = 2,
        Body = 3,             // 上衣
        Hands = 4,
        Legs = 5,             // 裤子
        Feet = 6,             // 鞋子
        Ears = 7,
        Neck = 8,
        Wrists = 9,
        RFinger = 10,
        LFinger = 11,
    }
    
    /// <summary>规则条件基类</summary>
    [JsonConverter(typeof(RuleConditionConverter))]
    public abstract class RuleCondition
    {
        public ConditionType Type { get; set; }
        public abstract bool Evaluate(RuleEvaluationContext context);
    }
    
    /// <summary>高度条件</summary>
    public class HeightCondition : RuleCondition
    {
        public HeightComparison Comparison { get; set; } = HeightComparison.LessThanOrEqual;
        public float Value { get; set; } = 0.0f;
        
        public HeightCondition()
        {
            Type = ConditionType.Height;
        }
        
        public override bool Evaluate(RuleEvaluationContext context)
        {
            var height = context.CurrentHeight;
            return Comparison switch
            {
                HeightComparison.GreaterThan => height > Value,
                HeightComparison.GreaterThanOrEqual => height >= Value,
                HeightComparison.LessThan => height < Value,
                HeightComparison.LessThanOrEqual => height <= Value,
                HeightComparison.Equal => Math.Abs(height - Value) < 0.0001f,
                _ => false
            };
        }
    }
    
    /// <summary>装备条件</summary>
    public class EquipmentCondition : RuleCondition
    {
        public EquipSlot Slot { get; set; } = EquipSlot.Body;
        public bool MustBeEquipped { get; set; } = true;  // true = 必须有装备, false = 必须没有装备
        
        public EquipmentCondition()
        {
            Type = ConditionType.Equipment;
        }
        
        public override bool Evaluate(RuleEvaluationContext context)
        {
            if (!context.AllowEquipmentEvaluation)
                return false;

            // 使用 Glamourer 获取投影后的装备状态
            if (context.GlamourerInterop == null)
                return true;  // Glamourer 不可用时，忽略此条件
            
            var hasEquipment = context.GlamourerInterop.HasEquipmentInSlotForLocalPlayer(Slot);
            if (hasEquipment == null)
                return false;  // 无法确认时视为不满足，避免登录加载中误匹配「全裸」等规则
                
            return hasEquipment.Value == MustBeEquipped;
        }
    }
    
    /// <summary>条件组（支持 AND/OR）</summary>
    public class ConditionGroup
    {
        /// <summary>组内条件之间的操作符（AND/OR）</summary>
        public LogicOperator Operator { get; set; } = LogicOperator.And;
        
        /// <summary>连接到下一个条件组的操作符（用于多条件组时）</summary>
        public LogicOperator OperatorToNext { get; set; } = LogicOperator.And;
        
        public List<RuleCondition> Conditions { get; set; } = new();
        
        public bool Evaluate(RuleEvaluationContext context)
        {
            if (Conditions.Count == 0)
                return true;  // 没有条件时默认满足
                
            if (Operator == LogicOperator.And)
            {
                return Conditions.All(c => c.Evaluate(context));
            }
            else  // Or
            {
                return Conditions.Any(c => c.Evaluate(context));
            }
        }
    }
    
    /// <summary>规则评估上下文</summary>
    public class RuleEvaluationContext
    {
        public float CurrentHeight { get; set; }
        public string? CharacterName { get; set; }
        public IPlayerCharacter? LocalPlayer { get; set; }
        internal GlamourerInterop? GlamourerInterop { get; set; }
        /// <summary>登录预热完成前为 false，禁止装备条件评估与 Glamourer GetState 轮询。</summary>
        public bool AllowEquipmentEvaluation { get; set; }
    }
    
    /// <summary>RuleCondition 的 JSON 转换器（支持多态）</summary>
    public class RuleConditionConverter : JsonConverter<RuleCondition>
    {
        public override void WriteJson(JsonWriter writer, RuleCondition? value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }
            
            var obj = new JObject();
            obj["$type"] = value.GetType().Name;
            
            if (value is HeightCondition heightCond)
            {
                obj["Comparison"] = (int)heightCond.Comparison;
                obj["Value"] = heightCond.Value;
            }
            else if (value is EquipmentCondition equipCond)
            {
                obj["Slot"] = (int)equipCond.Slot;
                obj["MustBeEquipped"] = equipCond.MustBeEquipped;
            }
            
            obj.WriteTo(writer);
        }
        
        public override RuleCondition? ReadJson(JsonReader reader, Type objectType, RuleCondition? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;
                
            var obj = JObject.Load(reader);
            var typeName = obj["$type"]?.ToString();
            
            if (typeName == nameof(HeightCondition))
            {
                return new HeightCondition
                {
                    Comparison = (HeightComparison)(obj["Comparison"]?.ToObject<int>() ?? 0),
                    Value = obj["Value"]?.ToObject<float>() ?? 0f
                };
            }
            else if (typeName == nameof(EquipmentCondition))
            {
                return new EquipmentCondition
                {
                    Slot = (EquipSlot)(obj["Slot"]?.ToObject<int>() ?? 0),
                    MustBeEquipped = obj["MustBeEquipped"]?.ToObject<bool>() ?? true
                };
            }
            
            return null;
        }
    }

    /// <summary>规则集 - 包含一组相关的规则</summary>
    public class RuleSet
    {
        public string Name { get; set; } = "Rule Set";
        public List<HeelsRule> Rules { get; set; } = new();
        
        /// <summary>是否使用 SimpleHeels</summary>
        public bool UseSimpleHeels { get; set; } = true;
        
        /// <summary>SimpleHeels 高度模式</summary>
        public SimpleHeelsHeightMode SimpleHeelsMode { get; set; } = SimpleHeelsHeightMode.Default;
        
        /// <summary>是否启用基准行动系统</summary>
        public bool UseBaselineActions { get; set; } = true;
        
        /// <summary>基准行动配置列表</summary>
        public List<BaselineActionConfig> BaselineConfigs { get; set; } = new();
        
        /// <summary>已忽略的新参数键（用户已 dismiss）</summary>
        public HashSet<string> DismissedNewParameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>基准行动主区块是否展开（UI 状态）。</summary>
        public bool IsBaselineSectionExpanded { get; set; } = true;

        /// <summary>基准行动各类型分组是否展开（UI 状态），键为 ActionType 名称。</summary>
        public Dictionary<string, bool> BaselineExpandedTypeGroups { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        
        public RuleSet()
        {
        }
        
        public RuleSet(string name)
        {
            Name = name;
        }
        
        /// <summary>深拷贝规则集</summary>
        public RuleSet Clone()
        {
            var clone = new RuleSet(Name + " (Copy)")
            {
                UseSimpleHeels = this.UseSimpleHeels,
                SimpleHeelsMode = this.SimpleHeelsMode,
                UseBaselineActions = this.UseBaselineActions,
                IsBaselineSectionExpanded = this.IsBaselineSectionExpanded,
                BaselineExpandedTypeGroups = new Dictionary<string, bool>(this.BaselineExpandedTypeGroups, StringComparer.OrdinalIgnoreCase)
            };
            
            // 深拷贝基准配置
            foreach (var config in BaselineConfigs)
            {
                var configJson = JsonConvert.SerializeObject(config);
                var configCopy = JsonConvert.DeserializeObject<BaselineActionConfig>(configJson);
                if (configCopy != null)
                    clone.BaselineConfigs.Add(configCopy);
            }
            
            // 拷贝已忽略列表
            clone.DismissedNewParameters = new HashSet<string>(DismissedNewParameters, StringComparer.OrdinalIgnoreCase);
            
            foreach (var rule in Rules)
            {
                // 简单的序列化/反序列化来深拷贝
                var json = JsonConvert.SerializeObject(rule);
                var ruleCopy = JsonConvert.DeserializeObject<HeelsRule>(json);
                if (ruleCopy != null)
                    clone.Rules.Add(ruleCopy);
            }
            return clone;
        }
    }
    
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        /// <summary>规则集列表</summary>
        public List<RuleSet> RuleSets { get; set; } = new();
        
        /// <summary>当前激活的规则集索引</summary>
        public int ActiveRuleSetIndex { get; set; } = 0;

        /// <summary>旧版统一规则列表，仅用于迁移。</summary>
        [Obsolete("Use RuleSets instead")]
        public List<HeelsRule> Rules { get; set; } = new();

        /// <summary>旧版 Glamourer 模式专用规则列表，仅用于迁移。</summary>
        [Obsolete("Migrated to RuleSets")]
        public List<HeelsRule> GlamourerRules { get; set; } = new();

        /// <summary>旧版 Penumbra 模式专用规则列表，仅用于迁移。</summary>
        [Obsolete("Migrated to RuleSets")]
        public List<HeelsRule> PenumbraRules { get; set; } = new();

        public int DecimalPrecision { get; set; } = 4;
        
        /// <summary>两次自动应用之间的最短间隔（秒），0 表示不限制。</summary>
        public float ApplyCooldownSeconds { get; set; } = 1f;

        /// <summary>规则持续匹配多久后才开始执行行动（秒），0 表示不等待。</summary>
        public float RuleMatchStableSeconds { get; set; } = 0.5f;

        /// <summary>界面语言；System 跟随系统 UI 语言。</summary>
        public UiLanguagePreference UiLanguage { get; set; } = UiLanguagePreference.System;

        /// <summary>规则匹配使用的 SimpleHeels 高度来源。</summary>
        public SimpleHeelsHeightMode SimpleHeelsHeightMode { get; set; } = SimpleHeelsHeightMode.Default;

        /// <summary>废弃字段：手动模式已移除，保留仅用于向后兼容。</summary>
        [Obsolete("Manual mode has been removed; SimpleHeels configuration moved to RuleSet level")]
        public float ManualHeight { get; set; } = 0.0f;

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
        /// <summary>行动类型（Glamourer / Penumbra / Honorific / Moodles）。</summary>
        public ActionType Type { get; set; } = ActionType.Glamourer;

        /// <summary>Glamourer: Design GUID。</summary>
        public string GlamourerDesign { get; set; } = "";

        /// <summary>Penumbra: 操作类型（设置选项 / 启用 Mod / 禁用 Mod）。</summary>
        public PenumbraActionKind PenumbraActionKind { get; set; } = PenumbraActionKind.SetModOption;
        /// <summary>Penumbra: Collection 名称。</summary>
        public string PenumbraCollection { get; set; } = "Default";
        /// <summary>Penumbra: Mod 名称。</summary>
        public string PenumbraModName { get; set; } = "";
        /// <summary>Penumbra: Option 名称（旧版，仅用于迁移）。</summary>
        public string PenumbraOption { get; set; } = "";
        /// <summary>Penumbra: Option 名称。</summary>
        public string PenumbraOptionName { get; set; } = "";
        /// <summary>Penumbra: Option 是否启用。</summary>
        public bool PenumbraOptionEnabled { get; set; } = true;
        /// <summary>Penumbra: 多选开关组：子选项名 → 匹配规则时是否开启。</summary>
        public Dictionary<string, bool> PenumbraMultiToggleStates { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Customize+: 配置名称。</summary>
        public string CustomizePlusProfile { get; set; } = "";

        /// <summary>可选：Honorific 称号（JSON）。</summary>
        public string HonorificTitleJson { get; set; } = "";

        /// <summary>可选：Moodles GUID。</summary>
        public string MoodleGuid { get; set; } = "";
        /// <summary>可选：Moodles 是否为预设。</summary>
        public bool MoodleIsPreset { get; set; }

        /// <summary>规则 UI 中是否折叠该行动详情。</summary>
        public bool IsActionCollapsed { get; set; }
    }

    public class HeelsRule
    {
        /// <summary>规则的自定义名称（用户可编辑）。</summary>
        public string Name { get; set; } = "";
        
        /// <summary>规则是否折叠（UI状态）。</summary>
        public bool IsCollapsed { get; set; } = false;
        
        public RuleBranchKind BranchKind { get; set; } = RuleBranchKind.ElseIf;
        
        /// <summary>新版条件组（支持多条件和 AND/OR），支持多个条件组。</summary>
        public List<ConditionGroup> ConditionGroups { get; set; } = new();
        
        /// <summary>旧版单条件组（仅用于迁移）。</summary>
        [Obsolete("Use ConditionGroups instead")]
        public ConditionGroup? ConditionGroup { get; set; }
        
        /// <summary>旧版：高度比较方式（仅用于迁移）。</summary>
        public HeightComparison HeightComparison { get; set; } = HeightComparison.LessThanOrEqual;
        /// <summary>旧版：高度阈值（仅用于迁移）。</summary>
        public float HeightValue { get; set; }

        /// <summary>旧版区间配置，仅用于迁移。</summary>
        public float MinHeight { get; set; } = 0.01f;
        public float MaxHeight { get; set; } = 99.0f;

        public List<HeelsRuleAction> Actions { get; set; } = new();

        /// <summary>旧版单行动配置，仅用于迁移。</summary>
        public string GlamourerDesign { get; set; } = "";
        public string PenumbraOptionName { get; set; } = "";
        public bool PenumbraOptionEnabled { get; set; } = true;

        /// <summary>旧版：按规则应用 Honorific 称号（已迁移到 Action 级别）。</summary>
        public bool EnableHonorific { get; set; }
        public string HonorificTitleJson { get; set; } = "";

        /// <summary>旧版：按规则应用 Moodles 状态或预设（已迁移到 Action 级别）。</summary>
        public bool EnableMoodles { get; set; }
        public string MoodleGuid { get; set; } = "";
        public bool MoodleIsPreset { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        /// <summary>行动列表是否折叠显示（UI 状态）。</summary>
        public bool IsActionsCollapsed { get; set; }
    }

    public class Plugin : IDalamudPlugin
    {
        public string Name => "Heels Glamourer Linker (Universal IPC)";

        [PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
        [PluginService] public static IFramework Framework { get; private set; } = null!;
        [PluginService] public static IObjectTable ObjectTable { get; private set; } = null!;
        [PluginService] public static IClientState ClientState { get; private set; } = null!;
        [PluginService] public static ICondition Condition { get; private set; } = null!;
        [PluginService] public static IPluginLog PluginLog { get; private set; } = null!;
        [PluginService] public static IDataManager DataManager { get; private set; } = null!;

        private Configuration Configuration { get; set; }
        
        /// <summary>获取当前激活的规则列表</summary>
        private List<HeelsRule> ActiveRules => 
            Configuration.RuleSets.Count > 0 && Configuration.ActiveRuleSetIndex >= 0 && Configuration.ActiveRuleSetIndex < Configuration.RuleSets.Count
                ? Configuration.RuleSets[Configuration.ActiveRuleSetIndex].Rules
                : new List<HeelsRule>();

        private static readonly Vector4 RuleUnreachableWarningColor = new(1.0f, 0.85f, 0.2f, 1.0f);
        private static readonly Vector4 RulePenumbraConflictWarningColor = new(1.0f, 0.55f, 0.15f, 1.0f);
        
        private bool drawConfigUi = false;
        private readonly HashSet<string> lastAppliedActionKeys = new(StringComparer.Ordinal);
        private string lastAppliedHonorificJson = "";
        private string lastAppliedMoodleKey = "";
        
        // 用于UI显示的"上次执行"信息（与去重机制分离）
        private int lastExecutedRuleIndex = -1;
        private DateTime? lastExecutedActionUtc = null;
        private readonly List<string> lastExecutedActionSummaries = new();
        private int lastMatchedRuleIndex = -1;
        private float currentHeelsHeight = 0f;
        private float currentHeelsDefaultHeight = 0f;
        private float currentHeelsActualHeight = 0f;
        private bool currentHeelsHasTempOffset = false;
        private int currentMatchedRuleIndex = -1; // 当前匹配的规则索引，-1 表示无匹配
        
        // 行动删除确认相关
        private int actionToDeleteRuleIndex = -1;
        private int actionToDeleteIndex = -1;
        
        // 条件拖拽排序相关
        private int? _conditionDragSourceRuleIndex;
        private int? _conditionDragSourceGroupIndex;
        private int? _conditionDragSourceIndex;
        private int? _conditionDragTargetIndex;
        
        // 条件删除相关
        private int conditionToDeleteRuleIndex = -1;
        private int conditionToDeleteGroupIndex = -1;
        private int conditionToDeleteIndex = -1;
        
        // 用于监听 DrawData 变化的缓存（装备外观变化检测）
        private readonly ushort[] lastDrawDataModelIds = new ushort[10]; // 10个装备槽位（头/身体/手/腿/脚/耳/项链/手镯/戒指x2）
        private bool drawDataInitialized = false;
        private int forceDrawDataCheckFrames = 0; // 强制检查剩余帧数

        private bool isGlamourerAvailable = false;
        private bool isSimpleHeelsAvailable = false;
        private bool isSimpleHeelsIpcReady = false;
        private bool isPenumbraIpcReady = false;
        private bool isMoodlesIpcReady = false;
        private bool isHonorificIpcReady = false;
        // private bool isCustomizePlusIpcReady = false;  // 已移除 Customize+ 支持

        private readonly PenumbraInterop _penumbraInterop;
        private readonly GlamourerInterop _glamourerInterop;
        private readonly MoodlesInterop _moodlesInterop;
        private readonly HonorificInterop _honorificInterop;
        // private readonly CustomizePlusInterop _customizePlusInterop;  // 已移除 Customize+ 支持
        
        private DateTime lastDependencyCheckUtc = DateTime.MinValue;
        private static readonly TimeSpan DependencyRecheckWhenMissing = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan DependencyRecheckWhenReady = TimeSpan.FromSeconds(30);

        /// <summary>本地玩家对象稳定就绪后，再等待一段时间才自动 apply，避免 &lt;me&gt; 未解析。</summary>
        private static readonly TimeSpan AutoApplyStartupDelay = TimeSpan.FromSeconds(3);
        /// <summary>登录保护期内主手锚点稳定时间，满足后才结束保护并允许规则匹配。</summary>
        private static readonly TimeSpan LoginMainHandStableDelay = TimeSpan.FromSeconds(2);
        /// <summary>登录保护期内最短等待，期间不进行任何规则匹配与 apply。</summary>
        private static readonly TimeSpan MinPostLoginProtectionDelay = TimeSpan.FromSeconds(5);
        /// <summary>登录保护最长持续时间，超时后自动结束以免拖慢正常游戏。</summary>
        private static readonly TimeSpan LoginProtectionMaxDuration = TimeSpan.FromSeconds(45);
        /// <summary>登录保护结束后，再延迟一段时间才允许基准行动（避免紧接 revert 把刚加载的投影剥掉）。</summary>
        private static readonly TimeSpan PostLoginBaselineDelay = TimeSpan.FromSeconds(5);
        /// <summary>收到 Glamourer 本地投影信号后，再稳定一段时间才允许规则评估。</summary>
        private static readonly TimeSpan GlamourerEquipmentSettleDelay = TimeSpan.FromSeconds(2);
        /// <summary>若 Glamourer 始终未发出本地投影信号，超过此时间后允许装备条件评估（兜底）。</summary>
        private static readonly TimeSpan GlamourerEquipmentFallback = TimeSpan.FromSeconds(25);
        /// <summary>登录后延迟多久才允许基准 Glamourer revert（与规则 Glamourer apply 无关）。</summary>
        private static readonly TimeSpan BaselineGlamourerRevertDelay = TimeSpan.FromSeconds(90);
        private DateTime? loginSinceUtc;
        private bool isLoginProtectionActive = true;
        private DateTime? localPlayerStableSinceUtc;
        private DateTime? appearancePopulatedSinceUtc;
        private DateTime? lastLocalGlamourerFinalizeUtc;
        private uint lastTrackedMainHandItemId;
        private bool mainHandAnchorInitialized;
        private DateTime? baselineActionsAllowedAfterUtc;
        private DateTime lastApplyUtc = DateTime.MinValue;
        private string applyGateStatus = "";
        
        // 调试信息
        private string lastError = "";
        private string lastIpcData = "";
        private string lastIpcDataHash = ""; // 用于检测 IPC 数据变化
        private string penumbraModSearchFilter = "";
        private string feetWarningDismissedSignature = "";
        private string lastFeetWarningSignature = "";
        private int? _ruleDragSourceIndex;
        private int? _ruleDragTargetIndex;
        private bool restoreDefaultsPending;
        private bool wasSettingsTabActive;
        private const string KoFiUrl = "https://ko-fi.com/kokatakyodai";
        private const int ConfigSchemaVersion = 13;
        private int stableTrackingRuleIndex = -1;
        private DateTime? ruleMatchStableSinceUtc;
        private const float DefaultWindowWidth = 600f;
        private const float DefaultWindowHeight = 450f;
        
        // 双击输入滑条的状态跟踪
        private string? _activeInputId;
        private string _inputBuffer = "";
        private bool _shouldFocusInput;
        
        /// <summary>
        /// 从旧插件名称（HeelsToggle）迁移配置文件到新名称（HeelsDesignLinker）
        /// </summary>
        private void MigrateOldConfigIfNeeded()
        {
            try
            {
                // Dalamud 配置文件保存在 pluginConfigs 目录，文件名为 [InternalName].json
                // 例如：%AppData%\XIVLauncherCN\pluginConfigs\HeelsToggle.json
                
                // 获取 pluginConfigs 目录（父级的父级目录）
                var currentConfigDir = PluginInterface.GetPluginConfigDirectory();
                var pluginsParentDir = Directory.GetParent(currentConfigDir)?.FullName;
                if (string.IsNullOrEmpty(pluginsParentDir))
                {
                    PluginLog.Warning("[HeelsDesignLinker][ConfigMigration] Cannot determine plugins parent directory.");
                    return;
                }
                
                var pluginConfigsDir = Path.Combine(Directory.GetParent(pluginsParentDir)?.FullName ?? "", "pluginConfigs");
                if (!Directory.Exists(pluginConfigsDir))
                    return;
                
                var oldConfigPath = Path.Combine(pluginConfigsDir, "HeelsToggle.json");
                var newConfigPath = Path.Combine(pluginConfigsDir, "HeelsDesignLinker.json");
                
                // 如果新配置已存在，不需要迁移
                if (File.Exists(newConfigPath))
                    return;
                
                // 如果旧配置存在，进行迁移
                if (File.Exists(oldConfigPath))
                {
                    // 复制旧配置到新位置
                    File.Copy(oldConfigPath, newConfigPath, overwrite: false);
                    
                    // 备份旧配置（而不是删除）
                    var backupPath = oldConfigPath + ".migrated_backup";
                    if (!File.Exists(backupPath))
                        File.Move(oldConfigPath, backupPath);
                    else
                        File.Delete(oldConfigPath);
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error($"[HeelsDesignLinker] Config migration failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 迁移单条件组到多条件组列表（支持多条件组）
        /// </summary>
        private void MigrateToRuleSetsIfNeeded()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            // 如果已经有 RuleSets，不需要迁移
            if (Configuration.RuleSets.Count > 0)
                return;
                
            // 如果有旧的 Rules 列表，迁移到第一个 RuleSet
            if (Configuration.Rules != null && Configuration.Rules.Count > 0)
            {
                var defaultRuleSet = new RuleSet(Localization.RuleSetDefaultName)
                {
                    UseSimpleHeels = Configuration.SimpleHeelsHeightMode != SimpleHeelsHeightMode.Manual,
                    SimpleHeelsMode = Configuration.SimpleHeelsHeightMode == SimpleHeelsHeightMode.Manual 
                        ? SimpleHeelsHeightMode.Default 
                        : Configuration.SimpleHeelsHeightMode
                };
                defaultRuleSet.Rules = Configuration.Rules;
                Configuration.RuleSets.Add(defaultRuleSet);
                Configuration.ActiveRuleSetIndex = 0;
                
                // 清空旧列表（但保留以支持降级）
                // Configuration.Rules = new List<HeelsRule>();
            }
#pragma warning restore CS0618
        }
        
        private void MigrateConditionGroupsIfNeeded()
        {
            bool needsSave = false;
            
            // 遍历所有 RuleSet 中的规则
            foreach (var ruleSet in Configuration.RuleSets)
            {
                foreach (var rule in ruleSet.Rules)
                {
#pragma warning disable CS0618 // Type or member is obsolete
                    // 如果有旧的单个 ConditionGroup 但 ConditionGroups 为空，进行迁移
                    if (rule.ConditionGroup != null && (rule.ConditionGroups == null || rule.ConditionGroups.Count == 0))
                    {
                        rule.ConditionGroups = new List<ConditionGroup> { rule.ConditionGroup };
                        rule.ConditionGroup = null;  // 清空旧字段
                        needsSave = true;
                    }
#pragma warning restore CS0618
                }
            }
            
            if (needsSave)
                SaveConfig();
        }

        /// <summary>
        /// 将误写入旧 Configuration.Rules 的行动合并回当前 RuleSet（UI 曾读写废弃列表导致不同步）。
        /// </summary>
        private void SyncLegacyRuleActionsToActiveRuleSet()
        {
            if (Configuration.RuleSets.Count == 0)
                return;

            var activeRules = ActiveRules;
#pragma warning disable CS0618
            var legacyRules = Configuration.Rules;
#pragma warning restore CS0618
            if (legacyRules == null || legacyRules.Count == 0 || ReferenceEquals(activeRules, legacyRules))
                return;

            var recovered = 0;
            var ruleCount = Math.Min(activeRules.Count, legacyRules.Count);
            for (var ruleIndex = 0; ruleIndex < ruleCount; ruleIndex++)
            {
                var activeRule = activeRules[ruleIndex];
                var legacyRule = legacyRules[ruleIndex];
                if (legacyRule.Actions == null || legacyRule.Actions.Count <= activeRule.Actions.Count)
                    continue;

                for (var actionIndex = activeRule.Actions.Count; actionIndex < legacyRule.Actions.Count; actionIndex++)
                {
                    var json = JsonConvert.SerializeObject(legacyRule.Actions[actionIndex]);
                    var copy = JsonConvert.DeserializeObject<HeelsRuleAction>(json);
                    if (copy == null)
                        continue;

                    activeRule.Actions.Add(copy);
                    recovered++;
                }
            }

            if (recovered > 0)
                SaveConfig();
        }

        public Plugin()
        {
            // 自动迁移旧配置文件（从 HeelsToggle.json 到 HeelsDesignLinker.json）
            MigrateOldConfigIfNeeded();
            
            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            
            // 迁移到 RuleSet 系统
            MigrateToRuleSetsIfNeeded();
            
            // 迁移单条件组到多条件组列表
            MigrateConditionGroupsIfNeeded();
            
            // 修复曾写入废弃 Rules 列表的行动
            SyncLegacyRuleActionsToActiveRuleSet();
            
            _penumbraInterop = new PenumbraInterop(PluginInterface);
            _glamourerInterop = new GlamourerInterop(PluginInterface, ObjectTable);
            _moodlesInterop = new MoodlesInterop(PluginInterface);
            _honorificInterop = new HonorificInterop(PluginInterface);
            // _customizePlusInterop = new CustomizePlusInterop(PluginInterface);  // 已移除 Customize+ 支持
            
            // 订阅 Glamourer 状态变化事件（主要检测方式）
            _glamourerInterop.OnStateChanged += OnGlamourerStateChanged;
            _glamourerInterop.OnStateFinalized += OnGlamourerStateFinalized;

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
                    Configuration.RuleMatchStableSeconds = 0.5f;
                if (Configuration.Version < 11)
                    MigrateToUnifiedRulesWithActionTypes();
                if (Configuration.Version < 12)
                    MigrateToConditionSystem();
                if (Configuration.Version < 13)
                    MigrateBaselineModeV2();
                Configuration.Version = ConfigSchemaVersion;
                PluginInterface.SavePluginConfig(Configuration);
            }

            Localization.SetLanguagePreference(Configuration.UiLanguage);
            
            // 确保至少有一个规则集
            if (Configuration.RuleSets.Count == 0)
            {
                Configuration.RuleSets.Add(CreateNewRuleSet("Default"));
                Configuration.ActiveRuleSetIndex = 0;
                SaveConfig();
            }
            
            // 确保 ActiveRuleSetIndex 有效
            if (Configuration.ActiveRuleSetIndex < 0 || Configuration.ActiveRuleSetIndex >= Configuration.RuleSets.Count)
            {
                Configuration.ActiveRuleSetIndex = 0;
                SaveConfig();
            }
            
            CommandManager.AddHandler("/hdl", new CommandInfo(OnCommand) { HelpMessage = Localization.CommandHelp });
            CommandManager.AddHandler("/heelsdesign", new CommandInfo(OnCommand) { HelpMessage = Localization.CommandHelp });
            
            RefreshDependencies(force: true);
            Framework.Update += OnFrameworkUpdate;
            ClientState.Login += OnLogin;
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
            // _customizePlusInterop.RefreshProfileList(force: true);  // 已移除 Customize+ 支持
        }

        private static bool ActionUsesGlamourer(HeelsRuleAction action) =>
            !string.IsNullOrWhiteSpace(action.GlamourerDesign);

        private bool ActionUsesPenumbra(HeelsRuleAction action)
        {
            if (string.IsNullOrWhiteSpace(action.PenumbraCollection)
                || string.IsNullOrWhiteSpace(action.PenumbraModName))
            {
                return false;
            }

            // 如果是启用/禁用 Mod 操作，不需要检查 Option
            if (action.PenumbraActionKind == PenumbraActionKind.EnableMod 
                || action.PenumbraActionKind == PenumbraActionKind.DisableMod)
            {
                return true;
            }

            // 如果是设置 Mod 选项操作，需要检查 Option
            if (string.IsNullOrWhiteSpace(action.PenumbraOption))
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

        private static HeelsRuleAction CreateDefaultRuleActionForType(ActionType actionType) =>
            actionType switch
            {
                ActionType.Glamourer => new HeelsRuleAction { Type = ActionType.Glamourer },
                ActionType.Penumbra => new HeelsRuleAction { Type = ActionType.Penumbra, PenumbraCollection = "Default" },
                // ActionType.CustomizePlus => new HeelsRuleAction { Type = ActionType.CustomizePlus },  // 已移除
                _ => new HeelsRuleAction { Type = ActionType.Glamourer },
            };

        private static HeelsRule CreateEmptyRule() => new()
        {
            Name = "",
            IsCollapsed = false,
            IsActive = true,
            Actions = [CreateDefaultRuleActionForType(ActionType.Glamourer)],
        };

        private static RuleSet CreateNewRuleSet(string name) =>
            new(name)
            {
                Rules = CreateDefaultRuleSet(),
            };

        private static List<HeelsRule> CreateDefaultRuleSet() =>
        [
            new HeelsRule
            {
                ConditionGroups =
                [
                    new ConditionGroup
                    {
                        Operator = LogicOperator.And,
                        Conditions =
                        [
                            new HeightCondition
                            {
                                Comparison = HeightComparison.LessThanOrEqual,
                                Value = -0.01f
                            }
                        ]
                    }
                ],
                Actions =
                [
                    new HeelsRuleAction { Type = ActionType.Glamourer, GlamourerDesign = "SFX_Barefoot" },
                ],
                IsActive = true,
            },
            new HeelsRule
            {
                ConditionGroups =
                [
                    new ConditionGroup
                    {
                        Operator = LogicOperator.And,
                        Conditions =
                        [
                            new HeightCondition
                            {
                                Comparison = HeightComparison.LessThanOrEqual,
                                Value = 0.03f
                            }
                        ]
                    }
                ],
                Actions =
                [
                    new HeelsRuleAction { Type = ActionType.Glamourer, GlamourerDesign = "SFX_Shoes" },
                ],
                IsActive = true,
            },
            new HeelsRule
            {
                BranchKind = RuleBranchKind.Else,
                ConditionGroups = [],
                Actions =
                [
                    new HeelsRuleAction { Type = ActionType.Glamourer, GlamourerDesign = "SFX_Heels" },
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
            var rules = ActiveRules;
            if (ruleIndex < 0 || ruleIndex >= rules.Count)
                return;

            var rule = rules[ruleIndex];
            if (rule.Actions is { Count: > 0 })
                return;

            rule.Actions =
            [
                new HeelsRuleAction
                {
                    Type = ActionType.Glamourer,
                    GlamourerDesign = rule.GlamourerDesign ?? "",
                    PenumbraOptionName = rule.PenumbraOptionName ?? "",
                    PenumbraOptionEnabled = rule.PenumbraOptionEnabled,
                },
            ];
            SaveConfig();
        }

        private static HeelsRuleAction CreateDefaultRuleAction() =>
            CreateDefaultRuleActionForType(ActionType.Glamourer);

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

            // 清除Honorific（如果新规则不需要）
            var hadHonorific = !string.IsNullOrEmpty(lastAppliedHonorificJson);
            var wantsHonorific = newRule != null && RuleUsesHonorific(newRule);
            if (hadHonorific
                && !wantsHonorific
                && localPlayer != null
                && localPlayer.IsValid())
            {
                _honorificInterop.TryClearLocalTitle(localPlayer, out _);
            }

            // 注意：lastAppliedActionKeys 已经在 UpdateRuleMatchStability 中清除了
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
            GetRuleActions(rule).Any(action => 
                action.Type == ActionType.Honorific || 
                !string.IsNullOrWhiteSpace(action.HonorificTitleJson));

        private bool RuleUsesMoodles(HeelsRule rule) =>
            GetRuleActions(rule).Any(action => 
                action.Type == ActionType.Moodles ||
                (Guid.TryParse(action.MoodleGuid, out var id) && id != Guid.Empty));

        private bool ConfigurationUsesGlamourer() =>
            Configuration.RuleSets.Any(rs => rs.Rules.Any(r => r.IsActive && RuleUsesGlamourer(r)));

        private bool ConfigurationUsesPenumbra() =>
            Configuration.RuleSets.Any(rs => rs.Rules.Any(r => r.IsActive && RuleUsesPenumbra(r)));

        private bool ConfigurationUsesHonorific() =>
            Configuration.RuleSets.Any(rs => rs.Rules.Any(r => r.IsActive && RuleUsesHonorific(r)));

        private bool ConfigurationUsesMoodles() =>
            Configuration.RuleSets.Any(rs => rs.Rules.Any(r => r.IsActive && RuleUsesMoodles(r)));

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
            // 检查活动 RuleSet 是否需要 SimpleHeels
            if (Configuration.RuleSets.Count > 0 && 
                Configuration.ActiveRuleSetIndex >= 0 && 
                Configuration.ActiveRuleSetIndex < Configuration.RuleSets.Count)
            {
                var activeRuleSet = Configuration.RuleSets[Configuration.ActiveRuleSetIndex];
                bool requiresSimpleHeels = activeRuleSet.UseSimpleHeels;
                
                if (requiresSimpleHeels && (!isSimpleHeelsAvailable || !isSimpleHeelsIpcReady))
                    return false;
            }

            if (!HasConfiguredOutput())
                return false;

            // 只要有任何一个配置的输出插件可用，就允许工作
            // 不再要求所有配置的插件都必须可用
            bool hasAnyOutputReady = false;
            
            if (ConfigurationUsesGlamourer() && isGlamourerAvailable)
                hasAnyOutputReady = true;

            if (ConfigurationUsesPenumbra() && isPenumbraIpcReady)
                hasAnyOutputReady = true;

            if (ConfigurationUsesMoodles() && isMoodlesIpcReady)
                hasAnyOutputReady = true;

            if (ConfigurationUsesHonorific() && isHonorificIpcReady)
                hasAnyOutputReady = true;

            return hasAnyOutputReady;
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
                catch (Exception)
                {
                }
            }

            isPenumbraIpcReady = _penumbraInterop.IsIpcAvailable();
            isMoodlesIpcReady = _moodlesInterop.IsIpcAvailable();
            isHonorificIpcReady = _honorificInterop.IsIpcAvailable();
            // isCustomizePlusIpcReady = _customizePlusInterop.IsIpcAvailable();  // 已移除 Customize+ 支持

            var nowReady = IsReadyForWork();
            if (nowReady && !wasReady)
            {
            }
            else if (!nowReady && wasReady)
            {
            }
        }

        private void OnLogin()
        {
            loginSinceUtc = DateTime.UtcNow;
            isLoginProtectionActive = true;
            _glamourerInterop.ResetLoginProjectionTracking();
            ResetApplyState();
            drawDataInitialized = false;
        }

        private bool IsLoginProtectionActive()
        {
            if (!isLoginProtectionActive)
                return false;

            if (!loginSinceUtc.HasValue
                || DateTime.UtcNow - loginSinceUtc.Value >= LoginProtectionMaxDuration)
            {
                EndLoginProtection();
                return false;
            }

            return true;
        }

        private void EndLoginProtection()
        {
            if (!isLoginProtectionActive)
                return;

            isLoginProtectionActive = false;
            baselineActionsAllowedAfterUtc = DateTime.UtcNow + PostLoginBaselineDelay;
            ResetAppearanceReadyTracking();
        }

        private bool IsBaselineApplyAllowed()
        {
            if (IsLoginProtectionActive())
                return false;

            return !baselineActionsAllowedAfterUtc.HasValue
                   || DateTime.UtcNow >= baselineActionsAllowedAfterUtc.Value;
        }

        private bool IsBaselineGlamourerRevertAllowed()
        {
            if (!loginSinceUtc.HasValue)
                return true;

            return DateTime.UtcNow - loginSinceUtc.Value >= BaselineGlamourerRevertDelay;
        }

        private void OnGlamourerStateFinalized(nint actorAddress)
        {
            var localPlayer = ObjectTable.LocalPlayer;
            if (localPlayer == null || !localPlayer.IsValid() || localPlayer.Address != actorAddress)
                return;

            lastLocalGlamourerFinalizeUtc = DateTime.UtcNow;
        }

        private bool IsPlayerInWorld()
        {
            if (Condition[ConditionFlag.BetweenAreas] || Condition[ConditionFlag.BetweenAreas51])
                return false;

            return true;
        }

        private void OnLogout(int type, int code)
        {
            _glamourerInterop.ResetLoginProjectionTracking();
            ResetApplyState();
            drawDataInitialized = false;
        }
        
        private void OnGlamourerStateChanged()
        {
            if (IsLoginProtectionActive())
                return;

            try
            {
                Array.Clear(lastDrawDataModelIds, 0, lastDrawDataModelIds.Length);
                drawDataInitialized = false;
                forceDrawDataCheckFrames = 30;
                stableTrackingRuleIndex = -1;
                ruleMatchStableSinceUtc = null;
            }
            catch (Exception ex)
            {
                PluginLog.Warning($"Error handling Glamourer state change: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查 DrawData 是否变化（监听装备外观变化）
        /// </summary>
        private unsafe bool CheckDrawDataChanged(out bool modelIdsChanged)
        {
            modelIdsChanged = false;

            try
            {
                var localPlayer = ObjectTable?.LocalPlayer;
                if (localPlayer == null)
                {
                    drawDataInitialized = false;
                    forceDrawDataCheckFrames = 0;
                    return false;
                }
                
                var character = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)localPlayer.Address;
                if (character == null)
                {
                    drawDataInitialized = false;
                    forceDrawDataCheckFrames = 0;
                    return false;
                }
                
                var changed = false;
                
                // 检查10个装备槽位的 Model ID（头部、身体、手、腿、脚、耳环、项链、手镯、戒指x2）
                for (int i = 0; i < 10; i++)
                {
                    var modelId = character->DrawData.EquipmentModelIds[i].Id;
                    
                    if (!drawDataInitialized)
                    {
                        // 首次初始化，不算变化
                        lastDrawDataModelIds[i] = modelId;
                    }
                    else if (lastDrawDataModelIds[i] != modelId)
                    {
                        // 检测到变化
                        changed = true;
                        lastDrawDataModelIds[i] = modelId;
                    }
                }
                
                drawDataInitialized = true;
                modelIdsChanged = changed;
                
                // 如果有强制检查标志，触发规则重评估，但不视为外观 Model 变化
                if (forceDrawDataCheckFrames > 0)
                {
                    forceDrawDataCheckFrames--;
                    return true;
                }
                
                return changed;
            }
            catch (Exception ex)
            {
                PluginLog.Warning($"CheckDrawDataChanged error: {ex.Message}");
                drawDataInitialized = false;
                forceDrawDataCheckFrames = 0;
                return false;
            }
        }

        private void ResetApplyState()
        {
            localPlayerStableSinceUtc = null;
            appearancePopulatedSinceUtc = null;
            lastLocalGlamourerFinalizeUtc = null;
            baselineActionsAllowedAfterUtc = null;
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

            // 规则改变时，立即清除已应用的action keys
            // 这样等稳定性延迟过后，action会重新执行
            if (stableTrackingRuleIndex >= 0)  // 如果之前有匹配的规则
            {
                lastAppliedActionKeys.Clear();
                lastAppliedHonorificJson = "";
                lastAppliedMoodleKey = "";
            }

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

        private void ResetAppearanceReadyTracking()
        {
            appearancePopulatedSinceUtc = null;
            mainHandAnchorInitialized = false;
        }

        /// <summary>主手槽在登录后几乎恒定有装备；皇帝拳套 13775 不在皇帝套列表，仍视为有效锚点。</summary>
        private static bool IsMainHandAppearanceAnchor(uint itemId) => itemId != 0;

        private unsafe bool TryGetMainHandEquippedItemId(out uint itemId)
        {
            itemId = 0;

            var inventoryManager = InventoryManager.Instance();
            if (inventoryManager == null)
                return false;

            var container = inventoryManager->GetInventoryContainer(InventoryType.EquippedItems);
            if (container == null)
                return false;

            var item = container->GetInventorySlot(0);
            if (item == null)
                return false;

            itemId = item->ItemId;
            return true;
        }

        /// <summary>主手背包槽是否已稳定，作为登录外观锚点。</summary>
        private bool IsMainHandAnchorReady(TimeSpan stableDelay, out string status)
        {
            status = "";

            if (!IsLoginProtectionActive())
                return true;

            if (!IsLocalPlayerReady())
            {
                ResetAppearanceReadyTracking();
                status = Localization.IsChine ? "等待本地角色对象" : "Waiting for local player object";
                return false;
            }

            if (!TryGetMainHandEquippedItemId(out var mainHandItemId))
            {
                ResetAppearanceReadyTracking();
                status = Localization.IsChine ? "等待主手装备数据" : "Waiting for main-hand equipment data";
                return false;
            }

            if (!IsMainHandAppearanceAnchor(mainHandItemId))
            {
                ResetAppearanceReadyTracking();
                status = Localization.IsChine ? "等待主手装备加载" : "Waiting for main-hand equipment";
                return false;
            }

            if (mainHandAnchorInitialized && mainHandItemId != lastTrackedMainHandItemId)
                ResetAppearanceReadyTracking();

            lastTrackedMainHandItemId = mainHandItemId;
            mainHandAnchorInitialized = true;

            appearancePopulatedSinceUtc ??= DateTime.UtcNow;
            var stableElapsed = DateTime.UtcNow - appearancePopulatedSinceUtc.Value;
            if (stableElapsed < stableDelay)
            {
                var remaining = stableDelay - stableElapsed;
                status = Localization.IsChine
                    ? $"主手装备稳定中 {remaining.TotalSeconds:F1}s"
                    : $"Main-hand anchor stabilizing {remaining.TotalSeconds:F1}s";
                return false;
            }

            return true;
        }

        private bool ConfigurationUsesEquipmentConditions()
        {
            foreach (var rule in ActiveRules)
            {
                if (!rule.IsActive || rule.ConditionGroups == null)
                    continue;

                foreach (var group in rule.ConditionGroups)
                {
                    if (group.Conditions.Any(c => c is EquipmentCondition))
                        return true;
                }
            }

            return false;
        }

        private bool PassesSessionPreamble(out string gateStatus, TimeSpan minPostLoginDelay)
        {
            gateStatus = "";

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

            if (!IsPlayerInWorld())
            {
                gateStatus = Localization.IsChine ? "过图/加载中" : "Between areas / loading";
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

            if (loginSinceUtc.HasValue)
            {
                var loginElapsed = now - loginSinceUtc.Value;
                if (loginElapsed < minPostLoginDelay)
                {
                    var remaining = minPostLoginDelay - loginElapsed;
                    gateStatus = Localization.IsChine
                        ? $"登录预热 {remaining.TotalSeconds:F1}s"
                        : $"Login warmup {remaining.TotalSeconds:F1}s";
                    return false;
                }
            }

            return true;
        }

        private bool PassesGlamourerEquipmentGate(out string gateStatus)
        {
            gateStatus = "";

            if (!ConfigurationUsesEquipmentConditions() || !isGlamourerAvailable)
                return true;

            if (_glamourerInterop.IsEquipmentEvaluationAllowed(
                    loginSinceUtc,
                    GlamourerEquipmentSettleDelay,
                    GlamourerEquipmentFallback,
                    out var glamWait))
                return true;

            gateStatus = Localization.IsChine
                ? glamWait
                : glamWait switch
                {
                    var s when s.StartsWith("Glamourer 装备状态稳定中") =>
                        s.Replace("Glamourer 装备状态稳定中", "Glamourer equipment stabilizing"),
                    _ => "Waiting for Glamourer local projection",
                };
            return false;
        }

        /// <summary>登录保护结束条件：外观/Glamourer 就绪。满足前不进行任何规则匹配与 apply。</summary>
        private bool IsLoginSessionReadyToWork(out string gateStatus)
        {
            if (!PassesSessionPreamble(out gateStatus, MinPostLoginProtectionDelay))
                return false;

            if (!IsMainHandAnchorReady(LoginMainHandStableDelay, out gateStatus))
                return false;

            if (!PassesGlamourerEquipmentGate(out gateStatus))
                return false;

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
                loginSinceUtc = null;
                isLoginProtectionActive = false;
                _glamourerInterop.ResetLoginProjectionTracking();
                ResetApplyState();
                lastIpcDataHash = "";
                drawDataInitialized = false; // 登出时重置
                return;
            }

            if (!loginSinceUtc.HasValue)
            {
                loginSinceUtc = DateTime.UtcNow;
                isLoginProtectionActive = true;
                _glamourerInterop.ResetLoginProjectionTracking();
                ResetApplyState();
                drawDataInitialized = false;
            }
            
            // 检查 DrawData 是否变化（装备外观变化检测 - 用于 Glamourer）
            var drawDataChanged = CheckDrawDataChanged(out var drawModelIdsChanged);
            
            // 检查当前 RuleSet 是否使用 SimpleHeels
            bool useSimpleHeels = false;
            if (Configuration.RuleSets.Count > 0 && 
                Configuration.ActiveRuleSetIndex >= 0 && 
                Configuration.ActiveRuleSetIndex < Configuration.RuleSets.Count)
            {
                useSimpleHeels = Configuration.RuleSets[Configuration.ActiveRuleSetIndex].UseSimpleHeels;
            }
            
            string currentIpcData = "";
            
            if (!useSimpleHeels || !isSimpleHeelsAvailable || !isSimpleHeelsIpcReady)
            {
                // 不使用 SimpleHeels 或 IPC 不可用：设置高度为 0
                currentHeelsDefaultHeight = 0f;
                currentHeelsActualHeight = 0f;
                currentHeelsHasTempOffset = false;
                currentHeelsHeight = 0f;
                lastError = "";
                
                if (!useSimpleHeels)
                {
                    currentIpcData = "Disabled";
                    lastIpcData = Localization.IsChine ? "未使用 SimpleHeels" : "SimpleHeels disabled";
                }
                else
                {
                    currentIpcData = "Unavailable";
                    lastIpcData = Localization.IsChine ? "SimpleHeels 不可用" : "SimpleHeels unavailable";
                }
            }
            else
            {
                // SimpleHeels IPC 模式
                try
                {
                    var getLocalPlayerProvider = PluginInterface.GetIpcSubscriber<string>("SimpleHeels.GetLocalPlayer");
                    var localPlayerData = getLocalPlayerProvider.InvokeFunc();
                    
                    currentIpcData = localPlayerData ?? "NULL";
                    lastIpcData = currentIpcData;
                    
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
            }
            
            // 检测 IPC 数据是否变化（用于高度变化）
            bool ipcDataChanged = false;
            if (currentIpcData != lastIpcDataHash)
            {
                ipcDataChanged = true;
                lastIpcDataHash = currentIpcData;
            }
            
            // 如果 IPC 变化或 DrawData 变化，重置规则匹配稳定性
            if (ipcDataChanged || drawDataChanged)
            {
                if (drawModelIdsChanged && IsLoginProtectionActive())
                    ResetAppearanceReadyTracking();

                stableTrackingRuleIndex = -1;
                ruleMatchStableSinceUtc = null;
            }

            if (IsLoginProtectionActive())
            {
                if (IsLoginSessionReadyToWork(out applyGateStatus))
                    EndLoginProtection();
                else
                    return;
            }

            applyGateStatus = "";
            if (!PassesSessionPreamble(out applyGateStatus, TimeSpan.Zero))
                return;

            HeelsRule? matchedRule = null;
            currentMatchedRuleIndex = -1;

            var activeRules = ActiveRules;
            for (int i = 0; i < activeRules.Count; i++)
            {
                var rule = activeRules[i];
                if (TryMatchRule(rule, i, currentHeelsHeight, allowEquipmentEvaluation: true))
                {
                    currentMatchedRuleIndex = i;
                    matchedRule = rule;
                    break;
                }
            }

            UpdateRuleMatchStability(currentMatchedRuleIndex);

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
            var ruleActions = GetRuleActions(matchedRule);

            // 应用基准行动（如果启用）
            if (Configuration.RuleSets.Count > 0 && 
                Configuration.ActiveRuleSetIndex >= 0 && 
                Configuration.ActiveRuleSetIndex < Configuration.RuleSets.Count)
            {
                var activeRuleSet = Configuration.RuleSets[Configuration.ActiveRuleSetIndex];
                if (activeRuleSet.UseBaselineActions)
                {
                    var newCount = UpdateBaselineConfigs(activeRuleSet);
                    if (newCount > 0)
                        PluginInterface.SavePluginConfig(Configuration);

                    if (IsBaselineApplyAllowed())
                        ApplyBaselineActions(activeRuleSet, ref appliedAnything);
                }
            }

            if (ruleActions.Count == 0)
                return;

            foreach (var action in ruleActions)
            {
                switch (action.Type)
                {
                    case ActionType.Glamourer:
                        ApplyGlamourerAction(action, ref appliedAnything);
                        break;
                    case ActionType.Penumbra:
                        if (ApplyPenumbraAction(action, ref appliedAnything))
                            configDirty = true;
                        break;
                    case ActionType.Honorific:
                        if (!isHonorificIpcReady)
                            break;

                        if (!string.IsNullOrWhiteSpace(action.HonorificTitleJson)
                            && action.HonorificTitleJson != lastAppliedHonorificJson)
                        {
                            if (_honorificInterop.TrySetLocalTitle(localPlayer, action.HonorificTitleJson, out var honorificError))
                            {
                                lastAppliedHonorificJson = action.HonorificTitleJson;
                                appliedAnything = true;
                                lastError = "";
                            }
                            else
                            {
                                lastError = honorificError;
                                PluginLog.Warning($"Honorific IPC apply failed: {honorificError}");
                            }
                        }
                        break;
                    case ActionType.Moodles:
                        if (!isMoodlesIpcReady)
                            break;

                        if (!string.IsNullOrWhiteSpace(action.MoodleGuid)
                            && Guid.TryParse(action.MoodleGuid, out var moodleId) && moodleId != Guid.Empty)
                        {
                            var moodleKey = $"{action.MoodleGuid}|{(action.MoodleIsPreset ? "preset" : "status")}";
                            if (moodleKey != lastAppliedMoodleKey)
                            {
                                if (_moodlesInterop.TryApply(localPlayer, moodleId, action.MoodleIsPreset, out var moodlesError))
                                {
                                    lastAppliedMoodleKey = moodleKey;
                                    appliedAnything = true;
                                    lastError = "";
                                }
                                else
                                {
                                    lastError = moodlesError;
                                    PluginLog.Warning($"Moodles IPC apply failed: {moodlesError}");
                                }
                            }
                        }
                        break;
                }
            }

            if (configDirty)
                SaveConfig();

            if (appliedAnything)
            {
                lastApplyUtc = DateTime.UtcNow;
                lastExecutedRuleIndex = currentMatchedRuleIndex;
                lastExecutedActionUtc = DateTime.UtcNow;
                RecordExecutedActionSummaries(ruleActions);
            }
        }

        private void RecordExecutedActionSummaries(List<HeelsRuleAction> ruleActions)
        {
            lastExecutedActionSummaries.Clear();
            foreach (var action in ruleActions)
            {
                switch (action.Type)
                {
                    case ActionType.Glamourer:
                        if (ActionUsesGlamourer(action))
                            lastExecutedActionSummaries.Add($"Glamourer: {action.GlamourerDesign}");
                        break;
                    case ActionType.Penumbra:
                        if (ActionUsesPenumbra(action))
                            lastExecutedActionSummaries.Add($"Penumbra: {action.PenumbraModName}");
                        break;
                    case ActionType.Honorific:
                        if (!string.IsNullOrWhiteSpace(action.HonorificTitleJson))
                            lastExecutedActionSummaries.Add("Honorific");
                        break;
                    case ActionType.Moodles:
                        if (!string.IsNullOrWhiteSpace(action.MoodleGuid))
                            lastExecutedActionSummaries.Add("Moodles");
                        break;
                }
            }
        }

        private void ApplyGlamourerAction(HeelsRuleAction action, ref bool appliedAnything)
        {
            if (!isGlamourerAvailable || !ActionUsesGlamourer(action))
                return;

            var applyKey = BuildGlamourerActionKey(action);
            if (lastAppliedActionKeys.Contains(applyKey))
                return;

            try
            {
                var designArg = FormatGlamourerDesignArgument(action.GlamourerDesign);
                CommandManager.ProcessCommand($"/glamour apply {designArg} | <me>");
                lastAppliedActionKeys.Add(applyKey);
                appliedAnything = true;
            }
            catch (Exception ex)
            {
                PluginLog.Error($"Failed to apply Glamourer design: {ex.Message}");
            }
        }

        private bool ApplyPenumbraAction(HeelsRuleAction action, ref bool appliedAnything)
        {
            if (!isPenumbraIpcReady || !ActionUsesPenumbra(action))
                return false;

            var configDirty = false;

            // 如果是启用/禁用 Mod 操作
            if (action.PenumbraActionKind == PenumbraActionKind.EnableMod 
                || action.PenumbraActionKind == PenumbraActionKind.DisableMod)
            {
                var enabled = action.PenumbraActionKind == PenumbraActionKind.EnableMod;
                var applyKey = $"P:SetMod:{action.PenumbraCollection}|{action.PenumbraModName}|{enabled}";

                if (!enabled && IsLoginProtectionActive())
                    return false;

                if (!lastAppliedActionKeys.Contains(applyKey))
                {
                    if (_penumbraInterop.TrySetModEnabled(
                            action.PenumbraCollection,
                            action.PenumbraModName,
                            enabled,
                            out var setModEc,
                            out var setModError))
                    {
                        if (setModEc == PenumbraIpcEc.Success || setModEc == PenumbraIpcEc.NothingChanged)
                        {
                            lastAppliedActionKeys.Add(applyKey);
                            appliedAnything = true;
                        }
                        else
                        {
                            lastError = setModError;
                            PluginLog.Warning($"[Action] Penumbra SetMod failed: [{action.PenumbraCollection}] {action.PenumbraModName} → Result: {setModEc}");
                        }
                    }
                    else
                    {
                        lastError = setModError;
                        PluginLog.Warning($"[Action] Penumbra SetMod IPC failed: {setModError}");
                    }
                }
                
                return configDirty;
            }

            // 否则是设置 Mod 选项操作
            var groupType = _penumbraInterop.GetOptionGroupType(
                action.PenumbraModName ?? "",
                action.PenumbraOption ?? "");

            if (PenumbraInterop.UsesBoolOptionValue(groupType))
            {
                var optionNames = _penumbraInterop.GetOptionNames(
                    action.PenumbraModName ?? "",
                    action.PenumbraOption ?? "");
                if (SyncPenumbraMultiToggleStates(action, optionNames))
                    configDirty = true;

                var enabledNames = action.PenumbraMultiToggleStates
                    .Where(pair => pair.Value)
                    .Select(pair => pair.Key.Trim())
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var applyKey =
                    $"P:{action.PenumbraCollection}|{action.PenumbraModName}|{action.PenumbraOption}|M:{string.Join(",", enabledNames)}";
                
                if (!lastAppliedActionKeys.Contains(applyKey))
                {
                    if (_penumbraInterop.TryApplyMultiToggleSettings(
                            action.PenumbraCollection,
                            action.PenumbraModName ?? "",
                            action.PenumbraOption ?? "",
                            enabledNames,
                            out var multiEc,
                            out var multiError))
                    {
                        lastAppliedActionKeys.Add(applyKey);
                        appliedAnything = true;
                    }
                    else
                    {
                        lastError = multiError;
                        PluginLog.Warning($"Penumbra MultiToggle IPC failed: {multiError}");
                    }
                }
            }
            else
            {
                var applyKey = BuildPenumbraActionKey(action);
                if (!lastAppliedActionKeys.Contains(applyKey))
                {
                    var penumbraEc = _penumbraInterop.TryApplyModSetting(
                            action.PenumbraCollection,
                            action.PenumbraModName ?? "",
                            action.PenumbraOption ?? "",
                            action.PenumbraOptionName,
                            action.PenumbraOptionEnabled,
                            out var singleEc,
                            out var singleError);
                    
                    if (penumbraEc && (singleEc == PenumbraIpcEc.Success || singleEc == PenumbraIpcEc.NothingChanged))
                    {
                        lastAppliedActionKeys.Add(applyKey);
                        appliedAnything = true;
                    }
                    else if (penumbraEc)
                    {
                        // IPC 调用成功但返回了错误码
                        lastError = $"Penumbra: {singleEc}";
                    }
                    else
                    {
                        // IPC 调用本身失败
                        lastError = $"Penumbra IPC 失败: {singleError}";
                        PluginLog.Warning($"Penumbra IPC call failed: {singleError}");
                    }
                }
            }

            return configDirty;
        }

        private void ApplyCustomizePlusAction(HeelsRuleAction action, IPlayerCharacter? localPlayer, ref bool appliedAnything)
        {
            // Customize+ 支持已移除：Customize+ 不提供所需的 IPC 方法
            // 如果配置中包含 CustomizePlus Action，将被跳过
            return;
        }

        private void ApplyGlamourerActionsForRule(HeelsRule rule, ref bool appliedAnything)
        {
            foreach (var action in GetRuleActions(rule))
            {
                ApplyGlamourerAction(action, ref appliedAnything);
            }
        }

        private bool ApplyPenumbraActionsForRule(HeelsRule rule, ref bool appliedAnything)
        {
            var configDirty = false;
            var penumbraActions = GetRuleActions(rule).Where(ActionUsesPenumbra).ToList();
            if (penumbraActions.Count == 0)
                return false;

            // 分离启用/禁用 Mod 操作和设置选项操作
            var modStateActions = penumbraActions
                .Where(a => a.PenumbraActionKind == PenumbraActionKind.EnableMod 
                         || a.PenumbraActionKind == PenumbraActionKind.DisableMod)
                .ToList();
            var modOptionActions = penumbraActions
                .Where(a => a.PenumbraActionKind == PenumbraActionKind.SetModOption)
                .ToList();

            // 先处理启用/禁用 Mod 操作（直接应用，不合并）
            foreach (var action in modStateActions)
            {
                if (ApplyPenumbraAction(action, ref appliedAnything))
                    configDirty = true;
            }

            // 再处理设置选项操作（需要合并同一组的选项）
            var mergedMultiToggles = new Dictionary<string, (HeelsRuleAction Action, Dictionary<string, bool> Toggles)>(
                StringComparer.OrdinalIgnoreCase);
            var singleSelectByGroup = new Dictionary<string, HeelsRuleAction>(StringComparer.OrdinalIgnoreCase);

            foreach (var action in modOptionActions)
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
                        action.PenumbraModName ?? "",
                        action.PenumbraOption ?? "",
                        enabledNames,
                        out var multiResult,
                        out var multiError))
                {
                    lastAppliedActionKeys.Add(applyKey);
                    appliedAnything = true;
                    lastError = "";
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
                        action.PenumbraModName ?? "",
                        action.PenumbraOption ?? "",
                        action.PenumbraOptionName,
                        action.PenumbraOptionEnabled,
                        out var singleResult,
                        out var singleError))
                {
                    if (singleResult == PenumbraIpcEc.Success || singleResult == PenumbraIpcEc.NothingChanged)
                    {
                        lastAppliedActionKeys.Add(applyKey);
                        appliedAnything = true;
                        lastError = "";
                    }
                    else
                    {
                        lastError = $"Penumbra error: {singleResult}";
                        PluginLog.Warning($"Penumbra IPC apply failed: result={singleResult}, error={singleError}");
                    }
                }
                else
                {
                    lastError = $"Penumbra IPC call failed: {singleError}";
                    PluginLog.Warning($"Penumbra IPC call failed: {singleError}");
                }
            }

            return configDirty;
        }

        private float SelectHeelsHeightForMode(float defaultHeight, float actualHeight)
        {
            // 使用活动 RuleSet 的 SimpleHeels 配置
            if (Configuration.RuleSets.Count > 0 && 
                Configuration.ActiveRuleSetIndex >= 0 && 
                Configuration.ActiveRuleSetIndex < Configuration.RuleSets.Count)
            {
                var activeRuleSet = Configuration.RuleSets[Configuration.ActiveRuleSetIndex];
                if (!activeRuleSet.UseSimpleHeels)
                    return 0.0f; // 不使用 SimpleHeels 时返回 0
                    
                return activeRuleSet.SimpleHeelsMode == SimpleHeelsHeightMode.Actual
                    ? actualHeight
                    : defaultHeight;
            }
            
            return defaultHeight;
        }

        #region Baseline Actions System

        /// <summary>
        /// 扫描规则集中所有 action 参数
        /// </summary>
        private List<BaselineParameterId> ScanRuleSetParameters(RuleSet ruleSet)
        {
            var parameters = new HashSet<BaselineParameterId>();
            
            foreach (var rule in ruleSet.Rules)
            {
                foreach (var action in rule.Actions)
                {
                    BaselineParameterId? param = null;
                    
                    switch (action.Type)
                    {
                        case ActionType.Penumbra:
                            if (!string.IsNullOrWhiteSpace(action.PenumbraModName))
                            {
                                param = new BaselineParameterId
                                {
                                    Type = ActionType.Penumbra,
                                    PenumbraCollection = action.PenumbraCollection,
                                    PenumbraModName = action.PenumbraModName
                                };
                            }
                            break;
                            
                        case ActionType.Glamourer:
                            if (!string.IsNullOrWhiteSpace(action.GlamourerDesign))
                            {
                                param = new BaselineParameterId
                                {
                                    Type = ActionType.Glamourer,
                                    GlamourerDesign = action.GlamourerDesign
                                };
                            }
                            break;
                            
                        case ActionType.Moodles:
                            if (!string.IsNullOrWhiteSpace(action.MoodleGuid))
                            {
                                param = new BaselineParameterId
                                {
                                    Type = ActionType.Moodles,
                                    MoodleGuid = action.MoodleGuid
                                };
                            }
                            break;
                            
                        case ActionType.Honorific:
                            if (!string.IsNullOrWhiteSpace(action.HonorificTitleJson))
                            {
                                param = new BaselineParameterId
                                {
                                    Type = ActionType.Honorific,
                                    HonorificTitleJson = action.HonorificTitleJson
                                };
                            }
                            break;
                    }
                    
                    if (param != null)
                        parameters.Add(param);
                }
            }
            
            return parameters.ToList();
        }

        /// <summary>当前规则/行动中仍被引用的基准参数键集合。</summary>
        private HashSet<string> GetActiveBaselineParameterKeys(RuleSet ruleSet) =>
            ScanRuleSetParameters(ruleSet)
                .Select(p => p.GetKey())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        /// <summary>仍被规则/行动引用的基准配置（已删除的参数配置保留但不返回）。</summary>
        private List<BaselineActionConfig> GetActiveBaselineConfigs(RuleSet ruleSet)
        {
            var activeKeys = GetActiveBaselineParameterKeys(ruleSet);
            return ruleSet.BaselineConfigs
                .Where(c => activeKeys.Contains(c.ParameterId.GetKey()))
                .ToList();
        }

        /// <summary>
        /// 更新规则集的基准配置（检测新参数）
        /// </summary>
        private int UpdateBaselineConfigs(RuleSet ruleSet)
        {
            var currentParams = ScanRuleSetParameters(ruleSet);
            var existingKeys = ruleSet.BaselineConfigs.Select(c => c.ParameterId.GetKey()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var newCount = 0;
            
            // 添加新检测到的参数
            foreach (var param in currentParams)
            {
                var key = param.GetKey();
                if (!existingKeys.Contains(key))
                {
                    var config = new BaselineActionConfig
                    {
                        ParameterId = param,
                        Mode = BaselineMode.Auto
                    };
                    
                    // 检查是否已被 dismiss
                    if (!ruleSet.DismissedNewParameters.Contains(key))
                    {
                        config.IsNew = true;
                        newCount++;
                    }
                    
                    ruleSet.BaselineConfigs.Add(config);
                }
            }
            
            // 不再引用的参数保留在 BaselineConfigs 中，仅从 UI 隐藏且不执行
            
            return newCount;
        }

        /// <summary>迁移旧版基准模式（Disabled/Enabled/Ignore）到 Auto/Manual/Ignore</summary>
        private void MigrateBaselineModeV2()
        {
            foreach (var ruleSet in Configuration.RuleSets)
            {
                foreach (var config in ruleSet.BaselineConfigs)
                {
                    var raw = (int)config.Mode;
                    switch (raw)
                    {
                        case 1: // 旧 Disabled
                            config.Mode = BaselineMode.Manual;
                            config.ManualModEnabled = false;
                            config.ManualState = BaselineManualState.Disabled;
                            break;
                        case 2: // 旧 Enabled（新 Ignore 也是 2，仅 v12 升级时按旧语义处理）
                            config.Mode = BaselineMode.Manual;
                            config.ManualModEnabled = true;
                            config.ManualState = BaselineManualState.Enabled;
                            break;
                        case 3: // 旧 Ignore
                            config.Mode = BaselineMode.Ignore;
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// 应用基准行动（在匹配规则前调用）
        /// </summary>
        private void ApplyBaselineActions(RuleSet ruleSet, ref bool appliedAnything)
        {
            if (!ruleSet.UseBaselineActions || !IsBaselineApplyAllowed())
                return;
            
            var activeKeys = GetActiveBaselineParameterKeys(ruleSet);
            foreach (var config in ruleSet.BaselineConfigs)
            {
                if (!activeKeys.Contains(config.ParameterId.GetKey()))
                    continue;

                if (config.Mode == BaselineMode.Ignore)
                    continue;
                
                var param = config.ParameterId;
                
                switch (param.Type)
                {
                    case ActionType.Penumbra:
                        ApplyBaselinePenumbra(ruleSet, config, ref appliedAnything);
                        break;
                    case ActionType.Glamourer:
                        ApplyBaselineGlamourer(param, config, ref appliedAnything);
                        break;
                    case ActionType.Moodles:
                        ApplyBaselineMoodles(param, config, ref appliedAnything);
                        break;
                    case ActionType.Honorific:
                        ApplyBaselineHonorific(param, config, ref appliedAnything);
                        break;
                }
            }
        }

        private void ApplyBaselinePenumbra(RuleSet ruleSet, BaselineActionConfig config, ref bool appliedAnything)
        {
            var param = config.ParameterId;
            if (!isPenumbraIpcReady || string.IsNullOrWhiteSpace(param.PenumbraModName))
                return;

            var modEnabled = config.Mode == BaselineMode.Auto
                ? false
                : config.ManualModEnabled;

            ApplyBaselinePenumbraModState(param, modEnabled, ref appliedAnything);

            if (config.Mode != BaselineMode.Manual)
                return;

            if (config.ManualPenumbraOptions.Count == 0)
                SyncBaselinePenumbraManualOptionsFromRules(ruleSet, config);

            foreach (var optionSetting in config.ManualPenumbraOptions)
                ApplyBaselinePenumbraOptionSetting(param, optionSetting, ref appliedAnything);
        }

        private void ApplyBaselinePenumbraModState(BaselineParameterId param, bool enabled, ref bool appliedAnything)
        {
            var applyKey = $"Baseline:P:{param.PenumbraCollection}|{param.PenumbraModName}|{enabled}";
            if (lastAppliedActionKeys.Contains(applyKey))
                return;

            if (!enabled && IsLoginProtectionActive())
                return;

            if (_penumbraInterop.TrySetModEnabled(
                    param.PenumbraCollection ?? "Default",
                    param.PenumbraModName,
                    enabled,
                    out var result,
                    out var error))
            {
                if (result == PenumbraIpcEc.Success || result == PenumbraIpcEc.NothingChanged)
                {
                    lastAppliedActionKeys.Add(applyKey);
                    appliedAnything = true;
                }
                else
                {
                    PluginLog.Warning($"Baseline Penumbra failed: [{param.PenumbraCollection}] {param.PenumbraModName} → Result: {result}");
                }
            }
            else
            {
                PluginLog.Warning($"Baseline Penumbra IPC failed: [{param.PenumbraCollection}] {param.PenumbraModName} → {error}");
            }
        }

        private void ApplyBaselinePenumbraOptionSetting(
            BaselineParameterId param,
            BaselinePenumbraOptionSetting optionSetting,
            ref bool appliedAnything)
        {
            if (string.IsNullOrWhiteSpace(optionSetting.PenumbraOption))
                return;

            var modName = param.PenumbraModName ?? "";
            var collection = param.PenumbraCollection ?? "Default";
            var groupType = _penumbraInterop.GetOptionGroupType(modName, optionSetting.PenumbraOption);

            if (PenumbraInterop.UsesBoolOptionValue(groupType))
            {
                var enabledNames = optionSetting.PenumbraMultiToggleStates
                    .Where(pair => pair.Value)
                    .Select(pair => pair.Key.Trim())
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var applyKey =
                    $"Baseline:P:{collection}|{modName}|{optionSetting.PenumbraOption}|M:{string.Join(",", enabledNames)}";
                if (lastAppliedActionKeys.Contains(applyKey))
                    return;

                if (_penumbraInterop.TryApplyMultiToggleSettings(
                        collection,
                        modName,
                        optionSetting.PenumbraOption,
                        enabledNames,
                        out var result,
                        out var error)
                    && (result == PenumbraIpcEc.Success || result == PenumbraIpcEc.NothingChanged))
                {
                    lastAppliedActionKeys.Add(applyKey);
                    appliedAnything = true;
                }
                else if (!string.IsNullOrEmpty(error))
                {
                    PluginLog.Warning($"Baseline Penumbra MultiToggle failed: {error}");
                }
            }
            else
            {
                var applyKey =
                    $"Baseline:P:{collection}|{modName}|{optionSetting.PenumbraOption}|{optionSetting.PenumbraOptionName}|{optionSetting.PenumbraOptionEnabled}";
                if (lastAppliedActionKeys.Contains(applyKey))
                    return;

                if (_penumbraInterop.TryApplyModSetting(
                        collection,
                        modName,
                        optionSetting.PenumbraOption,
                        optionSetting.PenumbraOptionName,
                        optionSetting.PenumbraOptionEnabled,
                        out var result,
                        out var error)
                    && (result == PenumbraIpcEc.Success || result == PenumbraIpcEc.NothingChanged))
                {
                    lastAppliedActionKeys.Add(applyKey);
                    appliedAnything = true;
                }
                else if (!string.IsNullOrEmpty(error))
                {
                    PluginLog.Warning($"Baseline Penumbra option failed: {error}");
                }
            }
        }

        private void SyncBaselinePenumbraManualOptionsFromRules(RuleSet ruleSet, BaselineActionConfig config)
        {
            var param = config.ParameterId;
            if (param.Type != ActionType.Penumbra)
                return;

            var collected = CollectPenumbraOptionsFromRuleSet(ruleSet, param);
            if (collected.Count == 0)
                return;

            config.ManualPenumbraOptions = collected;
        }

        private List<BaselinePenumbraOptionSetting> CollectPenumbraOptionsFromRuleSet(
            RuleSet ruleSet,
            BaselineParameterId param)
        {
            var result = new Dictionary<string, BaselinePenumbraOptionSetting>(StringComparer.OrdinalIgnoreCase);

            foreach (var rule in ruleSet.Rules)
            {
                foreach (var action in rule.Actions)
                {
                    if (action.Type != ActionType.Penumbra
                        || action.PenumbraActionKind != PenumbraActionKind.SetModOption
                        || string.IsNullOrWhiteSpace(action.PenumbraOption))
                        continue;

                    if (!string.Equals(action.PenumbraModName, param.PenumbraModName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!string.Equals(action.PenumbraCollection ?? "Default", param.PenumbraCollection ?? "Default",
                            StringComparison.OrdinalIgnoreCase))
                        continue;

                    var groupKey = action.PenumbraOption;
                    if (!result.TryGetValue(groupKey, out var setting))
                    {
                        setting = new BaselinePenumbraOptionSetting { PenumbraOption = groupKey };
                        result[groupKey] = setting;
                    }

                    var groupType = _penumbraInterop.GetOptionGroupType(
                        action.PenumbraModName ?? "",
                        action.PenumbraOption ?? "");
                    if (PenumbraInterop.UsesBoolOptionValue(groupType))
                    {
                        foreach (var (optionName, enabled) in action.PenumbraMultiToggleStates)
                            setting.PenumbraMultiToggleStates[optionName] = enabled;
                    }
                    else
                    {
                        setting.PenumbraOptionName = action.PenumbraOptionName ?? "";
                        setting.PenumbraOptionEnabled = action.PenumbraOptionEnabled;
                    }
                }
            }

            return result.Values.OrderBy(s => s.PenumbraOption, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private void ApplyBaselineGlamourer(BaselineParameterId param, BaselineActionConfig config, ref bool appliedAnything)
        {
            if (!isGlamourerAvailable)
                return;

            var shouldEnable = config.Mode == BaselineMode.Manual
                && config.ManualState == BaselineManualState.Enabled;
            var shouldDisable = config.Mode == BaselineMode.Auto
                || (config.Mode == BaselineMode.Manual && config.ManualState == BaselineManualState.Disabled);

            if (shouldDisable)
            {
                var applyKey = "Baseline:G:Revert";
                if (lastAppliedActionKeys.Contains(applyKey))
                    return;

                if (IsLoginProtectionActive() || !IsBaselineGlamourerRevertAllowed())
                    return;

                try
                {
                    CommandManager.ProcessCommand("/glamour revert <me>");
                    lastAppliedActionKeys.Add(applyKey);
                    appliedAnything = true;
                }
                catch (Exception ex)
                {
                    PluginLog.Warning($"Baseline Glamourer revert failed: {ex.Message}");
                }
                return;
            }

            if (!shouldEnable || string.IsNullOrWhiteSpace(param.GlamourerDesign))
                return;

            var action = new HeelsRuleAction { Type = ActionType.Glamourer, GlamourerDesign = param.GlamourerDesign };
            ApplyGlamourerAction(action, ref appliedAnything);
        }

        private void ApplyBaselineMoodles(BaselineParameterId param, BaselineActionConfig config, ref bool appliedAnything)
        {
            if (!isMoodlesIpcReady)
                return;

            var localPlayer = ObjectTable.LocalPlayer;
            if (localPlayer == null || !localPlayer.IsValid())
                return;

            if (config.Mode == BaselineMode.Auto
                || (config.Mode == BaselineMode.Manual && config.ManualState == BaselineManualState.Disabled))
                return;

            if (config.Mode != BaselineMode.Manual
                || config.ManualState != BaselineManualState.Enabled
                || string.IsNullOrWhiteSpace(param.MoodleGuid)
                || !Guid.TryParse(param.MoodleGuid, out var moodleId)
                || moodleId == Guid.Empty)
                return;

            var moodleItems = _moodlesInterop.GetItems(force: false);
            var isPreset = moodleItems.FirstOrDefault(item =>
                item.Id.ToString().Equals(param.MoodleGuid, StringComparison.OrdinalIgnoreCase))?.IsPreset ?? false;
            var applyKey = $"Baseline:M:{param.MoodleGuid}|{(isPreset ? "preset" : "status")}";
            if (lastAppliedActionKeys.Contains(applyKey))
                return;

            if (_moodlesInterop.TryApply(localPlayer, moodleId, isPreset, out var error))
            {
                lastAppliedActionKeys.Add(applyKey);
                appliedAnything = true;
            }
            else
            {
                PluginLog.Warning($"Baseline Moodles failed: {error}");
            }
        }

        private void ApplyBaselineHonorific(BaselineParameterId param, BaselineActionConfig config, ref bool appliedAnything)
        {
            if (!isHonorificIpcReady)
                return;

            var localPlayer = ObjectTable.LocalPlayer;
            if (localPlayer == null || !localPlayer.IsValid())
                return;

            if (config.Mode == BaselineMode.Auto
                || (config.Mode == BaselineMode.Manual && config.ManualState == BaselineManualState.Disabled))
            {
                var applyKey = "Baseline:H:Clear";
                if (lastAppliedActionKeys.Contains(applyKey))
                    return;

                if (_honorificInterop.TryClearLocalTitle(localPlayer, out _))
                {
                    lastAppliedActionKeys.Add(applyKey);
                    appliedAnything = true;
                }
                return;
            }

            if (config.Mode != BaselineMode.Manual
                || config.ManualState != BaselineManualState.Enabled
                || string.IsNullOrWhiteSpace(param.HonorificTitleJson))
                return;

            var titleKey = $"Baseline:H:{param.HonorificTitleJson}";
            if (lastAppliedActionKeys.Contains(titleKey))
                return;

            if (_honorificInterop.TrySetLocalTitle(localPlayer, param.HonorificTitleJson, out var error))
            {
                lastAppliedActionKeys.Add(titleKey);
                appliedAnything = true;
            }
            else
            {
                PluginLog.Warning($"Baseline Honorific failed: {error}");
            }
        }

        #endregion

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
                // SimpleHeels 配置已下沉到 RuleSet 级别，在 DrawRuleSetSelector() 中显示
                // DrawSimpleHeelsHeightModeBar();
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

        private string newRuleSetName = "";
        private string renameRuleSetName = "";
        private bool showNewRuleSetPopup = false;
        private bool showRenameRuleSetPopup = false;
        private bool showDeleteRuleSetConfirm = false;
        
        private void DrawRuleSetSelector()
        {
            var ruleSetNames = Configuration.RuleSets.Select(rs => rs.Name).ToArray();
            var currentIndex = Configuration.ActiveRuleSetIndex;
            
            ImGui.Text(Localization.RuleSetLabel);
            ImGui.SameLine();
            
            ImGui.SetNextItemWidth(300);
            if (ImGui.Combo("##RuleSetCombo", ref currentIndex, ruleSetNames, ruleSetNames.Length))
            {
                if (currentIndex >= 0 && currentIndex < Configuration.RuleSets.Count)
                {
                    Configuration.ActiveRuleSetIndex = currentIndex;
                    SaveConfig();
                }
            }
            
            ImGui.SameLine();
            if (ImGui.Button(Localization.RuleSetNew))
            {
                showNewRuleSetPopup = true;
                newRuleSetName = Localization.RuleSetNewDefaultName;
            }
            
            ImGui.SameLine();
            if (ImGui.Button(Localization.RuleSetRename) && Configuration.RuleSets.Count > 0)
            {
                showRenameRuleSetPopup = true;
                renameRuleSetName = Configuration.RuleSets[Configuration.ActiveRuleSetIndex].Name;
            }
            
            ImGui.SameLine();
            if (ImGui.Button(Localization.RuleSetCopy) && Configuration.RuleSets.Count > 0)
            {
                var clone = Configuration.RuleSets[Configuration.ActiveRuleSetIndex].Clone();
                Configuration.RuleSets.Add(clone);
                Configuration.ActiveRuleSetIndex = Configuration.RuleSets.Count - 1;
                SaveConfig();
            }
            
            ImGui.SameLine();
            if (ImGui.Button(Localization.RuleSetDelete) && Configuration.RuleSets.Count > 1)
            {
                showDeleteRuleSetConfirm = true;
            }
            
            // 新建 RuleSet 弹窗
            if (showNewRuleSetPopup)
            {
                ImGui.OpenPopup("NewRuleSet");
                showNewRuleSetPopup = false;
            }
            
            bool popupOpen = true;
            if (ImGui.BeginPopupModal("NewRuleSet", ref popupOpen, ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.Text(Localization.RuleSetNamePrompt);
                ImGui.InputText("##NewRuleSetName", ref newRuleSetName, 100);
                
                ImGui.Spacing();
                if (ImGui.Button(Localization.RuleSetCreate))
                {
                    if (!string.IsNullOrWhiteSpace(newRuleSetName))
                    {
                        Configuration.RuleSets.Add(CreateNewRuleSet(newRuleSetName));
                        Configuration.ActiveRuleSetIndex = Configuration.RuleSets.Count - 1;
                        SaveConfig();
                        ImGui.CloseCurrentPopup();
                    }
                }
                
                ImGui.SameLine();
                if (ImGui.Button(Localization.Cancel))
                {
                    ImGui.CloseCurrentPopup();
                }
                
                ImGui.EndPopup();
            }
            
            // 重命名 RuleSet 弹窗
            if (showRenameRuleSetPopup)
            {
                ImGui.OpenPopup("RenameRuleSet");
                showRenameRuleSetPopup = false;
            }
            
            popupOpen = true;
            if (ImGui.BeginPopupModal("RenameRuleSet", ref popupOpen, ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.Text(Localization.RuleSetRenamePrompt);
                ImGui.InputText("##RenameRuleSetName", ref renameRuleSetName, 100);
                
                ImGui.Spacing();
                if (ImGui.Button(Localization.Ok))
                {
                    if (!string.IsNullOrWhiteSpace(renameRuleSetName) && Configuration.RuleSets.Count > 0)
                    {
                        Configuration.RuleSets[Configuration.ActiveRuleSetIndex].Name = renameRuleSetName;
                        SaveConfig();
                        ImGui.CloseCurrentPopup();
                    }
                }
                
                ImGui.SameLine();
                if (ImGui.Button(Localization.Cancel))
                {
                    ImGui.CloseCurrentPopup();
                }
                
                ImGui.EndPopup();
            }
            
            // 删除确认弹窗
            if (showDeleteRuleSetConfirm)
            {
                ImGui.OpenPopup("DeleteRuleSetConfirm");
                showDeleteRuleSetConfirm = false;
            }
            
            popupOpen = true;
            if (ImGui.BeginPopupModal("DeleteRuleSetConfirm", ref popupOpen, ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.6f, 0.0f, 1.0f));
                ImGui.TextWrapped(Localization.RuleSetDeleteConfirmMessage(
                    Configuration.RuleSets[Configuration.ActiveRuleSetIndex].Name));
                ImGui.PopStyleColor();
                
                ImGui.Spacing();
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.2f, 0.2f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.9f, 0.3f, 0.3f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.7f, 0.1f, 0.1f, 1.0f));
                if (ImGui.Button(Localization.RuleSetConfirmDelete))
                {
                    Configuration.RuleSets.RemoveAt(Configuration.ActiveRuleSetIndex);
                    Configuration.ActiveRuleSetIndex = Math.Max(0, Configuration.ActiveRuleSetIndex - 1);
                    SaveConfig();
                    ImGui.CloseCurrentPopup();
                }
                ImGui.PopStyleColor(3);
                
                ImGui.SameLine();
                if (ImGui.Button(Localization.Cancel))
                {
                    ImGui.CloseCurrentPopup();
                }
                
                ImGui.EndPopup();
            }
            
            // SimpleHeels 配置（每个 RuleSet 独立）
            if (Configuration.RuleSets.Count > 0 && 
                Configuration.ActiveRuleSetIndex >= 0 && 
                Configuration.ActiveRuleSetIndex < Configuration.RuleSets.Count)
            {
                var activeRuleSet = Configuration.RuleSets[Configuration.ActiveRuleSetIndex];
                
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
                
                var useSimpleHeels = activeRuleSet.UseSimpleHeels;
                if (ImGui.Checkbox(Localization.UseSimpleHeels, ref useSimpleHeels))
                {
                    activeRuleSet.UseSimpleHeels = useSimpleHeels;
                    InvalidateApplyStateForHeightSourceChange();
                    SaveConfig();
                }
                
                // 显示红字警告：如果启用了 SimpleHeels 但 IPC 连接失败
                if (activeRuleSet.UseSimpleHeels && (!isSimpleHeelsAvailable || !isSimpleHeelsIpcReady))
                {
                    ImGui.SameLine();
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.2f, 0.2f, 1.0f));
                    ImGui.TextWrapped(Localization.IsChine 
                        ? "⚠ SimpleHeels 连接失败！" 
                        : "⚠ SimpleHeels connection failed!");
                    ImGui.PopStyleColor();
                }
                
                // 只有启用 SimpleHeels 时才显示模式选择
                if (activeRuleSet.UseSimpleHeels)
                {
                    ImGui.Indent();
                    
                    var mode = (int)activeRuleSet.SimpleHeelsMode;
                    if (ImGui.RadioButton(Localization.SimpleHeelsHeightDefault, mode == (int)SimpleHeelsHeightMode.Default))
                    {
                        if (activeRuleSet.SimpleHeelsMode != SimpleHeelsHeightMode.Default)
                        {
                            activeRuleSet.SimpleHeelsMode = SimpleHeelsHeightMode.Default;
                            currentHeelsHeight = SelectHeelsHeightForMode(currentHeelsDefaultHeight, currentHeelsActualHeight);
                            InvalidateApplyStateForHeightSourceChange();
                            SaveConfig();
                        }
                    }
                    
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip(Localization.SimpleHeelsHeightDefaultTooltip);
                    
                    ImGui.SameLine();
                    if (ImGui.RadioButton(Localization.SimpleHeelsHeightActual, mode == (int)SimpleHeelsHeightMode.Actual))
                    {
                        if (activeRuleSet.SimpleHeelsMode != SimpleHeelsHeightMode.Actual)
                        {
                            activeRuleSet.SimpleHeelsMode = SimpleHeelsHeightMode.Actual;
                            currentHeelsHeight = SelectHeelsHeightForMode(currentHeelsDefaultHeight, currentHeelsActualHeight);
                            InvalidateApplyStateForHeightSourceChange();
                            SaveConfig();
                        }
                    }
                    
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip(Localization.SimpleHeelsHeightActualTooltip);
                    
                    ImGui.Unindent();
                }
                
                // 基准行动配置
                DrawBaselineActionsConfig(activeRuleSet);
            }
        }
        
        private static bool IsBaselineTypeGroupExpanded(RuleSet ruleSet, ActionType type) =>
            !ruleSet.BaselineExpandedTypeGroups.TryGetValue(type.ToString(), out var expanded) || expanded;

        private void SetBaselineTypeGroupExpanded(RuleSet ruleSet, ActionType type, bool expanded)
        {
            ruleSet.BaselineExpandedTypeGroups[type.ToString()] = expanded;
            SaveConfig();
        }

        private void DrawBaselineActionsConfig(RuleSet ruleSet)
        {
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            
            ImGui.SetNextItemOpen(ruleSet.IsBaselineSectionExpanded, ImGuiCond.Always);
            var headerOpen = ImGui.CollapsingHeader(Localization.BaselineActions);
            if (ImGui.IsItemToggledOpen())
            {
                ruleSet.IsBaselineSectionExpanded = !ruleSet.IsBaselineSectionExpanded;
                SaveConfig();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Localization.BaselineActionsDesc);
            
            if (!headerOpen)
                return;
            
            ImGui.Indent();
            
            // 启用开关
            var useBaselineActions = ruleSet.UseBaselineActions;
            if (ImGui.Checkbox(Localization.BaselineActionsEnable, ref useBaselineActions))
            {
                ruleSet.UseBaselineActions = useBaselineActions;
                SaveConfig();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Localization.BaselineActionsEnableTooltip);
            
            if (!ruleSet.UseBaselineActions)
            {
                ImGui.Unindent();
                return;
            }
            
            ImGui.Spacing();
            
            // 按钮：刷新扫描、忽略所有新参数
            if (ImGui.Button(Localization.BaselineRefresh))
            {
                UpdateBaselineConfigs(ruleSet);
                SaveConfig();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Localization.BaselineRefreshTooltip);
            
            ImGui.SameLine();
            
            var activeBaselineConfigs = GetActiveBaselineConfigs(ruleSet);
            var hasNewParams = activeBaselineConfigs.Any(c => c.IsNew);
            if (hasNewParams)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1.0f, 0.8f, 0.0f, 0.6f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1.0f, 0.85f, 0.2f, 0.7f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.9f, 0.75f, 0.0f, 0.8f));
            }
            
            if (ImGui.Button(Localization.BaselineDismissAll))
            {
                foreach (var config in activeBaselineConfigs)
                {
                    if (config.IsNew)
                    {
                        config.IsNew = false;
                        ruleSet.DismissedNewParameters.Add(config.ParameterId.GetKey());
                    }
                }
                SaveConfig();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Localization.BaselineDismissAllTooltip);
            
            if (hasNewParams)
                ImGui.PopStyleColor(3);
            
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            
            // 参数列表 - 仅显示当前规则/行动中仍被引用的参数
            if (activeBaselineConfigs.Count == 0)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.6f, 0.6f, 0.6f, 1.0f));
                ImGui.TextWrapped(Localization.BaselineNoParameters);
                ImGui.PopStyleColor();
            }
            else
            {
                // 按类型分组
                var groupedConfigs = activeBaselineConfigs
                    .GroupBy(c => c.ParameterId.Type)
                    .OrderBy(g => g.Key)
                    .ToList();
                
                foreach (var group in groupedConfigs)
                {
                    var actionType = group.Key;
                    var configs = group.ToList();
                    
                    // 组标题和颜色
                    var (groupName, groupColor) = actionType switch
                    {
                        ActionType.Penumbra => (Localization.BaselinePenumbraMod + $" ({configs.Count})", new Vector4(0.6f, 0.8f, 1.0f, 1.0f)),
                        ActionType.Glamourer => (Localization.BaselineGlamourerDesign + $" ({configs.Count})", new Vector4(1.0f, 0.6f, 0.8f, 1.0f)),
                        ActionType.Moodles => (Localization.BaselineMoodle + $" ({configs.Count})", new Vector4(0.8f, 1.0f, 0.6f, 1.0f)),
                        ActionType.Honorific => (Localization.BaselineHonorific + $" ({configs.Count})", new Vector4(1.0f, 0.9f, 0.4f, 1.0f)),
                        _ => ("Unknown", new Vector4(0.7f, 0.7f, 0.7f, 1.0f))
                    };
                    
                    // 检查组内是否有新参数
                    var hasNewInGroup = configs.Any(c => c.IsNew);
                    if (hasNewInGroup)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.8f, 0.0f, 1.0f));
                        groupName += $" {Localization.BaselineNewParameter}";
                    }
                    else
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, groupColor);
                    }
                    
                    var groupExpanded = IsBaselineTypeGroupExpanded(ruleSet, actionType);
                    ImGui.SetNextItemOpen(groupExpanded, ImGuiCond.Always);
                    var treeOpen = ImGui.TreeNodeEx(groupName);
                    if (ImGui.IsItemToggledOpen())
                        SetBaselineTypeGroupExpanded(ruleSet, actionType, !groupExpanded);
                    ImGui.PopStyleColor();
                    
                    if (treeOpen)
                    {
                        ImGui.Indent();
                        
                        // 组内参数
                        foreach (var config in configs)
                        {
                            DrawBaselineParameter(config, ruleSet);
                        }
                        
                        ImGui.Unindent();
                        ImGui.TreePop();
                    }
                    
                    ImGui.Spacing();
                }
            }
            
            ImGui.Unindent();
        }
        
        private void DrawBaselineParameter(BaselineActionConfig config, RuleSet ruleSet)
        {
            var param = config.ParameterId;
            var paramKey = param.GetKey();
            var startPos = ImGui.GetCursorScreenPos();
            
            if (config.IsNew)
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.8f, 0.0f, 1.0f));
            
            var (displayName, fullInfo) = GetBaselineParameterDisplayInfo(param);
            if (config.IsNew)
            {
                ImGui.TextWrapped($"{Localization.BaselineNewParameter} {displayName}");
                ImGui.PopStyleColor();
            }
            else
            {
                ImGui.TextWrapped(displayName);
            }
            
            if (ImGui.IsItemHovered() && !string.IsNullOrEmpty(fullInfo))
            {
                ImGui.BeginTooltip();
                ImGui.PushTextWrapPos(400);
                ImGui.TextUnformatted(fullInfo);
                ImGui.PopTextWrapPos();
                ImGui.EndTooltip();
            }
            
            ImGui.AlignTextToFramePadding();
            ImGui.Text(Localization.BaselineMode + ":");
            ImGui.SameLine();
            
            var mode = (int)config.Mode;
            ImGui.SetNextItemWidth(120);
            var modeNames = new[] {
                Localization.BaselineModeAuto,
                Localization.BaselineModeManual,
                Localization.BaselineModeIgnore
            };
            
            if (ImGui.Combo($"##Mode{paramKey}", ref mode, modeNames, modeNames.Length))
            {
                var newMode = (BaselineMode)mode;
                if (newMode == BaselineMode.Manual && config.Mode != BaselineMode.Manual)
                {
                    if (param.Type == ActionType.Penumbra && config.ManualPenumbraOptions.Count == 0)
                        SyncBaselinePenumbraManualOptionsFromRules(ruleSet, config);
                }
                config.Mode = newMode;
                SaveConfig();
            }
            
            if (ImGui.IsItemHovered())
            {
                var tooltip = config.Mode switch
                {
                    BaselineMode.Auto => Localization.BaselineModeAutoTooltip,
                    BaselineMode.Manual => Localization.BaselineModeManualTooltip,
                    BaselineMode.Ignore => Localization.BaselineModeIgnoreTooltip,
                    _ => ""
                };
                if (!string.IsNullOrEmpty(tooltip))
                    ImGui.SetTooltip(tooltip);
            }
            
            if (config.Mode == BaselineMode.Manual)
            {
                ImGui.Indent(12f);
                if (param.Type == ActionType.Penumbra)
                    DrawBaselinePenumbraManualSettings(config, ruleSet, paramKey);
                else
                    DrawBaselineSimpleManualSettings(config, paramKey);
                ImGui.Unindent(12f);
            }
            
            if (config.IsNew)
            {
                var drawList = ImGui.GetWindowDrawList();
                var rectMax = new Vector2(
                    ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X,
                    ImGui.GetCursorScreenPos().Y);
                drawList.AddRect(
                    startPos,
                    rectMax,
                    ImGui.ColorConvertFloat4ToU32(new Vector4(1.0f, 0.8f, 0.0f, 0.8f)),
                    3f, ImDrawFlags.None, 1.5f);
            }
            
            ImGui.Separator();
        }

        private (string displayName, string fullInfo) GetBaselineParameterDisplayInfo(BaselineParameterId param)
        {
            switch (param.Type)
            {
                case ActionType.Penumbra:
                    return (
                        $"{param.PenumbraModName}",
                        $"{(Localization.IsChine ? "集合" : "Collection")}: {param.PenumbraCollection}\n{(Localization.IsChine ? "Mod名称" : "Mod Name")}: {param.PenumbraModName}");
                case ActionType.Glamourer:
                    var designName = _glamourerInterop.GetDesignNames(force: false)
                        .FirstOrDefault(name => name.Equals(param.GlamourerDesign, StringComparison.OrdinalIgnoreCase));
                    return (
                        designName ?? (param.GlamourerDesign?.Length > 40 ? param.GlamourerDesign.Substring(0, 37) + "..." : param.GlamourerDesign) ?? "",
                        $"{(Localization.IsChine ? "设计名称/GUID" : "Design Name/GUID")}: {param.GlamourerDesign}");
                case ActionType.Moodles:
                    var moodleItems = _moodlesInterop.GetItems(force: false);
                    var moodleItem = moodleItems.FirstOrDefault(item =>
                        item.Id.ToString().Equals(param.MoodleGuid, StringComparison.OrdinalIgnoreCase));
                    return (
                        moodleItem != null ? moodleItem.Title : (param.MoodleGuid ?? ""),
                        moodleItem != null
                            ? $"{(Localization.IsChine ? "名称" : "Name")}: {moodleItem.Title}\nGUID: {param.MoodleGuid}"
                            : $"GUID: {param.MoodleGuid}");
                case ActionType.Honorific:
                    var titleText = Localization.IsChine ? "称号" : "Title";
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(param.HonorificTitleJson))
                        {
                            var titleObj = JObject.Parse(param.HonorificTitleJson);
                            titleText = titleObj["Title"]?.ToString() ?? titleText;
                        }
                    }
                    catch { }
                    return (titleText, $"JSON: {param.HonorificTitleJson}");
                default:
                    return ("Unknown", "");
            }
        }

        private void DrawBaselineSimpleManualSettings(BaselineActionConfig config, string paramKey)
        {
            var state = (int)config.ManualState;
            if (ImGui.RadioButton($"{Localization.BaselineManualDisabled}##{paramKey}", state == (int)BaselineManualState.Disabled))
            {
                config.ManualState = BaselineManualState.Disabled;
                SaveConfig();
            }
            ImGui.SameLine();
            if (ImGui.RadioButton($"{Localization.BaselineManualEnabled}##{paramKey}", state == (int)BaselineManualState.Enabled))
            {
                config.ManualState = BaselineManualState.Enabled;
                SaveConfig();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Localization.BaselineManualStateTooltip);
        }

        private void DrawBaselinePenumbraManualSettings(BaselineActionConfig config, RuleSet ruleSet, string paramKey)
        {
            var modEnabled = config.ManualModEnabled;
            if (ImGui.Checkbox($"{Localization.BaselineManualModEnabled}##{paramKey}", ref modEnabled))
            {
                config.ManualModEnabled = modEnabled;
                SaveConfig();
            }
            
            ImGui.SameLine();
            if (ImGui.SmallButton($"{Localization.BaselineSyncFromRules}##{paramKey}"))
            {
                SyncBaselinePenumbraManualOptionsFromRules(ruleSet, config);
                SaveConfig();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Localization.BaselineSyncFromRulesTooltip);
            
            if (config.ManualPenumbraOptions.Count == 0)
            {
                ImGui.TextDisabled(Localization.IsChine
                    ? "（无选项，请从规则同步）"
                    : "(No options — sync from rules)");
                return;
            }
            
            var lineHeight = ImGui.GetTextLineHeightWithSpacing();
            var maxScrollHeight = lineHeight * 4.5f + ImGui.GetStyle().ItemSpacing.Y * 2;
            
            ImGui.BeginChild($"##BaselineOpts{paramKey}", new Vector2(0, maxScrollHeight), true);
            DrawBaselinePenumbraOptionSettings(config, paramKey);
            ImGui.EndChild();
        }

        private void DrawBaselinePenumbraOptionSettings(BaselineActionConfig config, string paramKey)
        {
            var param = config.ParameterId;
            var modName = param.PenumbraModName ?? "";
            
            for (var i = 0; i < config.ManualPenumbraOptions.Count; i++)
            {
                var optionSetting = config.ManualPenumbraOptions[i];
                ImGui.PushID(i);
                
                ImGui.TextDisabled(optionSetting.PenumbraOption);
                
                var groupType = _penumbraInterop.GetOptionGroupType(modName, optionSetting.PenumbraOption);
                if (PenumbraInterop.UsesBoolOptionValue(groupType))
                {
                    var optionNames = _penumbraInterop.GetOptionNames(modName, optionSetting.PenumbraOption);
                    foreach (var optionName in optionNames)
                    {
                        if (!optionSetting.PenumbraMultiToggleStates.ContainsKey(optionName))
                            optionSetting.PenumbraMultiToggleStates[optionName] = false;

                        var enabled = optionSetting.PenumbraMultiToggleStates[optionName];
                        if (ImGui.Checkbox($"{optionName}##BaselineMulti{paramKey}{i}{optionName}", ref enabled))
                        {
                            optionSetting.PenumbraMultiToggleStates[optionName] = enabled;
                            SaveConfig();
                        }
                    }
                }
                else
                {
                    var options = _penumbraInterop.GetOptionNames(modName, optionSetting.PenumbraOption);
                    var optionName = optionSetting.PenumbraOptionName ?? "";
                    var preview = string.IsNullOrWhiteSpace(optionName)
                        ? Localization.SelectPenumbraOptionName
                        : optionName;
                    ImGui.SetNextItemWidth(200);
                    if (ImGui.BeginCombo($"##BaselineOpt{paramKey}{i}", preview))
                    {
                        foreach (var option in options)
                        {
                            if (ImGui.Selectable(option, string.Equals(option, optionName, StringComparison.OrdinalIgnoreCase)))
                            {
                                optionSetting.PenumbraOptionName = option;
                                SaveConfig();
                            }
                        }
                        ImGui.EndCombo();
                    }
                    
                    ImGui.SameLine();
                    var optionEnabled = optionSetting.PenumbraOptionEnabled;
                    var optionEnabledLabel = Localization.IsChine ? "启用" : "On";
                    if (ImGui.Checkbox($"{optionEnabledLabel}##{paramKey}{i}", ref optionEnabled))
                    {
                        optionSetting.PenumbraOptionEnabled = optionEnabled;
                        SaveConfig();
                    }
                }
                
                ImGui.PopID();
            }
        }
        
        private bool scrollToLastRule = false;

        private void DrawRulesActionGuide()
        {
            ImGui.PushStyleColor(ImGuiCol.Text, RulePenumbraConflictWarningColor);
            ImGui.TextWrapped(Localization.PenumbraPriorityWarning);
            ImGui.PopStyleColor();
            ImGui.Spacing();

            if (!ImGui.CollapsingHeader(Localization.RulesActionGuideHeader))
                return;

            ImGui.Indent();
            ImGui.TextDisabled(Localization.PerActionTypeHint);
            ImGui.Spacing();
            ImGui.TextWrapped(Localization.ActionTypeHint);
            ImGui.TextWrapped(Localization.OptionalAddonsHint);
            ImGui.TextDisabled(Localization.MoodlesRemoteApplyHint);
            ImGui.Unindent();
        }
        
        private void DrawRulesTab()
        {
            // RuleSet 选择器
            DrawRuleSetSelector();
            
            ImGui.Separator();
            ImGui.Spacing();
            
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
            DrawRulesActionGuide();
            ImGui.Separator();
            ImGui.TextWrapped(Localization.HeightRulesOrderHint);

            if (ImGui.Button(Localization.AddNewRule))
            {
                ActiveRules.Add(CreateEmptyRule());
                SaveConfig();
                scrollToLastRule = true;
            }

            ImGui.BeginChild("RulesList", new Vector2(0, -30), true, ImGuiWindowFlags.None);
            DrawGlamourerFeetWarningBanner();

            var activeRules = ActiveRules;
            for (int i = 0; i < activeRules.Count; i++)
            {
                if (i > 0)
                {
                    ImGui.Spacing();
                    ImGui.Separator();
                    ImGui.Spacing();
                }

                ImGui.PushID(i);
                DrawRuleRow(i, out var ruleDeleted);
                ImGui.PopID();
                
                if (ruleDeleted)
                    break; // 删除了规则，立即退出循环避免索引错误
                
                // 如果是最后一个规则且需要滚动到它
                if (i == activeRules.Count - 1 && scrollToLastRule)
                {
                    ImGui.SetScrollHereY(1.0f);
                    scrollToLastRule = false;
                }
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
            if (SliderIntWithManualInput($"{Localization.DecimalPrecision} ({Localization.Places})", ref precision, 0, 5, "decimal_precision"))
            {
                Configuration.DecimalPrecision = precision;
                SaveConfig();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Localization.DoubleClickToInput);

            ImGui.SetNextItemWidth(200);
            var cooldown = Configuration.ApplyCooldownSeconds;
            if (SliderFloatWithManualInput(Localization.ApplyCooldown, ref cooldown, 0f, 10f, "%.1f s", "apply_cooldown"))
            {
                Configuration.ApplyCooldownSeconds = cooldown;
                SaveConfig();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Localization.ApplyCooldownHint + "\n" + Localization.DoubleClickToInput);
            else
                ImGui.TextDisabled(Localization.ApplyCooldownHint);

            ImGui.SetNextItemWidth(200);
            var matchStableDelay = Configuration.RuleMatchStableSeconds;
            if (SliderFloatWithManualInput(Localization.RuleMatchStableDelay, ref matchStableDelay, 0f, 3f, "%.2f s", "rule_match_stable"))
            {
                Configuration.RuleMatchStableSeconds = matchStableDelay;
                SaveConfig();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Localization.RuleMatchStableDelayHint + "\n" + Localization.DoubleClickToInput);
            else
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
            // 清空现有 RuleSets 并创建默认 RuleSet
            Configuration.RuleSets.Clear();
            Configuration.RuleSets.Add(CreateNewRuleSet(Localization.RuleSetDefaultName));
            Configuration.ActiveRuleSetIndex = 0;
            
            // 保留旧字段用于向后兼容（但标记为废弃）
#pragma warning disable CS0618
            Configuration.Rules = CreateDefaultRuleSet();
#pragma warning restore CS0618
            Configuration.GlamourerRules = [];
            Configuration.PenumbraRules = [];
            Configuration.DecimalPrecision = 4;
            Configuration.ApplyCooldownSeconds = 1f;
            Configuration.RuleMatchStableSeconds = 0.5f;
            Configuration.UiLanguage = UiLanguagePreference.System;
            Configuration.SimpleHeelsHeightMode = SimpleHeelsHeightMode.Default; // 保留用于兼容性
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
        }

        private void DrawRuleRow(int ruleIndex, out bool deleted)
        {
            deleted = false;
            var activeRules = ActiveRules;
            if (ruleIndex < 0 || ruleIndex >= activeRules.Count)
                return;
                
            var currentRule = activeRules[ruleIndex];
            
            // 在每条规则之间绘制分界线（第一条规则除外）
            if (ruleIndex > 0)
            {
                var lineStart = ImGui.GetCursorScreenPos();
                var lineEnd = new Vector2(lineStart.X + ImGui.GetContentRegionAvail().X, lineStart.Y);
                ImGui.GetWindowDrawList().AddLine(
                    lineStart,
                    lineEnd,
                    ImGui.GetColorU32(new Vector4(0.4f, 0.4f, 0.4f, 0.6f)),
                    2.5f);
                ImGui.Dummy(new Vector2(0, 2)); // 为分隔线占位
            }

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
            // 折叠按钮宽度
            var collapseWidth = ImGui.CalcTextSize("▼").X + ImGui.GetStyle().FramePadding.X * 2f;
            var switchColumnWidth = Math.Max(deleteWidth, Math.Max(dragWidth, Math.Max(collapseWidth, ImGui.GetFrameHeight()))) + 16f;

            if (ImGui.BeginTable(
                    $"RuleRowTable{ruleIndex}",
                    2,
                    ImGuiTableFlags.NoSavedSettings
                        | ImGuiTableFlags.BordersInnerV
                        | ImGuiTableFlags.SizingStretchProp))
            {
                ImGui.TableSetupColumn("RuleSwitch", ImGuiTableColumnFlags.WidthFixed, switchColumnWidth);
                ImGui.TableSetupColumn("RuleOptions", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                DrawRuleSwitchColumn(ruleIndex, currentRule, out var ruleDeleted);
                
                if (ruleDeleted)
                {
                    deleted = true;
                    if (ruleIndex == currentMatchedRuleIndex)
                    {
                        ImGui.PopStyleColor(3);
                    }
                    return;
                }

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

        private void DrawRuleSwitchColumn(int ruleIndex, HeelsRule currentRule, out bool deleted)
        {
            deleted = false;
            var columnWidth = Math.Max(0f, ImGui.GetContentRegionAvail().X);

            // 启用复选框和折叠按钮在同一行
            var isActive = currentRule.IsActive;
            if (ImGui.Checkbox($"##RuleActive{ruleIndex}", ref isActive))
            {
                currentRule.IsActive = isActive;
                SaveConfig();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Localization.EnableDisableRuleTooltip);
            
            ImGui.SameLine();
            
            // 折叠/展开箭头按钮
            var expanded = !currentRule.IsCollapsed;
            if (ImGui.ArrowButton($"##RuleCollapse{ruleIndex}", expanded ? ImGuiDir.Down : ImGuiDir.Right))
            {
                currentRule.IsCollapsed = !currentRule.IsCollapsed;
                SaveConfig();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(currentRule.IsCollapsed 
                    ? Localization.ExpandRuleTooltip
                    : Localization.CollapseRuleTooltip);

            DrawRuleDragHandle(ruleIndex, columnWidth);

            var deleteWidth = ImGui.CalcTextSize(Localization.Delete).X
                + ImGui.GetStyle().FramePadding.X * 2f;
            if (ImGui.Button($"{Localization.Delete}##DeleteRule{ruleIndex}", new Vector2(Math.Max(deleteWidth, columnWidth), 0)))
            {
                ImGui.OpenPopup($"ConfirmDeleteRule_{ruleIndex}");
            }
            
            // 删除确认对话框（必须在同一个上下文中渲染）
            bool popupOpen = true;
            if (ImGui.BeginPopupModal($"ConfirmDeleteRule_{ruleIndex}", ref popupOpen, ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.Text(Localization.IsChine 
                    ? "确定要删除此规则吗？" 
                    : "Are you sure you want to delete this rule?");
                ImGui.Spacing();
                
                if (ImGui.Button(Localization.IsChine ? "确定" : "Confirm", new Vector2(120, 0)))
                {
                    if (ruleIndex >= 0 && ruleIndex < ActiveRules.Count)
                    {
                        ActiveRules.RemoveAt(ruleIndex);
                        SaveConfig();
                        deleted = true;
                    }
                    ImGui.CloseCurrentPopup();
                }
                
                ImGui.SameLine();
                
                if (ImGui.Button(Localization.IsChine ? "取消" : "Cancel", new Vector2(120, 0)))
                {
                    ImGui.CloseCurrentPopup();
                }
                
                ImGui.EndPopup();
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
            var isUnreachable = RuleHeightAnalysis.IsUnreachable(ActiveRules, ruleIndex, tolerance);
            var isInvalidElse = ruleIndex > 0
                && currentRule.BranchKind == RuleBranchKind.Else
                && ruleIndex < ActiveRules.Count - 1
                && ActiveRules.Skip(ruleIndex + 1).Any(r => r.IsActive);
            var hasUnreachableWarning = isUnreachable || isInvalidElse;
            var isMatched = currentMatchedRuleIndex == ruleIndex;
            var hasFeetWarning = DoesRuleHaveFeetWarning(currentRule);
            
            // 折叠模式：只显示规则编号和名称
            if (currentRule.IsCollapsed)
            {
                ImGui.AlignTextToFramePadding();
                
                // 根据状态选择颜色
                Vector4 textColor;
                if (hasFeetWarning)
                    textColor = new Vector4(1.0f, 0.3f, 0.3f, 1.0f); // 红色：脚部装备警告（最高优先级）
                else if (hasUnreachableWarning)
                    textColor = RuleUnreachableWarningColor; // 黄色：不可达 / 无效否则
                else if (isMatched)
                    textColor = new Vector4(0.0f, 1.0f, 0.0f, 1.0f); // 绿色：当前匹配
                else
                    textColor = new Vector4(0.6f, 0.6f, 0.6f, 1.0f); // 灰色：正常
                
                ImGui.PushStyleColor(ImGuiCol.Text, textColor);
                
                // 显示规则编号
                var ruleLabel = Localization.RuleLabel(ruleIndex + 1);
                ImGui.Text(ruleLabel);
                
                // 显示自定义名称（如果有）
                if (!string.IsNullOrWhiteSpace(currentRule.Name))
                {
                    ImGui.SameLine();
                    ImGui.Text($"- {currentRule.Name}");
                }
                
                // 新行：显示分支类型和条件组信息
                var branchLabel = ruleIndex == 0 ? Localization.RuleOrderIf : Localization.RuleBranchLabel(currentRule.BranchKind);
                
                if (currentRule.BranchKind == RuleBranchKind.Else && ruleIndex > 0)
                {
                    // 否则分支，显示 "否则"
                    ImGui.Text($"{branchLabel}");
                }
                else
                {
                    // 否则如果或如果，显示条件组数量
                    var groupCount = currentRule.ConditionGroups?.Count ?? 0;
                    var groupText = Localization.ConditionGroupCount(groupCount);
                    ImGui.Text($"{branchLabel} - {groupText}");
                }
                
                // 新行：显示条件类型摘要
                var conditionTypesSummary = GetConditionTypesSummary(currentRule);
                ImGui.Text($"  {conditionTypesSummary}");
                
                // 新行：显示 action 数量
                var actionCount = currentRule.Actions?.Count ?? 0;
                var actionText = Localization.RuleActionCount(actionCount);
                ImGui.Text(actionText);
                
                ImGui.PopStyleColor();
                
                return;
            }
            
            // 展开模式：显示完整内容
            
            // 第一行：规则编号 + 自定义名称输入框
            ImGui.AlignTextToFramePadding();
            var expandedRuleLabel = Localization.RuleLabel(ruleIndex + 1);
            
            // 根据状态选择颜色（展开模式下的规则编号）
            Vector4 expandedTextColor;
            if (hasFeetWarning)
                expandedTextColor = new Vector4(1.0f, 0.3f, 0.3f, 1.0f); // 红色：脚部装备警告（最高优先级）
            else if (hasUnreachableWarning)
                expandedTextColor = RuleUnreachableWarningColor; // 黄色：不可达 / 无效否则
            else if (isMatched)
                expandedTextColor = new Vector4(0.0f, 1.0f, 0.0f, 1.0f); // 绿色：当前匹配
            else
                expandedTextColor = new Vector4(1.0f, 1.0f, 1.0f, 1.0f); // 白色：正常
            
            ImGui.PushStyleColor(ImGuiCol.Text, expandedTextColor);
            ImGui.Text(expandedRuleLabel);
            ImGui.PopStyleColor();
            
            ImGui.SameLine();
            ImGui.Text(Localization.RuleNameLabel);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(300);
            var ruleName = currentRule.Name ?? "";
            if (ImGui.InputText($"##RuleName{ruleIndex}", ref ruleName, 100))
            {
                currentRule.Name = ruleName;
                SaveConfig();
            }
            
            // 第二行：分支选择器（换行显示）
            ImGui.AlignTextToFramePadding();
            DrawRuleBranchSelector(ruleIndex, currentRule, hasUnreachableWarning, isInvalidElse);

            // 新的条件系统 UI（换行显示，不用 SameLine）
            DrawConditionSystemUI(ruleIndex, currentRule, hasUnreachableWarning);

            DrawRuleActionsList(ruleIndex, currentRule);
        }

        private void DrawRuleBranchSelector(int ruleIndex, HeelsRule rule, bool hasUnreachableWarning, bool isInvalidElse)
        {
            if (ruleIndex == 0)
            {
                if (hasUnreachableWarning)
                    ImGui.PushStyleColor(ImGuiCol.Text, RuleUnreachableWarningColor);

                ImGui.TextDisabled(Localization.RuleOrderIf);

                if (hasUnreachableWarning)
                {
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip(Localization.RuleUnreachableHint);
                    ImGui.PopStyleColor();
                }

                return;
            }

            if (hasUnreachableWarning)
                ImGui.PushStyleColor(ImGuiCol.Text, RuleUnreachableWarningColor);

            ImGui.SetNextItemWidth(96);
            var branch = rule.BranchKind;
            var preview = Localization.RuleBranchLabel(branch);
            if (ImGui.BeginCombo($"##RuleBranch{ruleIndex}", preview))
            {
                if (ImGui.Selectable(
                        Localization.RuleBranchLabel(RuleBranchKind.ElseIf),
                        branch == RuleBranchKind.ElseIf))
                {
                    rule.BranchKind = RuleBranchKind.ElseIf;
                    SaveConfig();
                }

                if (ImGui.Selectable(
                        Localization.RuleBranchLabel(RuleBranchKind.Else),
                        branch == RuleBranchKind.Else))
                {
                    rule.BranchKind = RuleBranchKind.Else;
                    SaveConfig();
                }

                ImGui.EndCombo();
            }

            if (isInvalidElse && ImGui.IsItemHovered())
                ImGui.SetTooltip(Localization.RuleBranchElseInvalidHint);
            else if (hasUnreachableWarning && ImGui.IsItemHovered())
                ImGui.SetTooltip(Localization.RuleUnreachableHint);

            if (hasUnreachableWarning)
                ImGui.PopStyleColor();
        }

        private void DrawConditionSystemUI(int ruleIndex, HeelsRule rule, bool hasUnreachableWarning)
        {
            var isElseBranch = ruleIndex > 0 && rule.BranchKind == RuleBranchKind.Else;
            
            // 确保 ConditionGroups 已初始化（如果规则不是 Else）
            if (!isElseBranch)
            {
                if (rule.ConditionGroups == null || rule.ConditionGroups.Count == 0)
                {
                    // 默认创建一个空的 AND 组
                    rule.ConditionGroups = new List<ConditionGroup>
                    {
                        new ConditionGroup
                        {
                            Operator = LogicOperator.And,
                            Conditions = new List<RuleCondition>()
                        }
                    };
                    SaveConfig();
                }
            }
            
            // 如果是 Else 分支，显示提示（不显示添加条件按钮）
            if (isElseBranch)
            {
                if (hasUnreachableWarning)
                    ImGui.PushStyleColor(ImGuiCol.Text, RuleUnreachableWarningColor);
                    
                ImGui.Text(Localization.ConditionModeNone);
                
                if (hasUnreachableWarning)
                {
                    if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                        ImGui.SetTooltip(Localization.RuleUnreachableHint);
                    ImGui.PopStyleColor();
                }
                
                return;
            }
            
            // 显示多个条件组
            for (int groupIndex = 0; groupIndex < rule.ConditionGroups!.Count; groupIndex++)
            {
                var group = rule.ConditionGroups[groupIndex];
                ImGui.PushID($"ConditionGroup_{ruleIndex}_{groupIndex}");
                
                // 条件组标题行
                DrawConditionGroupHeader(ruleIndex, groupIndex, group, hasUnreachableWarning);
                
                // 条件组内的条件列表
                ImGui.Indent();
                DrawConditionGroupContents(ruleIndex, groupIndex, group, hasUnreachableWarning);
                ImGui.Unindent();
                
                // 显示条件组之间的操作符（除了最后一个组）
                if (groupIndex < rule.ConditionGroups.Count - 1)
                {
                    DrawConditionGroupOperator(ruleIndex, groupIndex, group);
                }
                
                ImGui.PopID();
                ImGui.Spacing();
            }
            
            // "添加条件组"按钮
            if (ImGui.Button($"{Localization.AddConditionGroup}##AddGroup_{ruleIndex}"))
            {
                rule.ConditionGroups.Add(new ConditionGroup
                {
                    Operator = LogicOperator.And,
                    Conditions = new List<RuleCondition>()
                });
                SaveConfig();
            }
            
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Localization.AddConditionGroupTooltip);
        }
        
        /// <summary>绘制条件组标题栏（包含组内操作符选择器和删除按钮）</summary>
        private void DrawConditionGroupHeader(int ruleIndex, int groupIndex, ConditionGroup group, bool hasUnreachableWarning)
        {
            var rule = ActiveRules[ruleIndex];
            
            // 删除条件组按钮（只有多个组时才显示）
            if (rule.ConditionGroups!.Count > 1)
            {
                if (ImGui.Button($"-##DeleteGroup_{ruleIndex}_{groupIndex}"))
                {
                    ImGui.OpenPopup($"ConfirmDeleteGroup_{ruleIndex}_{groupIndex}");
                }
                
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(Localization.DeleteConditionGroup);
                
                // 删除确认对话框（必须在同一个上下文中渲染）
                bool popupOpen = true;
                if (ImGui.BeginPopupModal($"ConfirmDeleteGroup_{ruleIndex}_{groupIndex}", ref popupOpen, ImGuiWindowFlags.AlwaysAutoResize))
                {
                    ImGui.Text(Localization.ConfirmDeleteConditionGroup);
                    ImGui.Spacing();
                    
                    if (ImGui.Button(Localization.IsChine ? "确认" : "Confirm"))
                    {
                        rule.ConditionGroups.RemoveAt(groupIndex);
                        SaveConfig();
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.SameLine();
                    if (ImGui.Button(Localization.IsChine ? "取消" : "Cancel"))
                    {
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.EndPopup();
                }
                
                ImGui.SameLine();
            }
            
            // 组内操作符选择器
            ImGui.SetNextItemWidth(100);
            var modeLabel = group.Operator == LogicOperator.And ? Localization.ConditionModeAnd : Localization.ConditionModeOr;
            
            if (hasUnreachableWarning)
                ImGui.PushStyleColor(ImGuiCol.Text, RuleUnreachableWarningColor);
            
            if (ImGui.BeginCombo($"##GroupOperator_{groupIndex}", modeLabel))
            {
                if (ImGui.Selectable(Localization.ConditionModeAnd, group.Operator == LogicOperator.And))
                {
                    group.Operator = LogicOperator.And;
                    SaveConfig();
                }
                
                if (ImGui.Selectable(Localization.ConditionModeOr, group.Operator == LogicOperator.Or))
                {
                    group.Operator = LogicOperator.Or;
                    SaveConfig();
                }
                
                ImGui.EndCombo();
            }
            
            if (hasUnreachableWarning)
            {
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    ImGui.SetTooltip(Localization.RuleUnreachableHint);
                ImGui.PopStyleColor();
            }
            
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Localization.ConditionHint);
            
            // 条件组标题
            ImGui.SameLine();
            ImGui.TextDisabled(Localization.ConditionGroupLabel(groupIndex));
        }
        
        /// <summary>绘制条件组内容（条件列表）</summary>
        private void DrawConditionGroupContents(int ruleIndex, int groupIndex, ConditionGroup group, bool hasUnreachableWarning)
        {
            var conditions = group.Conditions;
            var formatString = $"%.{Configuration.DecimalPrecision}f";
            
            // 绘制每个条件
            for (int i = 0; i < conditions.Count; i++)
            {
                ImGui.PushID($"Condition_{i}");
                
                var condition = conditions[i];
                
                // 拖拽手柄
                if (ImGui.Button(": :"))
                {
                    // 按钮作为拖拽手柄
                }
                
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(Localization.IsChine ? "拖动以调整顺序" : "Drag to reorder");
                
                if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                {
                    if (!_conditionDragSourceIndex.HasValue)
                    {
                        _conditionDragSourceRuleIndex = ruleIndex;
                        _conditionDragSourceGroupIndex = groupIndex;
                        _conditionDragSourceIndex = i;
                    }
                }
                
                // 更新拖拽目标
                UpdateConditionDragTarget(ruleIndex, groupIndex, i);
                
                // 显示拖拽目标高亮
                if (_conditionDragSourceRuleIndex == ruleIndex 
                    && _conditionDragSourceGroupIndex == groupIndex
                    && _conditionDragSourceIndex.HasValue
                    && _conditionDragTargetIndex == i
                    && _conditionDragSourceIndex != i)
                {
                    var rowMin = ImGui.GetItemRectMin();
                    var rowMax = ImGui.GetItemRectMax();
                    rowMax.X = ImGui.GetContentRegionMax().X;
                    ImGui.GetWindowDrawList().AddRect(
                        rowMin,
                        rowMax,
                        ImGui.GetColorU32(ImGuiCol.DragDropTarget),
                        0f,
                        0,
                        2f);
                }
                
                ImGui.SameLine();
                
                // 删除按钮（带确认）
                if (ImGui.Button("-"))
                {
                    conditionToDeleteRuleIndex = ruleIndex;
                    conditionToDeleteGroupIndex = groupIndex;
                    conditionToDeleteIndex = i;
                    ImGui.OpenPopup($"ConfirmDeleteCondition_{ruleIndex}_{groupIndex}_{i}");
                }
                
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(Localization.IsChine ? "删除此条件" : "Delete this condition");
                
                // 删除确认对话框（必须在同一个上下文中渲染）
                bool popupOpen = true;
                if (ImGui.BeginPopupModal($"ConfirmDeleteCondition_{ruleIndex}_{groupIndex}_{i}", ref popupOpen, ImGuiWindowFlags.AlwaysAutoResize))
                {
                    ImGui.Text(Localization.IsChine ? "确认删除此条件？" : "Delete this condition?");
                    ImGui.Spacing();
                    
                    if (ImGui.Button(Localization.IsChine ? "确认" : "Confirm"))
                    {
                        group.Conditions.RemoveAt(i);
                        SaveConfig();
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.SameLine();
                    if (ImGui.Button(Localization.IsChine ? "取消" : "Cancel"))
                    {
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.EndPopup();
                }
                
                ImGui.SameLine();
                
                if (hasUnreachableWarning)
                    ImGui.BeginDisabled();
                
                // 根据条件类型绘制编辑器
                if (condition is HeightCondition heightCond)
                {
                    DrawHeightConditionEditor(ruleIndex, groupIndex, i, heightCond, formatString);
                }
                else if (condition is EquipmentCondition equipCond)
                {
                    DrawEquipmentConditionEditor(ruleIndex, groupIndex, i, equipCond);
                }
                
                if (hasUnreachableWarning)
                    ImGui.EndDisabled();
                
                ImGui.PopID();
            }
            
            // 处理条件拖拽重排序
            ProcessConditionDragReorder(ruleIndex, groupIndex);
            
            // 删除确认对话框已移至主配置窗口统一处理
            
            // 添加条件按钮
            if (ImGui.Button($"+##AddCondition_{groupIndex}"))
            {
                ImGui.OpenPopup($"AddConditionPopup_{groupIndex}");
            }
            
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Localization.IsChine ? "添加新条件" : "Add new condition");
            
            // 添加条件弹出菜单
            if (ImGui.BeginPopup($"AddConditionPopup_{groupIndex}"))
            {
                if (ImGui.Selectable(Localization.IsChine ? "高度条件" : "Height Condition"))
                {
                    group.Conditions.Add(new HeightCondition
                    {
                        Comparison = HeightComparison.LessThanOrEqual,
                        Value = 0.0f
                    });
                    SaveConfig();
                }
                
                if (ImGui.Selectable(Localization.IsChine ? "装备条件" : "Equipment Condition"))
                {
                    group.Conditions.Add(new EquipmentCondition
                    {
                        Slot = EquipSlot.Feet,
                        MustBeEquipped = true
                    });
                    SaveConfig();
                }
                
                ImGui.EndPopup();
            }
        }
        
        /// <summary>绘制条件组之间的操作符选择器</summary>
        private void DrawConditionGroupOperator(int ruleIndex, int groupIndex, ConditionGroup group)
        {
            ImGui.Indent();
            
            // 操作符选择器（连接到下一个组）
            ImGui.SetNextItemWidth(100);
            var operatorLabel = group.OperatorToNext == LogicOperator.And ? "AND" : "OR";
            
            if (ImGui.BeginCombo($"##GroupOperatorToNext_{groupIndex}", operatorLabel))
            {
                if (ImGui.Selectable("AND", group.OperatorToNext == LogicOperator.And))
                {
                    group.OperatorToNext = LogicOperator.And;
                    SaveConfig();
                }
                
                if (ImGui.Selectable("OR", group.OperatorToNext == LogicOperator.Or))
                {
                    group.OperatorToNext = LogicOperator.Or;
                    SaveConfig();
                }
                
                ImGui.EndCombo();
            }
            
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Localization.ConnectToNextGroup);
            
            ImGui.SameLine();
            ImGui.TextDisabled($"({(Localization.IsChine ? "连接到下一组" : "to next group")})");
            
            ImGui.Unindent();
        }

        // 旧的 DrawConditionsList 方法已被 DrawConditionGroupContents 取代（支持多条件组）
        
        private void UpdateConditionDragTarget(int ruleIndex, int groupIndex, int conditionIndex)
        {
            if (_conditionDragSourceRuleIndex != ruleIndex 
                || _conditionDragSourceGroupIndex != groupIndex
                || !_conditionDragSourceIndex.HasValue 
                || !ImGui.IsMouseDown(ImGuiMouseButton.Left))
                return;

            var mousePos = ImGui.GetIO().MousePos;
            var rowMin = ImGui.GetItemRectMin();
            var rowMax = ImGui.GetItemRectMax();
            rowMax.X = ImGui.GetContentRegionMax().X;
            
            if (mousePos.X < rowMin.X || mousePos.X > rowMax.X
                || mousePos.Y < rowMin.Y || mousePos.Y > rowMax.Y)
            {
                return;
            }

            _conditionDragTargetIndex = conditionIndex;
        }

        private void ProcessConditionDragReorder(int ruleIndex, int groupIndex)
        {
            if (_conditionDragSourceRuleIndex != ruleIndex 
                || _conditionDragSourceGroupIndex != groupIndex
                || !_conditionDragSourceIndex.HasValue)
                return;

            if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                ImGui.SetTooltip(Localization.IsChine 
                    ? $"移动条件 {_conditionDragSourceIndex.Value + 1}" 
                    : $"Moving condition {_conditionDragSourceIndex.Value + 1}");
                return;
            }

            if (_conditionDragTargetIndex.HasValue
                && _conditionDragSourceIndex.Value != _conditionDragTargetIndex.Value)
            {
                ReorderCondition(ruleIndex, groupIndex, _conditionDragSourceIndex.Value, _conditionDragTargetIndex.Value);
            }

            _conditionDragSourceRuleIndex = null;
            _conditionDragSourceGroupIndex = null;
            _conditionDragSourceIndex = null;
            _conditionDragTargetIndex = null;
        }
        
        /// <summary>绘制条件删除确认弹出框（用于新的多条件组 UI）</summary>

        private void DrawActionDeleteConfirmation(out bool deleted)
        {
            deleted = false;
            
            if (ImGui.BeginPopupModal("ConfirmDeleteAction", ref drawConfigUi, ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.Text(Localization.IsChine 
                    ? "确定要删除此行动吗？" 
                    : "Are you sure you want to delete this action?");
                ImGui.Spacing();
                
                if (ImGui.Button(Localization.IsChine ? "确定" : "Confirm", new Vector2(120, 0)))
                {
                    if (actionToDeleteRuleIndex >= 0 
                        && actionToDeleteRuleIndex < ActiveRules.Count
                        && actionToDeleteIndex >= 0)
                    {
                        var rule = ActiveRules[actionToDeleteRuleIndex];
                        if (actionToDeleteIndex < rule.Actions.Count)
                        {
                            rule.Actions.RemoveAt(actionToDeleteIndex);
                            SaveConfig();
                            deleted = true;
                        }
                    }
                    
                    actionToDeleteRuleIndex = -1;
                    actionToDeleteIndex = -1;
                    ImGui.CloseCurrentPopup();
                }
                
                ImGui.SameLine();
                
                if (ImGui.Button(Localization.IsChine ? "取消" : "Cancel", new Vector2(120, 0)))
                {
                    actionToDeleteRuleIndex = -1;
                    actionToDeleteIndex = -1;
                    ImGui.CloseCurrentPopup();
                }
                
                ImGui.EndPopup();
            }
        }

        /// <summary>获取规则中条件的类型摘要</summary>
        private string GetConditionTypesSummary(HeelsRule rule)
        {
            if (rule.ConditionGroups == null || rule.ConditionGroups.Count == 0)
                return Localization.NoConditionsSummary;
            
            bool hasHeight = false;
            bool hasEquipment = false;
            
            foreach (var group in rule.ConditionGroups)
            {
                if (group.Conditions == null) continue;
                
                foreach (var condition in group.Conditions)
                {
                    if (condition is HeightCondition)
                        hasHeight = true;
                    else if (condition is EquipmentCondition)
                        hasEquipment = true;
                    
                    if (hasHeight && hasEquipment)
                        break; // 已经找到所有类型，提前退出
                }
                
                if (hasHeight && hasEquipment)
                    break;
            }
            
            if (hasHeight && hasEquipment)
                return Localization.HeightAndEquipmentSummary;
            else if (hasHeight)
                return Localization.HeightOnlySummary;
            else if (hasEquipment)
                return Localization.EquipmentOnlySummary;
            else
                return Localization.EmptyConditionGroupsSummary;
        }

        private void ReorderCondition(int ruleIndex, int groupIndex, int fromIndex, int toIndex)
        {
            if (ruleIndex < 0 || ruleIndex >= ActiveRules.Count)
                return;
                
            var rule = ActiveRules[ruleIndex];
            if (rule.ConditionGroups == null || groupIndex < 0 || groupIndex >= rule.ConditionGroups.Count)
                return;
                
            var conditions = rule.ConditionGroups[groupIndex].Conditions;
            if (fromIndex < 0 || toIndex < 0 
                || fromIndex >= conditions.Count 
                || toIndex >= conditions.Count 
                || fromIndex == toIndex)
            {
                return;
            }

            var condition = conditions[fromIndex];
            conditions.RemoveAt(fromIndex);
            conditions.Insert(toIndex, condition);
            SaveConfig();
        }

        private void DrawHeightConditionEditor(int ruleIndex, int groupIndex, int conditionIndex, HeightCondition condition, string formatString)
        {
            ImGui.Text(Localization.HeightConditionLabel);
            ImGui.SameLine();
            
            // 比较运算符
            ImGui.SetNextItemWidth(52);
            var comparisonPreview = Localization.HeightComparisonSymbol(condition.Comparison);
            if (ImGui.BeginCombo($"##HeightComp{ruleIndex}_{groupIndex}_{conditionIndex}", comparisonPreview))
            {
                foreach (HeightComparison candidate in Enum.GetValues<HeightComparison>())
                {
                    if (ImGui.Selectable(
                            Localization.HeightComparisonSymbol(candidate),
                            candidate == condition.Comparison))
                    {
                        condition.Comparison = candidate;
                        SaveConfig();
                    }
                }
                ImGui.EndCombo();
            }
            
            ImGui.SameLine();
            
            // 高度值
            ImGui.SetNextItemWidth(72);
            var heightValue = condition.Value;
            if (SliderFloatWithManualInput(
                $"##HeightValue{ruleIndex}_{groupIndex}_{conditionIndex}",
                ref heightValue,
                -10f,
                10f,
                formatString,
                $"HeightValue{ruleIndex}_{groupIndex}_{conditionIndex}"))
            {
                condition.Value = heightValue;
                SaveConfig();
            }
            
            // 显示双击提示
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(Localization.DoubleClickToInput);
            }
        }

        private void DrawEquipmentConditionEditor(int ruleIndex, int groupIndex, int conditionIndex, EquipmentCondition condition)
        {
            ImGui.Text(Localization.ConditionTypeEquipment);
            ImGui.SameLine();
            
            // 装备槽位选择
            ImGui.SetNextItemWidth(120);
            var slotPreview = Localization.EquipSlotName(condition.Slot);
            if (ImGui.BeginCombo($"##EquipSlot{ruleIndex}_{groupIndex}_{conditionIndex}", slotPreview))
            {
                foreach (EquipSlot slot in Enum.GetValues<EquipSlot>())
                {
                    if (ImGui.Selectable(
                            Localization.EquipSlotName(slot),
                            slot == condition.Slot))
                    {
                        condition.Slot = slot;
                        SaveConfig();
                    }
                }
                ImGui.EndCombo();
            }
            
            ImGui.SameLine();
            
            // 必须装备/未装备切换
            ImGui.SetNextItemWidth(120);
            var equipStateLabel = condition.MustBeEquipped ? Localization.MustBeEquipped : Localization.MustNotBeEquipped;
            if (ImGui.BeginCombo($"##EquipState{ruleIndex}_{groupIndex}_{conditionIndex}", equipStateLabel))
            {
                if (ImGui.Selectable(Localization.MustBeEquipped, condition.MustBeEquipped))
                {
                    condition.MustBeEquipped = true;
                    SaveConfig();
                }
                
                if (ImGui.Selectable(Localization.MustNotBeEquipped, !condition.MustBeEquipped))
                {
                    condition.MustBeEquipped = false;
                    SaveConfig();
                }
                
                ImGui.EndCombo();
            }
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

        private Dictionary<int, string> GetPenumbraActionConflictHints(HeelsRule rule)
        {
            var conflicts = RulePenumbraActionAnalysis.Analyze(
                rule.Actions ?? [],
                (mod, option) => _penumbraInterop.GetOptionGroupType(mod, option));

            var hints = new Dictionary<int, string>();
            foreach (var (index, kind) in conflicts)
            {
                hints[index] = kind switch
                {
                    PenumbraActionConflictKind.ModEnableDisable => Localization.RulePenumbraModEnableDisableConflictHint,
                    PenumbraActionConflictKind.OptionSetting => Localization.RulePenumbraOptionConflictHint,
                    PenumbraActionConflictKind.MultiToggleSubOption => Localization.RulePenumbraMultiToggleConflictHint,
                    _ => "",
                };
            }

            return hints;
        }

        private void DrawRuleActionsList(int ruleIndex, HeelsRule rule)
        {
            EnsureRuleHasActions(ruleIndex);
            var actions = rule.Actions;
            var penumbraConflictHints = GetPenumbraActionConflictHints(rule);

            for (var actionIndex = 0; actionIndex < actions.Count; actionIndex++)
            {
                if (actionIndex > 0)
                {
                    ImGui.Spacing();
                    ImGui.Separator();
                    ImGui.Spacing();
                }

                ImGui.PushID(actionIndex);
                DrawRuleActionRow(
                    ruleIndex,
                    actionIndex,
                    actions[actionIndex],
                    actions.Count > 1,
                    penumbraConflictHints,
                    out var deleted);
                ImGui.PopID();
                if (deleted)
                    break;
            }

            if (ImGui.SmallButton($"{Localization.AddRuleAction}##Rule{ruleIndex}"))
            {
                actions.Add(CreateDefaultRuleAction());
                SaveConfig();
            }
        }

        private void DrawRuleActionRow(
            int ruleIndex,
            int actionIndex,
            HeelsRuleAction action,
            bool canDelete,
            IReadOnlyDictionary<int, string> penumbraConflictHints,
            out bool deleted)
        {
            deleted = false;
            
            var hasFeet = action.Type == ActionType.Glamourer && DoesGlamourerDesignHaveFeet(action.GlamourerDesign);
            var hasPenumbraConflict = penumbraConflictHints.TryGetValue(actionIndex, out var penumbraConflictHint);
            
            // 记录起始位置（用于绘制背景）
            var actionStartPos = ImGui.GetCursorScreenPos();
            var contentWidth = ImGui.GetContentRegionAvail().X;
            
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
            
            // 脚部装备红色 > Penumbra 冲突橙色 > 默认
            if (hasFeet)
                ImGui.TextColored(new Vector4(1.0f, 0.3f, 0.3f, 1.0f), Localization.RuleActionLabel(actionIndex + 1));
            else if (hasPenumbraConflict)
                ImGui.TextColored(RulePenumbraConflictWarningColor, Localization.RuleActionLabel(actionIndex + 1));
            else
                ImGui.TextDisabled(Localization.RuleActionLabel(actionIndex + 1));

            if (hasPenumbraConflict && ImGui.IsItemHovered())
                ImGui.SetTooltip(penumbraConflictHint);
            
            // 折叠时显示简要信息
            if (action.IsActionCollapsed)
            {
                ImGui.SameLine();
                
                // 类型图标和颜色
                var (typeIcon, typeColor, typeDesc) = action.Type switch
                {
                    ActionType.Penumbra => ("[P]", new Vector4(0.6f, 0.8f, 1.0f, 1.0f), Localization.ActionTypeLabel(ActionType.Penumbra)),
                    ActionType.Glamourer => ("[G]", new Vector4(1.0f, 0.6f, 0.8f, 1.0f), Localization.ActionTypeLabel(ActionType.Glamourer)),
                    ActionType.Moodles => ("[M]", new Vector4(0.8f, 1.0f, 0.6f, 1.0f), Localization.ActionTypeLabel(ActionType.Moodles)),
                    ActionType.Honorific => ("[H]", new Vector4(1.0f, 0.9f, 0.4f, 1.0f), Localization.ActionTypeLabel(ActionType.Honorific)),
                    _ => ("[?]", new Vector4(0.7f, 0.7f, 0.7f, 1.0f), "Unknown")
                };
                
                if (hasFeet)
                    typeColor = new Vector4(1.0f, 0.3f, 0.3f, 1.0f);
                else if (hasPenumbraConflict)
                    typeColor = RulePenumbraConflictWarningColor;
                
                // 显示类型图标（带颜色）
                ImGui.TextColored(typeColor, typeIcon);
                
                // 悬停显示类型说明
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.Text(typeDesc);
                    ImGui.EndTooltip();
                }
                
                ImGui.SameLine();
                
                var summary = GetActionSummary(action);
                
                if (hasFeet)
                    ImGui.TextColored(new Vector4(1.0f, 0.3f, 0.3f, 1.0f), summary);
                else if (hasPenumbraConflict)
                    ImGui.TextColored(RulePenumbraConflictWarningColor, summary);
                else
                    ImGui.TextColored(new Vector4(0.9f, 0.9f, 0.9f, 1.0f), summary);

                if (hasPenumbraConflict && ImGui.IsItemHovered())
                    ImGui.SetTooltip(penumbraConflictHint);
            }

            if (canDelete)
            {
                ImGui.SameLine();
                if (ImGui.SmallButton($"{Localization.DeleteRuleAction}##Rule{ruleIndex}Action{actionIndex}"))
                {
                    actionToDeleteRuleIndex = ruleIndex;
                    actionToDeleteIndex = actionIndex;
                    ImGui.OpenPopup("ConfirmDeleteAction");
                }
                
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(Localization.IsChine ? "删除此行动" : "Delete this action");
            }
            
            // 删除行动确认对话框（在返回前调用，避免被 action.IsActionCollapsed 跳过）
            DrawActionDeleteConfirmation(out var actionDeleted);
            if (actionDeleted)
            {
                deleted = true;
                return;
            }

            if (action.IsActionCollapsed)
            {
                DrawRuleActionWarningBackground(
                    actionStartPos,
                    contentWidth,
                    hasFeet,
                    hasPenumbraConflict);
                return;
            }

            ImGui.Indent();

            if (hasPenumbraConflict)
            {
                ImGui.TextColored(RulePenumbraConflictWarningColor, $"⚠ {penumbraConflictHint}");
                ImGui.Spacing();
            }
            
            DrawActionTypeSelector(ruleIndex, actionIndex, action);
            ImGui.Spacing();
            
            var idSuffix = $"R{ruleIndex}A{actionIndex}";
            
            switch (action.Type)
            {
                case ActionType.Glamourer:
                    ImGui.SetNextItemWidth(-1);
                    
                    // 如果包含脚部装备，给文字添加红色
                    if (hasFeet)
                        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.3f, 0.3f, 1.0f));
                    
                    DrawGlamourerDesignSelector(ruleIndex, actionIndex, action);
                    
                    if (hasFeet)
                        ImGui.PopStyleColor();
                    
                    // 如果包含脚部装备，显示警告
                    if (hasFeet)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.5f, 0.2f, 1.0f));
                        ImGui.TextWrapped(Localization.IsChine 
                            ? "⚠ 此设计包含脚部装备，可能会与 SimpleHeels 冲突" 
                            : "⚠ This design contains feet equipment, may conflict with SimpleHeels");
                        ImGui.PopStyleColor();
                    }
                    break;
                
                case ActionType.Penumbra:
                    if (!_penumbraInterop.IsIpcAvailable())
                    {
                        ImGui.TextDisabled(Localization.PenumbraListUnavailable);
                        break;
                    }
                    
                    // Penumbra 操作类型选择器
                    ImGui.Text(Localization.PenumbraActionTypeLabel);
                    ImGui.SetNextItemWidth(-1);
                    var penumbraActionTypes = new[] {
                        (Localization.PenumbraActionSetModOption, PenumbraActionKind.SetModOption),
                        (Localization.PenumbraActionEnableMod, PenumbraActionKind.EnableMod),
                        (Localization.PenumbraActionDisableMod, PenumbraActionKind.DisableMod)
                    };
                    var currentActionTypeDisplay = penumbraActionTypes
                        .FirstOrDefault(t => t.Item2 == action.PenumbraActionKind).Item1
                        ?? penumbraActionTypes[0].Item1;
                    if (ImGui.BeginCombo($"##PenumbraActionKind{idSuffix}", currentActionTypeDisplay))
                    {
                        foreach (var (display, kind) in penumbraActionTypes)
                        {
                            var isSelected = action.PenumbraActionKind == kind;
                            if (ImGui.Selectable(display, isSelected))
                            {
                                action.PenumbraActionKind = kind;
                                SaveConfig();
                            }
                        }
                        ImGui.EndCombo();
                    }
                    
                    ImGui.SetNextItemWidth(-1);
                    DrawPenumbraCollectionSelector(idSuffix, action);
                    ImGui.SetNextItemWidth(-1);
                    DrawPenumbraModSelector(idSuffix, action);
                    DrawPenumbraGlamourerTakeoverControls(action);
                    
                    // 只有在"设置 Mod 选项"模式下才显示选项相关的控件
                    if (action.PenumbraActionKind == PenumbraActionKind.SetModOption)
                    {
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
                    }
                    break;
                
                // case ActionType.CustomizePlus:  // 已移除 Customize+ 支持
                //     ImGui.SetNextItemWidth(-1);
                //     DrawCustomizePlusProfileSelector(idSuffix, action);
                //     break;
                    
                case ActionType.Honorific:
                    DrawHonorificActionEditor(ruleIndex, actionIndex, action, idSuffix);
                    break;
                    
                case ActionType.Moodles:
                    DrawMoodlesActionEditor(ruleIndex, actionIndex, action, idSuffix);
                    break;
            }
            
            ImGui.Unindent();
            
            DrawRuleActionWarningBackground(
                actionStartPos,
                contentWidth,
                hasFeet,
                hasPenumbraConflict);
        }

        private static void DrawRuleActionWarningBackground(
            Vector2 actionStartPos,
            float contentWidth,
            bool hasFeetWarning,
            bool hasPenumbraConflict)
        {
            if (!hasFeetWarning && !hasPenumbraConflict)
                return;

            var actionEndPos = ImGui.GetCursorScreenPos();
            var drawList = ImGui.GetWindowDrawList();
            var fillColor = hasFeetWarning
                ? new Vector4(0.4f, 0.0f, 0.0f, 0.3f)
                : new Vector4(0.45f, 0.2f, 0.0f, 0.28f);
            drawList.ChannelsSplit(2);
            drawList.ChannelsSetCurrent(0);
            drawList.AddRectFilled(
                actionStartPos,
                new Vector2(actionStartPos.X + contentWidth, actionEndPos.Y),
                ImGui.GetColorU32(fillColor),
                3.0f);
            if (hasPenumbraConflict)
            {
                drawList.AddRect(
                    actionStartPos,
                    new Vector2(actionStartPos.X + contentWidth, actionEndPos.Y),
                    ImGui.GetColorU32(RulePenumbraConflictWarningColor),
                    3.0f,
                    ImDrawFlags.None,
                    1.5f);
            }

            drawList.ChannelsMerge();
        }

        private bool DoesGlamourerDesignHaveFeet(string designNameOrGuid)
        {
            if (string.IsNullOrWhiteSpace(designNameOrGuid))
                return false;
            
            // 尝试解析为 GUID
            Guid guid;
            if (Guid.TryParse(designNameOrGuid, out guid))
            {
                // 已经是 GUID，直接检查
                var result = _glamourerInterop.DoesDesignApplyToFeet(guid);
                return result;
            }
            else
            {
                // 是设计名称，需要先转换为 GUID
                var guidFromName = _glamourerInterop.GetDesignGuidByName(designNameOrGuid);
                if (guidFromName.HasValue)
                {
                    var result = _glamourerInterop.DoesDesignApplyToFeet(guidFromName.Value);
                    return result;
                }
                else
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// 检查规则是否包含带有脚部装备警告的 Glamourer action
        /// </summary>
        private bool DoesRuleHaveFeetWarning(HeelsRule rule)
        {
            if (rule.Actions == null || rule.Actions.Count == 0)
                return false;

            foreach (var action in rule.Actions)
            {
                if (action.Type == ActionType.Glamourer && DoesGlamourerDesignHaveFeet(action.GlamourerDesign))
                    return true;
            }

            return false;
        }

        private void DrawHonorificActionEditor(int ruleIndex, int actionIndex, HeelsRuleAction action, string idSuffix)
        {
            if (!isHonorificIpcReady)
            {
                ImGui.TextDisabled(Localization.IsChine ? "Honorific 不可用" : "Honorific unavailable");
                return;
            }
            
            var localPlayer = ObjectTable.LocalPlayer;
            if (localPlayer == null)
            {
                ImGui.TextDisabled(Localization.IsChine ? "玩家未登录" : "Player not logged in");
                return;
            }
            
            // 获取称号列表
            var titleOptions = _honorificInterop.GetTitleOptions(localPlayer, force: false);
            
            ImGui.Text(Localization.IsChine ? "Honorific 称号:" : "Honorific Title:");
            
            // 当前选择的称号
            var currentTitle = string.IsNullOrWhiteSpace(action.HonorificTitleJson) 
                ? (Localization.IsChine ? "<无>" : "<None>")
                : ExtractTitleFromJson(action.HonorificTitleJson);
            
            ImGui.SetNextItemWidth(-1);
            if (ImGui.BeginCombo($"##HonorificTitle{idSuffix}", currentTitle))
            {
                // "无" 选项
                if (ImGui.Selectable(Localization.IsChine ? "<无>" : "<None>", string.IsNullOrWhiteSpace(action.HonorificTitleJson)))
                {
                    action.HonorificTitleJson = "";
                    SaveConfig();
                }
                
                // 称号列表
                if (titleOptions.Count == 0)
                {
                    ImGui.TextDisabled(Localization.IsChine ? "无可用称号" : "No titles available");
                }
                else
                {
                    foreach (var option in titleOptions)
                    {
                        var isSelected = action.HonorificTitleJson == option.Json;
                        if (ImGui.Selectable(option.Display, isSelected))
                        {
                            action.HonorificTitleJson = option.Json;
                            SaveConfig();
                        }
                    }
                }
                
                ImGui.EndCombo();
            }
            
            // 刷新按钮
            ImGui.SameLine();
            if (ImGui.SmallButton($"{(Localization.IsChine ? "刷新" : "Refresh")}##RefreshHonorific{idSuffix}"))
            {
                _honorificInterop.RefreshTitleList(localPlayer, force: true);
            }
        }

        private string ExtractTitleFromJson(string json)
        {
            try
            {
                var obj = JObject.Parse(json);
                return obj["Title"]?.ToString() ?? json.Substring(0, Math.Min(30, json.Length));
            }
            catch
            {
                return json.Substring(0, Math.Min(30, json.Length));
            }
        }

        private void DrawMoodlesActionEditor(int ruleIndex, int actionIndex, HeelsRuleAction action, string idSuffix)
        {
            if (!isMoodlesIpcReady)
            {
                ImGui.TextDisabled(Localization.IsChine ? "Moodles 不可用" : "Moodles unavailable");
                return;
            }
            
            // 获取 Moodles 列表
            var moodleItems = _moodlesInterop.GetItems(force: false);
            
            ImGui.Text(Localization.IsChine ? "Moodles 状态/预设:" : "Moodles Status/Preset:");
            
            // 当前选择
            var currentDisplay = string.IsNullOrWhiteSpace(action.MoodleGuid)
                ? (Localization.IsChine ? "<无>" : "<None>")
                : GetMoodleDisplayName(action.MoodleGuid, action.MoodleIsPreset, moodleItems);
            
            ImGui.SetNextItemWidth(-1);
            if (ImGui.BeginCombo($"##MoodleSelect{idSuffix}", currentDisplay))
            {
                // "无" 选项
                if (ImGui.Selectable(Localization.IsChine ? "<无>" : "<None>", string.IsNullOrWhiteSpace(action.MoodleGuid)))
                {
                    action.MoodleGuid = "";
                    action.MoodleIsPreset = false;
                    SaveConfig();
                }
                
                // Moodles 列表
                if (moodleItems.Count == 0)
                {
                    ImGui.TextDisabled(Localization.IsChine ? "无可用 Moodles" : "No moodles available");
                }
                else
                {
                    // 先显示 Status，再显示 Preset
                    var statuses = moodleItems.Where(m => !m.IsPreset).ToList();
                    var presets = moodleItems.Where(m => m.IsPreset).ToList();
                    
                    if (statuses.Any())
                    {
                        ImGui.TextDisabled(Localization.IsChine ? "--- 状态 ---" : "--- Status ---");
                        foreach (var item in statuses)
                        {
                            var isSelected = action.MoodleGuid == item.Id.ToString() && !action.MoodleIsPreset;
                            var label = $"{item.Title}";
                            if (ImGui.Selectable(label, isSelected))
                            {
                                action.MoodleGuid = item.Id.ToString();
                                action.MoodleIsPreset = false;
                                SaveConfig();
                            }
                        }
                    }
                    
                    if (presets.Any())
                    {
                        if (statuses.Any())
                            ImGui.Spacing();
                        ImGui.TextDisabled(Localization.IsChine ? "--- 预设 ---" : "--- Presets ---");
                        foreach (var item in presets)
                        {
                            var isSelected = action.MoodleGuid == item.Id.ToString() && action.MoodleIsPreset;
                            var label = $"{item.Title}";
                            if (ImGui.Selectable(label, isSelected))
                            {
                                action.MoodleGuid = item.Id.ToString();
                                action.MoodleIsPreset = true;
                                SaveConfig();
                            }
                        }
                    }
                }
                
                ImGui.EndCombo();
            }
            
            // 刷新按钮
            ImGui.SameLine();
            if (ImGui.SmallButton($"{(Localization.IsChine ? "刷新" : "Refresh")}##RefreshMoodles{idSuffix}"))
            {
                _moodlesInterop.RefreshList(force: true);
            }
        }

        private string GetMoodleDisplayName(string guid, bool isPreset, IReadOnlyList<MoodleListItem> items)
        {
            if (Guid.TryParse(guid, out var id))
            {
                var item = items.FirstOrDefault(m => m.Id == id && m.IsPreset == isPreset);
                if (item != null)
                {
                    var typeLabel = isPreset ? (Localization.IsChine ? "预设" : "Preset") : (Localization.IsChine ? "状态" : "Status");
                    return $"[{typeLabel}] {item.Title}";
                }
            }
            
            var type = isPreset ? "Preset" : "Status";
            return $"[{type}] {guid.Substring(0, Math.Min(8, guid.Length))}";
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

        private void DrawActionTypeSelector(int ruleIndex, int actionIndex, HeelsRuleAction action)
        {
            ImGui.SetNextItemWidth(150);
            var currentTypeLabel = Localization.ActionTypeLabel(action.Type);
            if (ImGui.BeginCombo($"##ActionType{ruleIndex}_{actionIndex}", currentTypeLabel))
            {
                if (ImGui.Selectable(Localization.ActionTypeLabel(ActionType.Glamourer), action.Type == ActionType.Glamourer))
                {
                    action.Type = ActionType.Glamourer;
                    SaveConfig();
                }
                if (ImGui.Selectable(Localization.ActionTypeLabel(ActionType.Penumbra), action.Type == ActionType.Penumbra))
                {
                    action.Type = ActionType.Penumbra;
                    SaveConfig();
                }
                // if (ImGui.Selectable(Localization.ActionTypeLabel(ActionType.CustomizePlus), action.Type == ActionType.CustomizePlus))  // 已移除
                // {
                //     action.Type = ActionType.CustomizePlus;
                //     SaveConfig();
                // }
                if (ImGui.Selectable(Localization.ActionTypeLabel(ActionType.Honorific), action.Type == ActionType.Honorific))
                {
                    action.Type = ActionType.Honorific;
                    SaveConfig();
                }
                if (ImGui.Selectable(Localization.ActionTypeLabel(ActionType.Moodles), action.Type == ActionType.Moodles))
                {
                    action.Type = ActionType.Moodles;
                    SaveConfig();
                }
                ImGui.EndCombo();
            }
            ImGui.SameLine();
            ImGui.TextDisabled(Localization.ActionTypeText);
        }

        private void DrawCustomizePlusProfileSelector(string idSuffix, HeelsRuleAction action)
        {
            // Customize+ 支持已移除
            ImGui.TextColored(new Vector4(1.0f, 0.5f, 0.3f, 1.0f), 
                Localization.IsChine ? "Customize+ 支持已移除" : "Customize+ support removed");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(Localization.IsChine 
                    ? "Customize+ 不提供所需的 IPC 方法，暂时无法支持。\n如需使用 Customize+ 配置，请直接在 Customize+ 插件中管理。" 
                    : "Customize+ does not provide required IPC methods.\nPlease manage profiles directly in Customize+ plugin.");
            }
        }

        /// <summary>
        /// 获取 Action 的简要摘要（用于折叠显示）
        /// </summary>
        private string GetActionSummary(HeelsRuleAction action)
        {
            switch (action.Type)
            {
                case ActionType.Glamourer:
                    if (!string.IsNullOrWhiteSpace(action.GlamourerDesign))
                    {
                        // 显示最后 8 位字符
                        var guid = action.GlamourerDesign.Trim();
                        if (guid.Length > 8)
                            return $"[...{guid.Substring(guid.Length - 8)}]";
                        return $"[{guid}]";
                    }
                    break;
                    
                case ActionType.Penumbra:
                    var parts = new List<string>();
                    
                    // 第一条：Mod 名称
                    if (!string.IsNullOrWhiteSpace(action.PenumbraModName))
                    {
                        parts.Add(action.PenumbraModName);
                    }
                    
                    // 如果是启用/禁用 Mod 操作，显示操作类型
                    if (action.PenumbraActionKind == PenumbraActionKind.EnableMod)
                    {
                        parts.Add(Localization.PenumbraEnableShort);
                    }
                    else if (action.PenumbraActionKind == PenumbraActionKind.DisableMod)
                    {
                        parts.Add(Localization.PenumbraDisableShort);
                    }
                    // 否则显示 Option 信息
                    else if (!string.IsNullOrWhiteSpace(action.PenumbraOptionName))
                    {
                        parts.Add(action.PenumbraOptionName);
                    }
                    else if (action.PenumbraMultiToggleStates.Any())
                    {
                        // 显示第一个启用的选项
                        var firstEnabled = action.PenumbraMultiToggleStates
                            .Where(kv => kv.Value)
                            .Select(kv => kv.Key)
                            .FirstOrDefault();
                        if (!string.IsNullOrWhiteSpace(firstEnabled))
                            parts.Add(firstEnabled);
                    }
                    
                    if (parts.Count >= 2)
                        return $"[{parts[0]} → {parts[1]}]";
                    else if (parts.Count == 1)
                        return $"[{parts[0]}]";
                    break;
                    
                // case ActionType.CustomizePlus:  // 已移除 Customize+ 支持
                //     if (!string.IsNullOrWhiteSpace(action.CustomizePlusProfile))
                //         return $"[{action.CustomizePlusProfile}]";
                //     break;
                    
                case ActionType.Honorific:
                    if (!string.IsNullOrWhiteSpace(action.HonorificTitleJson))
                    {
                        var title = ExtractTitleFromJson(action.HonorificTitleJson);
                        return $"[{title}]";
                    }
                    break;
                    
                case ActionType.Moodles:
                    if (!string.IsNullOrWhiteSpace(action.MoodleGuid))
                    {
                        var moodleItems = _moodlesInterop.GetItems(force: false);
                        var displayName = GetMoodleDisplayName(action.MoodleGuid, action.MoodleIsPreset, moodleItems);
                        // 移除 [类型] 前缀，只显示名称
                        var cleanName = displayName;
                        var bracketEnd = displayName.IndexOf(']');
                        if (bracketEnd > 0 && bracketEnd < displayName.Length - 1)
                        {
                            cleanName = displayName.Substring(bracketEnd + 1).Trim();
                        }
                        
                        var typeLabel = action.MoodleIsPreset ? "Preset" : "Status";
                        return $"[{typeLabel} → {cleanName}]";
                    }
                    break;
            }
            
            return "[未配置]";
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
            
            // 调试日志 - 查看实际值
            
            var hasFeet = DoesGlamourerDesignHaveFeet(name);
            
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

            // 如果当前选择的设计包含脚部装备，下拉框背景变红色调（preview 文字保持默认颜色）
            if (hasFeet)
            {
                // 下拉菜单背景色
                ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.5f, 0.15f, 0.15f, 1.0f));
                // 下拉菜单悬停背景色
                ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(0.6f, 0.2f, 0.2f, 1.0f));
                // 下拉菜单激活背景色
                ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(0.7f, 0.25f, 0.25f, 1.0f));
            }

            bool comboOpened = ImGui.BeginCombo($"##DesignCombo{ruleIndex}A{actionIndex}", preview);
            if (comboOpened)
            {
                if (designNames.Count == 0)
                {
                    ImGui.TextDisabled(Localization.GlamourerDesignListEmpty);
                }
                else
                {
                    // "选择 Glamourer 设计..." 选项（默认白色）
                    bool isSelected = string.IsNullOrWhiteSpace(name);
                    if (ImGui.Selectable(Localization.SelectGlamourerDesign, isSelected))
                    {
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            _glamourerInterop.InvalidateCache(name);
                            action.GlamourerDesign = "";
                            SaveConfig();
                        }
                    }

                    // 遍历所有设计
                    foreach (var listedName in designNames)
                    {
                        // 检查此设计是否包含脚部装备
                        var designHasFeet = _glamourerInterop.DesignAppliesFeet(listedName) == true;
                        isSelected = string.Equals(listedName, name, StringComparison.OrdinalIgnoreCase);
                        
                        // 为每个选项设置颜色（红色或白色）
                        var textColor = designHasFeet 
                            ? new Vector4(1.0f, 0.3f, 0.3f, 1.0f)  // 红色
                            : new Vector4(1.0f, 1.0f, 1.0f, 1.0f); // 白色
                        ImGui.PushStyleColor(ImGuiCol.Text, textColor);
                        
                        if (ImGui.Selectable(listedName, isSelected))
                        {
                            if (!isSelected)
                            {
                                _glamourerInterop.InvalidateCache(name);
                                action.GlamourerDesign = listedName;
                                SaveConfig();
                            }
                        }
                        
                        // 立即恢复颜色
                        ImGui.PopStyleColor();
                    }
                }

                ImGui.EndCombo();
            }
            
            if (hasFeet)
            {
                // 恢复 3 个颜色设置（FrameBg, FrameBgHovered, FrameBgActive）
                ImGui.PopStyleColor(3);
            }

            DrawGlamourerFeetApplyMark(name);
        }

        private bool AnyRuleShowsFeetEquipmentWarning()
        {
            return ActiveRules.Any(rule =>
                GetRuleActions(rule).Any(action =>
                    action.Type == ActionType.Glamourer
                    && !string.IsNullOrWhiteSpace(action.GlamourerDesign)
                    && _glamourerInterop.DesignAppliesFeet(action.GlamourerDesign) == true));
        }

        private string BuildFeetWarningSignature()
        {
            var parts = new List<string>();
            var activeRules = ActiveRules;
            for (int i = 0; i < activeRules.Count; i++)
            {
                var actionIndex = 0;
                foreach (var action in GetRuleActions(activeRules[i]))
                {
                    var design = action.GlamourerDesign ?? "";
                    if (action.Type == ActionType.Glamourer
                        && !string.IsNullOrWhiteSpace(design)
                        && _glamourerInterop.DesignAppliesFeet(design) == true)
                    {
                        parts.Add($"{i}:{actionIndex}:{design}");
                    }

                    actionIndex++;
                }
            }

            return string.Join("|", parts);
        }

        /// <summary>
        /// 双击滑条以手动输入值的辅助方法（float版本）
        /// </summary>
        private bool SliderFloatWithManualInput(string label, ref float value, float min, float max, string format, string inputId)
        {
            bool changed = false;
            
            // 如果当前是输入模式
            if (_activeInputId == inputId)
            {
                ImGui.SetNextItemWidth(ImGui.CalcTextSize(label).X + 200);
                if (_shouldFocusInput)
                {
                    ImGui.SetKeyboardFocusHere();
                    _shouldFocusInput = false;
                }
                
                if (ImGui.InputText($"##{inputId}_input", ref _inputBuffer, 32, ImGuiInputTextFlags.EnterReturnsTrue))
                {
                    if (float.TryParse(_inputBuffer, out var parsedValue))
                    {
                        value = Math.Clamp(parsedValue, min, max);
                        changed = true;
                    }
                    _activeInputId = null;
                }
                
                // ESC 退出输入模式
                if (ImGui.IsKeyPressed(ImGuiKey.Escape))
                {
                    _activeInputId = null;
                }
                // 只有在输入框曾经激活过后失去焦点时才退出（避免第一帧就退出）
                else if (ImGui.IsItemDeactivated())
                {
                    _activeInputId = null;
                }
                
                ImGui.SameLine();
                ImGui.TextDisabled(label);
            }
            else
            {
                // 正常滑条模式
                if (ImGui.SliderFloat(label, ref value, min, max, format))
                {
                    changed = true;
                }
                
                // 检测双击或 Ctrl+点击
                var io = ImGui.GetIO();
                bool isCtrlPressed = io.KeyCtrl || ImGui.IsKeyDown(ImGuiKey.LeftCtrl) || ImGui.IsKeyDown(ImGuiKey.RightCtrl);
                bool isHovered = ImGui.IsItemHovered();
                bool isDoubleClicked = ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left);
                bool isSingleClickedWithCtrl = ImGui.IsMouseClicked(ImGuiMouseButton.Left) && isCtrlPressed;
                
                if (isHovered && (isDoubleClicked || isSingleClickedWithCtrl))
                {
                    _activeInputId = inputId;
                    _inputBuffer = value.ToString("F3");
                    _shouldFocusInput = true;
                }
            }
            
            return changed;
        }

        /// <summary>
        /// 双击滑条以手动输入值的辅助方法（int版本）
        /// </summary>
        private bool SliderIntWithManualInput(string label, ref int value, int min, int max, string inputId)
        {
            bool changed = false;
            
            // 如果当前是输入模式
            if (_activeInputId == inputId)
            {
                ImGui.SetNextItemWidth(ImGui.CalcTextSize(label).X + 200);
                if (_shouldFocusInput)
                {
                    ImGui.SetKeyboardFocusHere();
                    _shouldFocusInput = false;
                }
                
                if (ImGui.InputText($"##{inputId}_input", ref _inputBuffer, 32, ImGuiInputTextFlags.EnterReturnsTrue))
                {
                    if (int.TryParse(_inputBuffer, out var parsedValue))
                    {
                        value = Math.Clamp(parsedValue, min, max);
                        changed = true;
                    }
                    _activeInputId = null;
                }
                
                // ESC 退出输入模式
                if (ImGui.IsKeyPressed(ImGuiKey.Escape))
                {
                    _activeInputId = null;
                }
                // 只有在输入框曾经激活过后失去焦点时才退出（避免第一帧就退出）
                else if (ImGui.IsItemDeactivated())
                {
                    _activeInputId = null;
                }
                
                ImGui.SameLine();
                ImGui.TextDisabled(label);
            }
            else
            {
                // 正常滑条模式
                if (ImGui.SliderInt(label, ref value, min, max))
                {
                    changed = true;
                }
                
                // 检测双击或 Ctrl+点击
                var io = ImGui.GetIO();
                bool isCtrlPressed = io.KeyCtrl || ImGui.IsKeyDown(ImGuiKey.LeftCtrl) || ImGui.IsKeyDown(ImGuiKey.RightCtrl);
                bool shouldActivateInput = (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) ||
                    (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && isCtrlPressed);
                
                if (shouldActivateInput)
                {
                    _activeInputId = inputId;
                    _inputBuffer = value.ToString();
                    _shouldFocusInput = true;
                }
            }
            
            return changed;
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
        
        /// <summary>
        /// 通过 Model ID 查找物品名称（简化版本 - 暂时返回null，后续优化）
        /// </summary>
        private string? GetItemNameByModelId(ushort modelId, byte variant, int equipSlot)
        {
            // TODO: Model ID 到 Item Name 的映射比较复杂
            // 需要更深入研究 Lumina 的 Item 数据结构
            // 暂时返回 null，只显示 Model ID
            return null;
        }

        private void DrawDebugTab()
        {
            // 错误信息
            if (!string.IsNullOrEmpty(lastError))
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.3f, 0.3f, 1.0f));
                ImGui.TextWrapped($"{Localization.ErrorInfo}: {lastError}");
                ImGui.PopStyleColor();
                ImGui.Separator();
            }
            
            // IPC 测试
            if (ImGui.CollapsingHeader(Localization.IsChine ? "IPC 测试" : "IPC Test"))
            {
                ImGui.Indent();
                if (ImGui.Button(Localization.TestIPC))
                {
                    try
                    {
                        var provider = PluginInterface.GetIpcSubscriber<string>("SimpleHeels.GetLocalPlayer");
                        lastIpcData = provider.InvokeFunc();
                    }
                    catch (Exception ex)
                    {
                        lastError = $"{Localization.TestIPC} {Localization.ErrorInfo}: {ex.Message}";
                        PluginLog.Error($"IPC test failed: {ex}");
                    }
                }
                
                if (!string.IsNullOrEmpty(lastIpcData))
                {
                    ImGui.Spacing();
                    if (ImGui.CollapsingHeader(Localization.RawIPCData))
                        ImGui.TextWrapped(lastIpcData);
                }
                ImGui.Unindent();
            }
            
            // 配置文件位置信息
            if (ImGui.CollapsingHeader(Localization.IsChine ? "配置文件位置" : "Config File Location"))
            {
                ImGui.Indent();
                
                try
                {
                    var configDir = PluginInterface.GetPluginConfigDirectory();
                    ImGui.TextWrapped($"Config Directory: {configDir}");
                    
                    // 尝试构造 pluginConfigs 路径
                    var pluginsParentDir = Directory.GetParent(configDir)?.FullName;
                    if (!string.IsNullOrEmpty(pluginsParentDir))
                    {
                        var pluginConfigsDir = Path.Combine(Directory.GetParent(pluginsParentDir)?.FullName ?? "", "pluginConfigs");
                        var expectedConfigPath = Path.Combine(pluginConfigsDir, "HeelsDesignLinker.json");
                        var oldConfigPath = Path.Combine(pluginConfigsDir, "HeelsToggle.json");
                        
                        ImGui.TextWrapped($"Expected Config: {expectedConfigPath}");
                        ImGui.SameLine();
                        if (File.Exists(expectedConfigPath))
                            ImGui.TextColored(new Vector4(0.3f, 1.0f, 0.3f, 1.0f), "✓ Exists");
                        else
                            ImGui.TextColored(new Vector4(1.0f, 0.5f, 0.3f, 1.0f), "✗ Not Found");
                        
                        if (File.Exists(oldConfigPath))
                        {
                            ImGui.TextWrapped($"Old Config: {oldConfigPath}");
                            ImGui.SameLine();
                            ImGui.TextColored(new Vector4(1.0f, 1.0f, 0.3f, 1.0f), "! Found (needs migration)");
                            
                            // 手动迁移按钮
                            if (ImGui.Button(Localization.IsChine ? "手动迁移配置" : "Migrate Config Manually"))
                            {
                                try
                                {
                                    File.Copy(oldConfigPath, expectedConfigPath, overwrite: true);
                                    var backupPath = oldConfigPath + ".migrated_backup";
                                    if (!File.Exists(backupPath))
                                        File.Move(oldConfigPath, backupPath);
                                    else
                                        File.Delete(oldConfigPath);
                                }
                                catch (Exception migrateEx)
                                {
                                    PluginLog.Error($"[HeelsDesignLinker] Manual migration failed: {migrateEx.Message}");
                                }
                            }
                        }
                        else if (!File.Exists(expectedConfigPath))
                        {
                            // 如果新配置也不存在，显示提示
                            ImGui.TextColored(new Vector4(1.0f, 0.5f, 0.3f, 1.0f), 
                                Localization.IsChine ? "警告：未找到任何配置文件" : "Warning: No config file found");
                        }
                    }
                }
                catch (Exception ex)
                {
                    ImGui.TextColored(new Vector4(1.0f, 0.3f, 0.3f, 1.0f), $"Error: {ex.Message}");
                }
                
                ImGui.Unindent();
            }
            
            // 插件状态信息
            if (ImGui.CollapsingHeader(Localization.IsChine ? "插件状态" : "Plugin Status", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Indent();
                
                var simpleHeelsStatus = !isSimpleHeelsAvailable
                    ? $"✗ {Localization.NotAvailable}"
                    : isSimpleHeelsIpcReady
                        ? $"✓ {Localization.Available}"
                        : (Localization.IsChine ? "△ 已加载 / IPC 等待中" : "△ Loaded / IPC pending");
                ImGui.Text($"{Localization.SimpleHeelsStatus}: {simpleHeelsStatus}");
                
                if (ConfigurationUsesGlamourer())
                {
                    ImGui.Text($"{Localization.GlamourerStatus}: {(isGlamourerAvailable ? $"✓ {Localization.Available}" : $"✗ {Localization.NotAvailable}")}");
                    
                    // 显示 Glamourer 事件计数
                    if (isGlamourerAvailable)
                    {
                        ImGui.Indent();
                        
                        // API 版本信息
                        ImGui.TextColored(new Vector4(0.3f, 0.8f, 1.0f, 1.0f), 
                            $"API Version: {_glamourerInterop.GlamourerApiVersion}");
                        
                        // 可用方法列表
                        if (ImGui.CollapsingHeader("Available IPC Methods"))
                        {
                            foreach (var method in _glamourerInterop.AvailableGetStateMethods)
                            {
                                var color = method.StartsWith("✓") 
                                    ? new Vector4(0.3f, 1.0f, 0.3f, 1.0f)
                                    : new Vector4(0.7f, 0.7f, 0.7f, 1.0f);
                                ImGui.TextColored(color, method);
                            }
                        }
                        
                        // 事件订阅状态
                        if (ImGui.CollapsingHeader("Event Subscription Status"))
                        {
                            foreach (var status in _glamourerInterop.EventSubscriptionStatus)
                            {
                                var color = status.StartsWith("✓") 
                                    ? new Vector4(0.3f, 1.0f, 0.3f, 1.0f)
                                    : new Vector4(1.0f, 0.5f, 0.3f, 1.0f);
                                ImGui.TextColored(color, status);
                            }
                        }
                        
                        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1.0f), 
                            $"Event counts: StateChanged.V2={_glamourerInterop.StateChangedEventCount}, " +
                            $"WithType={_glamourerInterop.StateChangedWithTypeEventCount}, " +
                            $"Finalized={_glamourerInterop.StateFinalizedEventCount}, " +
                            $"GPose={_glamourerInterop.GPoseChangedEventCount}");
                        
                        // 显示 GetState 调试信息
                        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1.0f), "GetState attempts:");
                        foreach (var attempt in _glamourerInterop.GetStateAttempts)
                        {
                            var color = attempt.StartsWith("✓") 
                                ? new Vector4(0.3f, 1.0f, 0.3f, 1.0f)
                                : new Vector4(1.0f, 0.5f, 0.3f, 1.0f);
                            ImGui.TextColored(color, $"  {attempt}");
                        }
                        if (_glamourerInterop.GetStateAttempts.Count == 0)
                        {
                            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1.0f), "  (Not attempted yet)");
                        }
                        
                        ImGui.Unindent();
                    }
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
                
                ImGui.Unindent();
            }
            
            // 最后执行的行动信息
            if (ImGui.CollapsingHeader(Localization.IsChine ? "最后执行的行动" : "Last Executed Actions", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Indent();
                
                var lastAppliedDisplay = "-";
                if (lastExecutedRuleIndex >= 0 && lastExecutedActionUtc.HasValue && lastExecutedActionSummaries.Count > 0)
                {
                    var timeSinceExecuted = DateTime.UtcNow - lastExecutedActionUtc.Value;
                    var timeStr = timeSinceExecuted.TotalSeconds < 60 
                        ? $"{(int)timeSinceExecuted.TotalSeconds}s ago" 
                        : $"{(int)timeSinceExecuted.TotalMinutes}m ago";
                    lastAppliedDisplay = $"Rule {lastExecutedRuleIndex + 1} ({timeStr}): {string.Join(", ", lastExecutedActionSummaries)}";
                }
                
                ImGui.TextWrapped($"{Localization.LastAppliedPrimary}: {lastAppliedDisplay}");
                ImGui.Text($"{Localization.LastAppliedHonorific}: {(string.IsNullOrEmpty(lastAppliedHonorificJson) ? "-" : Localization.HonorificApplied)}");
                ImGui.Text($"{Localization.LastAppliedMoodle}: {(string.IsNullOrEmpty(lastAppliedMoodleKey) ? "-" : lastAppliedMoodleKey)}");
                ImGui.Text($"{Localization.ApplyGate}: {applyGateStatus}");
                
                ImGui.Unindent();
            }
            
            // 诊断信息
            if (ImGui.CollapsingHeader(Localization.IsChine ? "诊断信息" : "Diagnostic Info"))
            {
                ImGui.Indent();
                
                ImGui.Text($"lastMatchedRuleIndex: {lastMatchedRuleIndex}");
                ImGui.Text($"stableTrackingRuleIndex: {stableTrackingRuleIndex}");
                ImGui.Text($"currentMatchedRuleIndex: {currentMatchedRuleIndex}");
                ImGui.Text($"lastAppliedActionKeys.Count: {lastAppliedActionKeys.Count} (for dedup)");
                
                // 稳定性和冷却诊断
                if (ruleMatchStableSinceUtc.HasValue)
                {
                    var stableElapsed = (DateTime.UtcNow - ruleMatchStableSinceUtc.Value).TotalSeconds;
                    var stableRequired = Configuration.RuleMatchStableSeconds;
                    ImGui.Text($"Stability: {stableElapsed:F1}s / {stableRequired:F1}s required");
                }
                else
                {
                    ImGui.Text("Stability: waiting for rule match");
                }
                
                if (lastApplyUtc != DateTime.MinValue)
                {
                    var cooldownElapsed = (DateTime.UtcNow - lastApplyUtc).TotalSeconds;
                    var cooldownRequired = Configuration.ApplyCooldownSeconds;
                    ImGui.Text($"Cooldown: {cooldownElapsed:F1}s / {cooldownRequired:F1}s required");
                }
                else
                {
                    ImGui.Text("Cooldown: no previous apply");
                }
                
                ImGui.Unindent();
            }
            
            // 装备槽状态 - 只要 Glamourer 可用就显示，用于调试
            if (isGlamourerAvailable)
            {
                if (ImGui.CollapsingHeader(Localization.GlamourerEquipmentStatus))
                {
                    ImGui.Indent();
                    DrawGlamourerEquipmentStatus();
                    ImGui.Unindent();
                }
            }
        }
        
        private void DrawGlamourerEquipmentStatus()
        {
            if (!isGlamourerAvailable || _glamourerInterop == null)
            {
                ImGui.TextDisabled(Localization.EquipmentStatusUnavailable);
                return;
            }
            
            // 强制刷新按钮
            if (ImGui.Button(Localization.DebugForceRefreshGlamourer))
            {
                _glamourerInterop.GetLocalPlayerState(forceRefresh: true);
            }
            ImGui.SameLine();
            var lastUpdateText = _glamourerInterop.GetLocalPlayerState() == null 
                ? Localization.DebugLastUpdateNever 
                : Localization.DebugLastUpdateJustNow;
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1.0f), 
                $"({Localization.DebugLastUpdate}: {lastUpdateText})");
            
            ImGui.Separator();
            
            // 获取 Glamourer 状态
            var glamourerState = _glamourerInterop.GetLocalPlayerState();
            if (glamourerState == null)
            {
                ImGui.TextColored(new Vector4(1.0f, 0.5f, 0.3f, 1.0f), Localization.DebugGlamourerStateNull);
                return;
            }
            
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.3f, 1.0f, 0.3f, 1.0f));
            ImGui.TextWrapped(Localization.DebugShowingInventoryEquipment);
            ImGui.PopStyleColor();
            ImGui.Text(Localization.DebugColorLegend);
            ImGui.Text(Localization.DebugGlamourerDataNote);
            ImGui.Separator();
            
            var localPlayer = ObjectTable?.LocalPlayer;
            if (localPlayer == null)
            {
                ImGui.TextColored(new Vector4(1.0f, 0.5f, 0.3f, 1.0f), Localization.DebugLocalPlayerUnavailable);
                return;
            }
            
            // 获取 InventoryManager 和 Character
            unsafe
            {
                var character = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)localPlayer.Address;
                if (character == null)
                {
                    ImGui.TextColored(new Vector4(1.0f, 0.5f, 0.3f, 1.0f), Localization.DebugCharacterDataUnavailable);
                    return;
                }
                
                var inventoryManager = FFXIVClientStructs.FFXIV.Client.Game.InventoryManager.Instance();
                if (inventoryManager == null)
                {
                    ImGui.TextColored(new Vector4(1.0f, 0.5f, 0.3f, 1.0f), Localization.DebugInventoryManagerUnavailable);
                    return;
                }
                
                var container = inventoryManager->GetInventoryContainer(FFXIVClientStructs.FFXIV.Client.Game.InventoryType.EquippedItems);
                if (container == null)
                {
                    ImGui.TextColored(new Vector4(1.0f, 0.5f, 0.3f, 1.0f), Localization.DebugEquipmentContainerUnavailable);
                    return;
                }
                
                // 显示所有装备槽位的 Glamourer 数据
                foreach (EquipSlot slot in Enum.GetValues<EquipSlot>())
                {
                    var slotName = Localization.EquipSlotName(slot);
                    
                    // 获取 Glamourer Equipment 数据
                    string glamourerSlotKey = slot switch
                    {
                        EquipSlot.MainHand => "MainHand",
                        EquipSlot.OffHand => "OffHand",
                        EquipSlot.Head => "Head",
                        EquipSlot.Body => "Body",
                        EquipSlot.Hands => "Hands",
                        EquipSlot.Legs => "Legs",
                        EquipSlot.Feet => "Feet",
                        EquipSlot.Ears => "Ears",
                        EquipSlot.Neck => "Neck",
                        EquipSlot.Wrists => "Wrists",
                        EquipSlot.RFinger => "RFinger",
                        EquipSlot.LFinger => "LFinger",
                        _ => null
                    };
                    
                    var equipSlotIndex = slot switch
                    {
                        EquipSlot.MainHand => 0,
                        EquipSlot.OffHand => 1,
                        EquipSlot.Head => 2,
                        EquipSlot.Body => 3,
                        EquipSlot.Hands => 4,
                        EquipSlot.Legs => 6,
                        EquipSlot.Feet => 7,
                        EquipSlot.Ears => 8,
                        EquipSlot.Neck => 9,
                        EquipSlot.Wrists => 10,
                        EquipSlot.RFinger => 11,
                        EquipSlot.LFinger => 12,
                        _ => -1
                    };
                    
                    if (glamourerSlotKey == null || equipSlotIndex < 0)
                        continue;
                    
                    try
                    {
                        var equipmentData = glamourerState.SelectToken($"Equipment.{glamourerSlotKey}");
                        if (equipmentData != null)
                        {
                            var glamItemId = equipmentData.Value<uint>("ItemId");
                            var modelId = equipmentData.Value<ushort>("ModelId");
                            var variant = equipmentData.Value<byte>("Variant");
                            var stain0 = equipmentData.Value<byte>("Stain0");
                            var stain1 = equipmentData.Value<byte>("Stain1");
                            var apply = equipmentData.Value<bool>("Apply");
                            
                            // 获取实际装备的 ItemId（背包）
                            var item = container->GetInventorySlot(equipSlotIndex);
                            var actualItemId = item != null ? item->ItemId : 0u;
                            
                            // 判断是否为特殊物品（检查背包的 ItemId）
                            var isSpecialItem = EmperorsNewItems.IsEmperorsNewByItemId(actualItemId);
                            
                            // 状态颜色和图标
                            Vector4 statusColor;
                            string statusIcon;
                            string statusText;
                            
                            if (isSpecialItem)
                            {
                                statusColor = new Vector4(1.0f, 0.5f, 0.0f, 1.0f); // 橙色 - 特殊物品
                                statusIcon = "⚠"; // 警告符号 - 特殊物品
                                statusText = Localization.DebugSpecialItem;
                            }
                            else if (actualItemId > 0)
                            {
                                statusColor = new Vector4(0.7f, 0.7f, 1.0f, 1.0f); // 蓝色 - 正常装备
                                statusIcon = "○"; // 圆圈 - 正常装备
                                statusText = "";
                            }
                            else
                            {
                                statusColor = new Vector4(1.0f, 0.3f, 0.3f, 1.0f); // 红色 - 未装备
                                statusIcon = "✗"; // 叉号 - 未装备
                                statusText = Localization.DebugNotEquipped;
                            }
                            
                            ImGui.TextColored(statusColor, $"{statusIcon} {slotName}:");
                            ImGui.SameLine();
                            
                            // 显示详细信息（背包装备）
                            string details = $"{Localization.DebugInventoryItemId}={actualItemId}";
                            if (!string.IsNullOrEmpty(statusText))
                                details += $" {statusText}";
                            
                            ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.8f, 1.0f), details);
                            
                            // 显示 Glamourer 数据（灰色，仅供参考）
                            if (glamItemId != actualItemId)
                            {
                                ImGui.SameLine();
                                string glamInfo = $"({Localization.DebugGlamourerItemId}={glamItemId})";
                                var glamColor = EmperorsNewItems.IsEmperorsNewByItemId(glamItemId) 
                                    ? new Vector4(1.0f, 0.5f, 0.0f, 0.7f)  // 橙色 - Glam中是特殊物品
                                    : new Vector4(0.5f, 0.5f, 0.5f, 1.0f);  // 灰色 - Glam中是普通装备
                                ImGui.TextColored(glamColor, glamInfo);
                            }
                        }
                        else
                        {
                            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1.0f), $"? {slotName}: {Localization.DebugSlotNoData}");
                        }
                    }
                    catch (Exception ex)
                    {
                        ImGui.TextColored(new Vector4(1.0f, 0.3f, 0.3f, 1.0f), $"✗ {slotName}: {Localization.DebugSlotError} - {ex.Message}");
                    }
                }
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

        private bool TryMatchRule(HeelsRule rule, int ruleIndex, float height, bool allowEquipmentEvaluation)
        {
            if (!rule.IsActive)
                return false;

            if (ruleIndex > 0 && rule.BranchKind == RuleBranchKind.Else)
                return true;

            // 使用新的条件系统（多条件组）
            // 注意：即使 ConditionGroups 为空列表，也使用新系统（空列表代表无条件，总是匹配）
            if (rule.ConditionGroups != null)
            {
                var context = BuildRuleEvaluationContext(height, allowEquipmentEvaluation);
                return EvaluateConditionGroups(rule.ConditionGroups, context);
            }
            
            // 向后兼容：如果有旧的单个 ConditionGroup（迁移时使用）
            if (rule.ConditionGroup != null)
            {
                var context = BuildRuleEvaluationContext(height, allowEquipmentEvaluation);
                return rule.ConditionGroup.Evaluate(context);
            }

            // 向后兼容：如果没有 ConditionGroup，使用旧的高度比较逻辑
            return RuleMatchesHeightCondition(rule, height);
        }

        private RuleEvaluationContext BuildRuleEvaluationContext(float height, bool allowEquipmentEvaluation)
        {
            var localPlayer = ObjectTable?.LocalPlayer;
            string? characterName = null;
            if (localPlayer != null)
            {
                try
                {
                    var worldName = localPlayer.HomeWorld.Value.Name.ToString();
                    characterName = $"{localPlayer.Name}@{worldName}";
                }
                catch
                {
                    characterName = null;
                }
            }

            return new RuleEvaluationContext
            {
                CurrentHeight = height,
                CharacterName = characterName,
                LocalPlayer = localPlayer,
                GlamourerInterop = _glamourerInterop,
                AllowEquipmentEvaluation = allowEquipmentEvaluation,
            };
        }
        
        /// <summary>
        /// 评估多个条件组，使用每个组的 OperatorToNext 连接结果
        /// 例如：(Group1 结果) AND (Group2 结果) OR (Group3 结果)
        /// </summary>
        private bool EvaluateConditionGroups(List<ConditionGroup> groups, RuleEvaluationContext context)
        {
            if (groups.Count == 0)
                return true;  // 没有条件组时默认满足
            
            // 评估第一个组
            bool result = groups[0].Evaluate(context);
            
            // 依次评估后续组并使用 OperatorToNext 组合结果
            for (int i = 1; i < groups.Count; i++)
            {
                var groupResult = groups[i].Evaluate(context);
                var operatorFromPrevious = groups[i - 1].OperatorToNext;
                
                if (operatorFromPrevious == LogicOperator.And)
                {
                    result = result && groupResult;
                }
                else  // Or
                {
                    result = result || groupResult;
                }
            }
            
            return result;
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
            var rules = ActiveRules;
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

            Configuration.GlamourerRules = CloneRulesForMode(source, ActionType.Glamourer);
            Configuration.PenumbraRules = CloneRulesForMode(source, ActionType.Penumbra);
        }

        private void MigrateToUnifiedRulesWithActionTypes()
        {
            if (Configuration.Rules.Count > 0)
                return;

            var unifiedRules = new List<HeelsRule>();

            foreach (var rule in Configuration.GlamourerRules)
            {
                var cloned = CloneRuleWithActionType(rule, ActionType.Glamourer);
                unifiedRules.Add(cloned);
            }

            foreach (var rule in Configuration.PenumbraRules)
            {
                var cloned = CloneRuleWithActionType(rule, ActionType.Penumbra);
                unifiedRules.Add(cloned);
            }

            Configuration.Rules = unifiedRules;
        }

        private HeelsRule CloneRuleWithActionType(HeelsRule source, ActionType actionType)
        {
            var cloned = new HeelsRule
            {
                BranchKind = source.BranchKind,
                HeightComparison = source.HeightComparison,
                HeightValue = source.HeightValue,
                IsActive = source.IsActive,
                Actions = [],
            };

            foreach (var action in source.Actions)
            {
                var clonedAction = new HeelsRuleAction
                {
                    Type = actionType,
                    GlamourerDesign = action.GlamourerDesign ?? "",
                    PenumbraCollection = action.PenumbraCollection ?? "Default",
                    PenumbraModName = action.PenumbraModName ?? "",
                    PenumbraOption = action.PenumbraOption ?? "",
                    PenumbraOptionName = action.PenumbraOptionName ?? "",
                    PenumbraOptionEnabled = action.PenumbraOptionEnabled,
                    PenumbraMultiToggleStates = action.PenumbraMultiToggleStates != null
                        ? new Dictionary<string, bool>(action.PenumbraMultiToggleStates, StringComparer.OrdinalIgnoreCase)
                        : new(StringComparer.OrdinalIgnoreCase),
                    PenumbraActionKind = action.PenumbraActionKind,
                    CustomizePlusProfile = "",
                    HonorificTitleJson = source.HonorificTitleJson ?? "",
                    MoodleGuid = source.MoodleGuid ?? "",
                    MoodleIsPreset = source.MoodleIsPreset,
                    IsActionCollapsed = action.IsActionCollapsed,
                };
                cloned.Actions.Add(clonedAction);
            }

            if (cloned.Actions.Count == 0)
                cloned.Actions.Add(CreateDefaultRuleActionForType(actionType));

            return cloned;
        }
        
        /// <summary>
        /// v11→v12: 将旧的 HeightComparison/HeightValue 迁移到新的 ConditionGroup 系统
        /// </summary>
        private void MigrateToConditionSystem()
        {
            foreach (var rule in Configuration.Rules)
            {
                // 如果已经有 ConditionGroup，跳过
                if (rule.ConditionGroup != null)
                    continue;
                    
                // Else 分支不需要条件
                if (rule.BranchKind == RuleBranchKind.Else)
                {
                    rule.ConditionGroup = new ConditionGroup
                    {
                        Operator = LogicOperator.And,
                        Conditions = new List<RuleCondition>()
                    };
                    continue;
                }
                
                // 将旧的高度条件转换为新的 HeightCondition
                var heightCondition = new HeightCondition
                {
                    Comparison = rule.HeightComparison,
                    Value = rule.HeightValue
                };
                
                rule.ConditionGroup = new ConditionGroup
                {
                    Operator = LogicOperator.And,
                    Conditions = new List<RuleCondition> { heightCondition }
                };
            }
            
        }

        private List<HeelsRule> CloneRulesForMode(List<HeelsRule> source, ActionType actionType)
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
                    cloned.Actions.Add(CloneActionForMode(action, actionType));

                if (cloned.Actions.Count == 0)
                    cloned.Actions.Add(CreateDefaultRuleActionForType(actionType));

                result.Add(cloned);
            }

            return result;
        }

        private static HeelsRuleAction CloneActionForMode(HeelsRuleAction source, ActionType actionType)
        {
            if (actionType == ActionType.Glamourer)
            {
                return new HeelsRuleAction
                {
                    Type = ActionType.Glamourer,
                    GlamourerDesign = source.GlamourerDesign ?? "",
                    IsActionCollapsed = source.IsActionCollapsed,
                };
            }

            return new HeelsRuleAction
            {
                Type = ActionType.Penumbra,
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
            
            // 清理 Glamourer 事件订阅
            _glamourerInterop.Dispose();
            
            // 清理 Penumbra 事件订阅
            _penumbraInterop.Dispose();
        }
    }
}