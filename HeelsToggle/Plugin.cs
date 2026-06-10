using System.Numerics;
using System.Text.RegularExpressions;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Configuration;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Components;
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
        Moodles = 4,
        SoundMixer = 5,
    }

    /// <summary>SoundMixer 操作类型</summary>
    public enum SoundMixerActionKind
    {
        /// <summary>临时切换预设</summary>
        TemporaryPreset = 0,
        /// <summary>临时设置单组音量</summary>
        TemporaryGroupVolume = 1,
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
    
    /// <summary>装备条件匹配方式（在必须装备/未装备之下）。</summary>
    public enum EquipmentMatchMode
    {
        /// <summary>任意可见装备（排除 ModelId 0 与皇帝套），与旧版语义一致。</summary>
        Any = 0,
        /// <summary>DrawObject 槽位 ModelId 与 <see cref="EquipmentCondition.TargetModelId"/> 精确比较。</summary>
        SpecificModelId = 1,
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
        public bool MustBeEquipped { get; set; } = true;
        public EquipmentMatchMode MatchMode { get; set; } = EquipmentMatchMode.Any;
        public ushort TargetModelId { get; set; }

        public EquipmentCondition()
        {
            Type = ConditionType.Equipment;
        }

        public bool UsesSpecificModelId =>
            MustBeEquipped
            && MatchMode == EquipmentMatchMode.SpecificModelId
            && IsRuleEquipmentSlotSupported(Slot);

        public override bool Evaluate(RuleEvaluationContext context)
        {
            if (!context.AllowEquipmentEvaluation || !IsRuleEquipmentSlotSupported(Slot))
                return false;

            if (!MustBeEquipped)
            {
                var vacant = context.RenderedEquipment.TryGetHasEquipment(Slot);
                return vacant == false;
            }

            if (MatchMode == EquipmentMatchMode.Any)
            {
                var hasEquipment = context.RenderedEquipment.TryGetHasEquipment(Slot);
                return hasEquipment == true;
            }

            var modelId = context.RenderedEquipment.TryGetRenderedModelId(Slot);
            return modelId != null && modelId.Value == TargetModelId;
        }

        internal static bool IsRuleEquipmentSlotSupported(EquipSlot slot) =>
            slot is not EquipSlot.MainHand and not EquipSlot.OffHand;
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
        internal RenderedEquipmentSnapshot RenderedEquipment { get; set; } = RenderedEquipmentSnapshot.Unavailable;
        /// <summary>登录预热完成前为 false，禁止装备条件评估。</summary>
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
                obj["MatchMode"] = (int)equipCond.MatchMode;
                obj["TargetModelId"] = equipCond.TargetModelId;
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
                var slot = (EquipSlot)(obj["Slot"]?.ToObject<int>() ?? 0);
                if (!EquipmentCondition.IsRuleEquipmentSlotSupported(slot))
                    slot = EquipSlot.Feet;

                var mustBeEquipped = obj["MustBeEquipped"]?.ToObject<bool>() ?? true;
                var matchMode = (EquipmentMatchMode)(obj["MatchMode"]?.ToObject<int>() ?? 0);
                if (!mustBeEquipped)
                    matchMode = EquipmentMatchMode.Any;

                return new EquipmentCondition
                {
                    Slot = slot,
                    MustBeEquipped = mustBeEquipped,
                    MatchMode = matchMode,
                    TargetModelId = obj["TargetModelId"]?.ToObject<ushort>() ?? 0,
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

        /// <summary>SimpleHeels 配置区块是否展开（UI 状态）。</summary>
        public bool IsSimpleHeelsSectionExpanded { get; set; } = true;

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
                IsSimpleHeelsSectionExpanded = this.IsSimpleHeelsSectionExpanded,
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

        /// <summary>上次关闭插件时的命中规则签名（逗号分隔规则索引，空表示无匹配）。</summary>
        public string LastShutdownMatchedRuleSignature { get; set; } = "";

        /// <summary>上次关闭插件时激活的规则集索引。</summary>
        public int LastShutdownActiveRuleSetIndex { get; set; } = -1;

        /// <summary>上次关闭时的 Moodles apply 去重键。</summary>
        public string LastShutdownAppliedMoodleKey { get; set; } = "";

        /// <summary>上次关闭时的 Honorific apply 去重键。</summary>
        public string LastShutdownLastAppliedHonorificJson { get; set; } = "";

        /// <summary>上次关闭时的行动 apply 去重键（含基准 Penumbra/Moodles 等）。</summary>
        public List<string> LastShutdownAppliedActionKeys { get; set; } = new();

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
        public float ApplyCooldownSeconds { get; set; } = 0.5f;

        /// <summary>规则持续匹配多久后才开始执行行动（秒），0 表示不等待。</summary>
        public float RuleMatchStableSeconds { get; set; } = 0.25f;

        /// <summary>界面语言；System 跟随系统 UI 语言。</summary>
        public UiLanguagePreference UiLanguage { get; set; } = UiLanguagePreference.System;

        /// <summary>在游戏顶部服务器信息栏（DTR）显示当前高度与匹配规则。</summary>
        public bool ShowDtrStatusBar { get; set; } = true;

        /// <summary>SFW 模式是否激活（全局，高于规则 Penumbra 层）。</summary>
        public bool SfwModeActive { get; set; } = false;

        /// <summary>旧版 SFW 扁平行动列表，仅用于 v16→v17 迁移。</summary>
        [Obsolete("Use SfwModeActionGroups instead")]
        public List<HeelsRuleAction> SfwModeActions { get; set; } = new();

        /// <summary>SFW 模式 Penumbra 行动组（与规则内 Penumbra 行动组同结构）。</summary>
        public List<PenumbraActionGroup> SfwModeActionGroups { get; set; } = new();

        /// <summary>规则匹配使用的 SimpleHeels 高度来源。</summary>
        public SimpleHeelsHeightMode SimpleHeelsHeightMode { get; set; } = SimpleHeelsHeightMode.Default;

        /// <summary>废弃字段：手动模式已移除，保留仅用于向后兼容。</summary>
        [Obsolete("Manual mode has been removed; SimpleHeels configuration moved to RuleSet level")]
        public float ManualHeight { get; set; } = 0.0f;

        /// <summary>配置窗口宽度（像素）。</summary>
        public float WindowWidth { get; set; } = 600f;

        /// <summary>配置窗口高度（像素）。</summary>
        public float WindowHeight { get; set; } = 450f;

        /// <summary>Penumbra 规则 apply 使用临时写入（key -1211）；关闭则永久改写 collection 设定。</summary>
        public bool UsePenumbraTemporaryApply { get; set; } = true;

        /// <summary>apply 前自动清除 Glamourer 在同一 Mod 上的 Penumbra 临时层。</summary>
        public bool AutoOverwriteGlamourerPenumbra { get; set; } = false;

        /// <summary>旧版 Penumbra 全局设置，仅用于迁移。</summary>
        public string PenumbraCollection { get; set; } = "Default";
        public string PenumbraModName { get; set; } = "";
        public string PenumbraOption { get; set; } = "";
    }

    /// <summary>Penumbra 行动组内单条子行动（不含 Collection / Mod）。</summary>
    public class PenumbraSubAction
    {
        public PenumbraActionKind PenumbraActionKind { get; set; } = PenumbraActionKind.SetModOption;
        public string PenumbraOption { get; set; } = "";
        public string PenumbraOptionName { get; set; } = "";
        public bool PenumbraOptionEnabled { get; set; } = true;
        public Dictionary<string, bool> PenumbraMultiToggleStates { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public bool PenumbraOverwriteGlamourer { get; set; } = false;
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        public bool IsCollapsed { get; set; } = false;
    }

    /// <summary>Penumbra 行动组：一个 Collection + Mod，内含多条子行动。</summary>
    public class PenumbraActionGroup
    {
        public string PenumbraCollection { get; set; } = "Default";
        public string PenumbraModName { get; set; } = "";
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        public bool IsCollapsed { get; set; } = false;
        public List<PenumbraSubAction> SubActions { get; set; } = new();

        /// <summary>旧版组内扁平行动（v17），迁移至 SubActions。</summary>
        [Obsolete("Use SubActions instead")]
        public List<HeelsRuleAction> Actions { get; set; } = new();
    }

    public class HeelsRuleAction
    {
        /// <summary>行动类型（Glamourer / Penumbra / Honorific / Moodles）。</summary>
        public ActionType Type { get; set; } = ActionType.Glamourer;

        /// <summary>Glamourer: Design GUID。</summary>
        public string GlamourerDesign { get; set; } = "";

        /// <summary>Glamourer 行动优先级：数值越大越优先（越后应用、冲突槽位胜出）。仅 Glamourer 类型使用。</summary>
        public int GlamourerPriority { get; set; } = 0;

        /// <summary>Penumbra 行动组（Type == Penumbra 时使用）。</summary>
        public PenumbraActionGroup? PenumbraGroup { get; set; }

        /// <summary>旧版扁平 Penumbra 字段，仅用于 v18 前配置迁移。</summary>
        public PenumbraActionKind PenumbraActionKind { get; set; } = PenumbraActionKind.SetModOption;
        public string PenumbraCollection { get; set; } = "Default";
        public string PenumbraModName { get; set; } = "";
        public string PenumbraOption { get; set; } = "";
        public string PenumbraOptionName { get; set; } = "";
        public bool PenumbraOptionEnabled { get; set; } = true;
        public Dictionary<string, bool> PenumbraMultiToggleStates { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public bool PenumbraOverwriteGlamourer { get; set; } = false;

        /// <summary>Customize+: 配置名称。</summary>
        public string CustomizePlusProfile { get; set; } = "";

        /// <summary>可选：Honorific 称号（JSON）。</summary>
        public string HonorificTitleJson { get; set; } = "";

        /// <summary>可选：Moodles GUID。</summary>
        public string MoodleGuid { get; set; } = "";
        /// <summary>可选：Moodles 是否为预设。</summary>
        public bool MoodleIsPreset { get; set; }

        /// <summary>SoundMixer: 操作类型。</summary>
        public SoundMixerActionKind SoundMixerActionKind { get; set; } = SoundMixerActionKind.TemporaryPreset;
        /// <summary>SoundMixer: 预设名称或 ID。</summary>
        public string SoundMixerPresetName { get; set; } = "";
        /// <summary>SoundMixer: 分组名称或 ID。</summary>
        public string SoundMixerGroupName { get; set; } = "";
        /// <summary>SoundMixer: 目标音量。</summary>
        public float SoundMixerGroupVolume { get; set; } = 1.0f;
        /// <summary>SoundMixer: 临时覆盖优先级（同 tag 内越高越优先）。</summary>
        public int SoundMixerPriority { get; set; } = SoundMixerInterop.DefaultPriority;

        /// <summary>规则 UI 中是否折叠该行动详情。</summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        public bool IsActionCollapsed { get; set; }
    }

    public class HeelsRule
    {
        /// <summary>规则的自定义名称（用户可编辑）。</summary>
        public string Name { get; set; } = "";
        
        /// <summary>规则是否折叠（UI状态）。</summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        public bool IsCollapsed { get; set; } = false;
        
        public RuleBranchKind BranchKind { get; set; } = RuleBranchKind.ElseIf;

        /// <summary>
        /// 与「下一条规则」的连接关系（即分组边界）：
        /// Or（默认）= 同一分组（同一 if/否则如果/否则 链，组内互斥、first-match、否则兜底）；
        /// And = 结束本分组并让下一条开启新分组（分组之间相互独立、共存叠加应用）。
        /// </summary>
        public LogicOperator OperatorToNext { get; set; } = LogicOperator.Or;

        /// <summary>分组名称：仅在该规则是其分组的第一条时生效（用于分组头显示）。</summary>
        public string GroupName { get; set; } = "";

        /// <summary>分组是否整体折叠：仅在该规则是其分组的第一条时生效（折叠后隐藏组内所有规则）。</summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        public bool GroupCollapsed { get; set; } = false;
        
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

        /// <summary>SFW 模式激活时该规则是否参与匹配（默认 true = 参与）。</summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        public bool SfwModeEnabled { get; set; } = true;
        
        /// <summary>行动列表是否折叠显示（UI 状态，已合并至 <see cref="IsCollapsed"/>，仅用于旧配置迁移）。</summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
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
        [PluginService] public static ITextureProvider TextureProvider { get; private set; } = null!;
        [PluginService] public static IDtrBar DtrBar { get; private set; } = null!;

        private const string DtrBarEntryTitle = "Heels Design Linker";
        private IDtrBarEntry? dtrBarEntry;
        private string lastDtrBarText = "";
        private IDtrBarEntry? dtrSfwBarEntry;
        private string lastDtrSfwBarText = "";

        private readonly struct PenumbraApplyLayerConfig
        {
            public static PenumbraApplyLayerConfig Rule { get; } = new("P:", false);
            public static PenumbraApplyLayerConfig Sfw { get; } = new("SF:", true);

            public string KeyPrefix { get; }
            public bool IsSfwLayer { get; }

            private PenumbraApplyLayerConfig(string keyPrefix, bool isSfwLayer)
            {
                KeyPrefix = keyPrefix;
                IsSfwLayer = isSfwLayer;
            }
        }

        private Configuration Configuration { get; set; }
        
        /// <summary>获取当前激活的规则列表</summary>
        private List<HeelsRule> ActiveRules => 
            Configuration.RuleSets.Count > 0 
            && Configuration.ActiveRuleSetIndex >= 0 
            && Configuration.ActiveRuleSetIndex < Configuration.RuleSets.Count
                ? Configuration.RuleSets[Configuration.ActiveRuleSetIndex].Rules
                : new List<HeelsRule>();

        /// <summary>该规则索引本帧是否被命中并应用（含主规则与 AND 共存附加规则）。</summary>
        private bool IsRuleApplied(int ruleIndex) => currentAppliedRuleIndices.Contains(ruleIndex);

        /// <summary>该规则是否是其分组（OR 块）的第一条：列表首条，或上一条连接符为 And（开启新分组）。</summary>
        private bool IsRuleGroupStart(int ruleIndex)
        {
            var rules = ActiveRules;
            return ruleIndex == 0
                || (ruleIndex - 1 >= 0 && ruleIndex - 1 < rules.Count
                    && rules[ruleIndex - 1].OperatorToNext == LogicOperator.And);
        }

        /// <summary>该规则之后、在同一分组内是否还存在已启用的规则（用于判断“否则”是否让组内后续规则不可达）。</summary>
        private bool HasActiveRuleLaterInSameGroup(int ruleIndex)
        {
            var rules = ActiveRules;
            for (var j = ruleIndex; j < rules.Count - 1; j++)
            {
                // j 与 j+1 之间为 And → 分组在此结束，后续规则属于新分组。
                if (rules[j].OperatorToNext == LogicOperator.And)
                    return false;

                if (rules[j + 1].IsActive)
                    return true;
            }

            return false;
        }

        private static readonly Vector4 RuleUnreachableWarningColor = new(1.0f, 0.85f, 0.2f, 1.0f);
        private static readonly Vector4 RulePenumbraConflictWarningColor = new(1.0f, 0.55f, 0.15f, 1.0f);
        private static readonly Vector4 RuleConditionConflictColor = new(1.0f, 0.35f, 0.35f, 1.0f);
        private static readonly Vector4 MatchedRuleStatusColor = new(0.4f, 1.0f, 0.5f, 1.0f);
        private static readonly Vector4 PenumbraGlamourerTakeoverWarningColor = new(1.0f, 0.92f, 0.2f, 1.0f);
        private static readonly Vector4 GlamourerSlotConflictUnresolvedColor = new(1.0f, 0.3f, 0.3f, 1.0f);
        private static readonly Vector4 GlamourerSlotConflictResolvedColor = new(0.4f, 0.7f, 1.0f, 1.0f);

        // Glamourer 装备槽位冲突分析结果（(规则索引, 行动索引) → 冲突状态），节流刷新。
        private Dictionary<(int RuleIndex, int ActionIndex), GlamourerConflictKind> glamourerConflicts = new();
        private DateTime glamourerConflictsComputedUtc = DateTime.MinValue;
        private static readonly TimeSpan GlamourerConflictRefreshInterval = TimeSpan.FromSeconds(1);

        // 当前命中的可共存(AND)规则之间，在同一 Mod 上存在 Penumbra 选项冲突的 Mod 键集合（Collection|Mod），节流刷新。
        private HashSet<string> matchedModConflicts = new(StringComparer.OrdinalIgnoreCase);
        private DateTime matchedModConflictsComputedUtc = DateTime.MinValue;
        
        private bool drawConfigUi = false;
        private static readonly TimeSpan PenumbraUnverifiedRetryInterval = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan PenumbraDedupTrustInterval = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan PenumbraIpcReadyGrace = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan PenumbraStatusErrorDisplayDelay = TimeSpan.FromSeconds(2);
        private readonly HashSet<string> lastAppliedActionKeys = new(StringComparer.Ordinal);
        private readonly Dictionary<string, DateTime> penumbraApplyAttemptUtcByKey = new(StringComparer.Ordinal);
        private readonly Dictionary<string, DateTime> penumbraDedupTrustedUntilUtcByKey = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> penumbraDriftStreakByKey = new(StringComparer.Ordinal);
        private DateTime? penumbraIpcLastReadyUtc;
        private DateTime? penumbraLoadedLastTrueUtc;
        private DateTime? penumbraRawErrorSinceUtc;
        private bool isPenumbraLoaded;
        private bool penumbraStatusDisplayError;
        private string penumbraStatusDisplayText = "";
        private string lastAppliedHonorificJson = "";
        private string lastAppliedMoodleKey = "";
        /// <summary>启动后若命中与上次关闭相同，跳过一次基准/规则 re-apply。</summary>
        private bool skipReapplyForRestoredShutdownMatch;
        /// <summary>本 apply 周期内基准是否成功写入了 Moodles（用于随后让规则 Moodles 再 apply 一次）。</summary>
        private bool baselineMoodleAppliedThisCycle;
        
        // 用于UI显示的"上次执行"信息（与去重机制分离）
        private int lastExecutedRuleIndex = -1;
        private DateTime? lastExecutedActionUtc = null;
        private readonly List<string> lastExecutedActionSummaries = new();
        private readonly List<string> appliedActionSummariesThisCycle = new();
        private int lastMatchedRuleIndex = -1;
        // 当前帧命中并将被应用的全部规则索引（按列表顺序；含主规则与 AND 共存的附加规则）。
        private readonly List<int> currentAppliedRuleIndices = new();
        // 上次「已应用」的命中规则集合签名（用于检测集合变化并重算覆盖/去重）。
        private string lastMatchedSetSignature = "";
        // 用于稳定性计时的命中集合签名；null 表示需要重新计时（如高度/外观抖动）。
        private string? stableTrackingSignature = null;
        private float currentHeelsHeight = 0f;
        private float currentHeelsDefaultHeight = 0f;
        private float currentHeelsActualHeight = 0f;
        private bool currentHeelsHasTempOffset = false;
        private DateTime? lastTempOffsetSeenUtc;
        private float lastKnownActualHeelsHeight;
        private const double TempOffsetAbsenceGraceSeconds = 3.0;
        private int currentMatchedRuleIndex = -1; // 当前匹配的规则索引，-1 表示无匹配
        
        // 行动删除确认相关
        private int actionToDeleteRuleIndex = -1;
        private int actionToDeleteIndex = -1;
        
        // 条件拖拽排序相关
        private int? _conditionDragSourceRuleIndex;
        private int? _conditionDragSourceGroupIndex;
        private int? _conditionDragSourceIndex;
        private int? _conditionDragTargetIndex;

        // 规则内行动拖拽排序相关
        private int? _actionDragSourceRuleIndex;
        private int? _actionDragSourceIndex;
        private int? _actionDragTargetIndex;
        private bool _actionDropAfter;
        
        // 条件删除相关
        private int conditionToDeleteRuleIndex = -1;
        private int conditionToDeleteGroupIndex = -1;
        private int conditionToDeleteIndex = -1;
        
        private readonly AppearanceChangeTracker _appearanceChangeTracker = new();

        private bool isGlamourerAvailable = false;
        private bool isSimpleHeelsAvailable = false;
        private bool isSimpleHeelsIpcReady = false;
        private bool isPenumbraIpcReady = false;
        private bool isMoodlesIpcReady = false;
        private bool isHonorificIpcReady = false;
        private bool isSoundMixerIpcReady = false;
        private bool isSoundMixerLoaded;
        private DateTime? soundMixerLoadedLastTrueUtc;
        private DateTime? soundMixerIpcLastReadyUtc;
        private DateTime? soundMixerRawErrorSinceUtc;
        private bool soundMixerStatusDisplayError;
        private string soundMixerStatusDisplayText = "";
        private int soundMixerDetectedApiVersion;
        private bool soundMixerApiVersionCompatible = true;
        private DateTime soundMixerIpcProbeUtc = DateTime.MinValue;
        private IReadOnlyList<SoundMixerIpcGateProbe> soundMixerIpcGateProbes = [];
        private bool soundMixerOverridesActive = false;
        private bool penumbraTemporaryOverridesActive = false;
        private bool sfwTemporaryOverridesActive = false;
        private PenumbraApplyLayerConfig? _activePenumbraApplyLayer;
        /// <summary>规则切换清临时层后，本周期 batch apply 勿合并旧 -1211 选项组（避免 A→B 同 Mod 残留）。</summary>
        private bool penumbraApplyWithoutTempMerge;
        /// <summary>SFW 层清临时后，本周期 batch apply 勿合并旧 -1210 选项组。</summary>
        private bool sfwApplyWithoutTempMerge;
        // private bool isCustomizePlusIpcReady = false;  // 已移除 Customize+ 支持

        private readonly PenumbraInterop _penumbraInterop;
        private readonly GlamourerInterop _glamourerInterop;
        private readonly MoodlesInterop _moodlesInterop;
        private readonly HonorificInterop _honorificInterop;
        private readonly SoundMixerInterop _soundMixerInterop;
        // private readonly CustomizePlusInterop _customizePlusInterop;  // 已移除 Customize+ 支持
        
        private DateTime lastDependencyCheckUtc = DateTime.MinValue;
        private static readonly TimeSpan DependencyRecheckWhenMissing = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan DependencyRecheckWhenReady = TimeSpan.FromSeconds(30);

        /// <summary>登录保护最短时长（与主手锚点并行），期间不匹配、不 apply。</summary>
        private static readonly TimeSpan LoginProtectionMinDuration = TimeSpan.FromSeconds(0.35);
        /// <summary>登录保护期内主手背包锚点需保持稳定的时间。</summary>
        private static readonly TimeSpan MainHandAnchorStableDelay = TimeSpan.FromSeconds(0.25);
        /// <summary>会话中本地玩家对象恢复有效后，再等待一段时间才自动 apply。</summary>
        private static readonly TimeSpan SessionRecoveryStableDelay = TimeSpan.FromSeconds(0.3);
        /// <summary>登录保护最长持续时间，超时后自动结束以免拖慢正常游戏。</summary>
        private static readonly TimeSpan LoginProtectionMaxDuration = TimeSpan.FromSeconds(45);
        /// <summary>登录保护结束后，再延迟一段时间才允许基准行动（避免紧接 revert 把刚加载的投影剥掉）。</summary>
        private static readonly TimeSpan PostLoginBaselineDelay = TimeSpan.FromSeconds(0.1f);
        /// <summary>登录后一段时间内，规则匹配稳定等待的上限（与基准行动同步门控后，二者同周期 apply）。</summary>
        private static readonly TimeSpan PostLoginFastMatchWindow = TimeSpan.FromSeconds(20);
        private const float PostLoginRuleMatchStableCapSeconds = 0.2f;
        /// <summary>登录后延迟多久才允许基准 Glamourer revert（与规则 Glamourer apply 无关）。</summary>
        private static readonly TimeSpan BaselineGlamourerRevertDelay = TimeSpan.FromSeconds(90);
        private DateTime? loginSinceUtc;
        private bool isLoginProtectionActive;
        private DateTime? localPlayerStableSinceUtc;
        private DateTime? appearancePopulatedSinceUtc;
        private uint lastTrackedMainHandItemId;
        private bool mainHandAnchorInitialized;
        private DateTime? baselineActionsAllowedAfterUtc;
        private DateTime lastApplyUtc = DateTime.MinValue;
        private string applyGateStatus = "";
        
        // 调试信息
        private string lastError = "";
        private string lastIpcData = "";
        private float lastRuleMatchingHeight = float.NaN; // 用于检测规则匹配高度变化（非 IPC 原始字符串）
        private string penumbraModSearchFilter = "";
        private readonly Dictionary<string, string> _equipmentItemSearchQueries = new(StringComparer.Ordinal);
        private string feetWarningDismissedSignature = "";
        private string lastFeetWarningSignature = "";
        private int? _ruleDragSourceIndex;
        private int? _ruleDragTargetIndex;
        private bool _ruleDropAfter;
        private int? _groupDragSourceIndex;
        private int? _groupDragTargetIndex;
        private bool _groupDropAfter;
        private bool restoreDefaultsPending;
        private bool wasSettingsTabActive;
        private const string KoFiUrl = "https://ko-fi.com/kokatakyodai";
        private const int ConfigSchemaVersion = 23;
        private const int SfwGroupRuleIndexBase = -2;
        private const int PenumbraSubHostRuleIndexBase = -100000;
        private const int PenumbraSubHostRuleIndexStride = 1000;
        private const int SfwPenumbraGroupsReorderRuleIndex = -50000;
        private const string DtrSfwBarEntryTitle = "HDL SFW";
        private int stableTrackingRuleIndex = -1;
        private DateTime? ruleMatchStableSinceUtc;
        private const float DefaultWindowWidth = 600f;
        private const float DefaultWindowHeight = 450f;
        private const string ConfigurationWindowImGuiId = "HeelsDesignLinkerConfig";
        
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
            _soundMixerInterop = new SoundMixerInterop(PluginInterface);
            // _customizePlusInterop = new CustomizePlusInterop(PluginInterface);  // 已移除 Customize+ 支持
            
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
                if (Configuration.Version < 14)
                {
                    // v14: UsePenumbraTemporaryApply / AutoOverwriteGlamourerPenumbra / PenumbraOverwriteGlamourer 使用类型默认值
                }
                if (Configuration.Version < 15)
                {
                    // v15: ShowDtrStatusBar 使用类型默认值 true
                }
                if (Configuration.Version < 16)
                {
                    Configuration.SfwModeActions ??= new List<HeelsRuleAction>();
                }
                if (Configuration.Version < 17)
                {
                    Configuration.SfwModeActionGroups ??= new List<PenumbraActionGroup>();
                    MigrateSfwModeActionsToGroupsIfNeeded();
                }
                if (Configuration.Version < 18)
                {
                    MigrateAllPenumbraActionsToGroupsIfNeeded();
                }
                if (Configuration.Version < 19)
                {
                    // v19: 规则间 AND/OR 连接符（HeelsRule.OperatorToNext，默认 Or）
                    // 与 Glamourer 行动优先级（GlamourerPriority，默认 0）。
                    // 均使用类型默认值，旧配置行为不变，无需数据迁移。
                }
                if (Configuration.Version < 20)
                {
                    // v20: 规则/行动折叠状态持久化修复（Penumbra 组与 IsActionCollapsed 同步、旧 IsActionsCollapsed 迁移）。
                    NormalizeCollapsePersistence();
                }
                if (Configuration.Version < 21)
                {
                    // v21: HeelsRule.SfwModeEnabled（默认 true，旧规则在 SFW 模式下仍参与匹配）。
                }
                if (Configuration.Version < 22)
                {
                    // v22: LastShutdown* 字段用于插件重载后恢复命中与 apply 去重状态。
                }
                if (Configuration.Version < 23)
                {
                    // v23: 插件启用时校验行动字段分类，错放字段拆分为独立行动。
                }
                Configuration.Version = ConfigSchemaVersion;
                PluginInterface.SavePluginConfig(Configuration);
            }

            var sanitizedActionCount = SanitizeMisplacedActionFields();
            if (sanitizedActionCount > 0)
            {
                PluginLog.Information(Localization.ConfigSanitizedMisplacedActions(sanitizedActionCount));
                SaveConfig();
            }

            Localization.SetLanguagePreference(Configuration.UiLanguage);
            RestoreShutdownApplySnapshot();
            
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
            var trimmedArgs = args?.Trim() ?? "";
            if (string.Equals(trimmedArgs, "sfw", StringComparison.OrdinalIgnoreCase))
            {
                SetSfwModeActive(true);
                return;
            }

            if (string.Equals(trimmedArgs, "nsfw", StringComparison.OrdinalIgnoreCase))
            {
                SetSfwModeActive(false);
                return;
            }

            drawConfigUi = !drawConfigUi;
            if (drawConfigUi)
                OpenConfigurationUi();
        }

        private void OpenConfigurationUi()
        {
            drawConfigUi = true;
            RefreshDependencies(force: true);
            _glamourerInterop.RefreshDesignList(force: true);
            _penumbraInterop.EnsureUiSelectedModSubscription();
            _penumbraInterop.RefreshData(force: true);
            _moodlesInterop.RefreshList(force: true);
            _honorificInterop.RefreshTitleList(ObjectTable.LocalPlayer, force: true);
            // _customizePlusInterop.RefreshProfileList(force: true);  // 已移除 Customize+ 支持
        }

        private static bool ActionUsesGlamourer(HeelsRuleAction action) =>
            action.Type == ActionType.Glamourer
            && !string.IsNullOrWhiteSpace(action.GlamourerDesign);

        private static bool HasLegacyFlatPenumbraFields(HeelsRuleAction action) =>
            action.PenumbraActionKind != PenumbraActionKind.SetModOption
            || !string.IsNullOrWhiteSpace(action.PenumbraModName)
            || !string.IsNullOrWhiteSpace(action.PenumbraOption)
            || !string.IsNullOrWhiteSpace(action.PenumbraOptionName)
            || action.PenumbraMultiToggleStates.Count > 0;

        private static PenumbraSubAction ExtractPenumbraSubActionFromLegacy(HeelsRuleAction action) =>
            new()
            {
                PenumbraActionKind = action.PenumbraActionKind,
                PenumbraOption = action.PenumbraOption ?? "",
                PenumbraOptionName = action.PenumbraOptionName ?? "",
                PenumbraOptionEnabled = action.PenumbraOptionEnabled,
                PenumbraOverwriteGlamourer = action.PenumbraOverwriteGlamourer,
                PenumbraMultiToggleStates = new Dictionary<string, bool>(
                    action.PenumbraMultiToggleStates,
                    StringComparer.OrdinalIgnoreCase),
            };

        private static void ClearLegacyFlatPenumbraFields(HeelsRuleAction action)
        {
            action.PenumbraActionKind = PenumbraActionKind.SetModOption;
            action.PenumbraCollection = "Default";
            action.PenumbraModName = "";
            action.PenumbraOption = "";
            action.PenumbraOptionName = "";
            action.PenumbraOptionEnabled = true;
            action.PenumbraOverwriteGlamourer = false;
            action.PenumbraMultiToggleStates.Clear();
        }

        private PenumbraSubAction CreateDefaultPenumbraSubAction() =>
            new() { PenumbraActionKind = PenumbraActionKind.SetModOption };

        private PenumbraActionGroup CreateDefaultPenumbraActionGroup()
        {
            return new PenumbraActionGroup
            {
                PenumbraCollection = GetActivePenumbraCollectionName(),
                SubActions = [CreateDefaultPenumbraSubAction()],
            };
        }

        private PenumbraActionGroup EnsurePenumbraGroupOnAction(HeelsRuleAction action)
        {
            if (action.PenumbraGroup == null)
                action.PenumbraGroup = new PenumbraActionGroup();

            var group = action.PenumbraGroup;
            MigratePenumbraGroupLegacySubActions(group);

            if (string.IsNullOrWhiteSpace(group.PenumbraModName)
                && !string.IsNullOrWhiteSpace(action.PenumbraModName))
                group.PenumbraModName = action.PenumbraModName;

            if ((string.IsNullOrWhiteSpace(group.PenumbraCollection) || group.PenumbraCollection == "Default")
                && !string.IsNullOrWhiteSpace(action.PenumbraCollection)
                && action.PenumbraCollection != "Default")
                group.PenumbraCollection = action.PenumbraCollection;

            if (group.SubActions.Count == 0 && HasLegacyFlatPenumbraFields(action))
                group.SubActions.Add(ExtractPenumbraSubActionFromLegacy(action));

            if (group.SubActions.Count == 0)
                group.SubActions.Add(CreateDefaultPenumbraSubAction());

            ClearLegacyFlatPenumbraFields(action);
            return group;
        }

        private void MigratePenumbraGroupLegacySubActions(PenumbraActionGroup group)
        {
#pragma warning disable CS0618
            if (group.Actions.Count == 0)
                return;

            foreach (var legacy in group.Actions)
            {
                if (legacy.Type != ActionType.Penumbra)
                    continue;

                if (string.IsNullOrWhiteSpace(group.PenumbraModName)
                    && !string.IsNullOrWhiteSpace(legacy.PenumbraModName))
                    group.PenumbraModName = legacy.PenumbraModName;

                if ((string.IsNullOrWhiteSpace(group.PenumbraCollection) || group.PenumbraCollection == "Default")
                    && !string.IsNullOrWhiteSpace(legacy.PenumbraCollection)
                    && legacy.PenumbraCollection != "Default")
                    group.PenumbraCollection = legacy.PenumbraCollection;

                group.SubActions.Add(ExtractPenumbraSubActionFromLegacy(legacy));
            }

            group.Actions.Clear();
#pragma warning restore
        }

        private static HeelsRuleAction FlattenPenumbraSubAction(PenumbraActionGroup group, PenumbraSubAction sub) =>
            new()
            {
                Type = ActionType.Penumbra,
                PenumbraCollection = group.PenumbraCollection ?? "Default",
                PenumbraModName = group.PenumbraModName ?? "",
                PenumbraActionKind = sub.PenumbraActionKind,
                PenumbraOption = sub.PenumbraOption ?? "",
                PenumbraOptionName = sub.PenumbraOptionName ?? "",
                PenumbraOptionEnabled = sub.PenumbraOptionEnabled,
                PenumbraOverwriteGlamourer = sub.PenumbraOverwriteGlamourer,
                PenumbraMultiToggleStates = new Dictionary<string, bool>(
                    sub.PenumbraMultiToggleStates,
                    StringComparer.OrdinalIgnoreCase),
            };

        /// <summary>将 UI 临时扁平对象写回 Penumbra 子行动（SaveConfig 前必须同步，否则磁盘仍为旧值）。</summary>
        private static void ApplyFlatPenumbraSubActionToSub(PenumbraSubAction sub, HeelsRuleAction flat)
        {
            sub.PenumbraOption = flat.PenumbraOption ?? "";
            sub.PenumbraOptionName = flat.PenumbraOptionName ?? "";
            sub.PenumbraOptionEnabled = flat.PenumbraOptionEnabled;
            sub.PenumbraOverwriteGlamourer = flat.PenumbraOverwriteGlamourer;
            sub.PenumbraMultiToggleStates = new Dictionary<string, bool>(
                flat.PenumbraMultiToggleStates,
                StringComparer.OrdinalIgnoreCase);
        }

        private void SavePenumbraSubActionEdit(PenumbraSubAction? syncSub, HeelsRuleAction flat)
        {
            if (syncSub != null)
                ApplyFlatPenumbraSubActionToSub(syncSub, flat);
            SaveConfig();
        }

        private IEnumerable<HeelsRuleAction> EnumerateEffectiveRuleActions(HeelsRule rule)
        {
            foreach (var action in GetRuleActions(rule))
            {
                if (action.Type == ActionType.Penumbra)
                {
                    foreach (var flat in FlattenPenumbraGroupAction(action))
                        yield return flat;
                }
                else
                {
                    yield return action;
                }
            }
        }

        /// <summary>规则行动增删改后同步基准配置并持久化。</summary>
        private void PersistRuleSetAfterActionMutation()
        {
            if (TryGetActiveRuleSet(out var ruleSet) && ruleSet.UseBaselineActions)
                UpdateBaselineConfigs(ruleSet);
            SaveConfig();
        }

        private List<HeelsRuleAction> FlattenPenumbraGroup(PenumbraActionGroup group) =>
            group.SubActions.Select(sub => FlattenPenumbraSubAction(group, sub)).ToList();

        private List<HeelsRuleAction> FlattenPenumbraGroupAction(HeelsRuleAction groupAction)
        {
            if (groupAction.Type != ActionType.Penumbra)
                return [];

            var group = EnsurePenumbraGroupOnAction(groupAction);
            return FlattenPenumbraGroup(group);
        }

        private List<HeelsRuleAction> FlattenRulePenumbraActions(HeelsRule rule) =>
            GetRuleActions(rule)
                .Where(a => a.Type == ActionType.Penumbra)
                .SelectMany(FlattenPenumbraGroupAction)
                .ToList();

        private bool ActionUsesPenumbraSubAction(PenumbraActionGroup group, PenumbraSubAction sub) =>
            ActionUsesPenumbra(FlattenPenumbraSubAction(group, sub));

        private bool ActionUsesPenumbra(HeelsRuleAction action)
        {
            if (string.IsNullOrWhiteSpace(action.PenumbraCollection)
                || string.IsNullOrWhiteSpace(action.PenumbraModName))
            {
                return false;
            }

            if (action.PenumbraActionKind == PenumbraActionKind.EnableMod
                || action.PenumbraActionKind == PenumbraActionKind.DisableMod)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(action.PenumbraOption))
                return false;

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
            FlattenRulePenumbraActions(rule).Any(ActionUsesPenumbra);

        private string GetActivePenumbraCollectionName() =>
            _penumbraInterop.GetDefaultCollectionName(GetLocalPlayerObjectIndex());

        private static int ToSfwGroupRuleIndex(int groupIndex) => SfwGroupRuleIndexBase - groupIndex;

        private static int ToPenumbraSubHostRuleIndex(int ruleIndex, int groupActionIndex) =>
            PenumbraSubHostRuleIndexBase - (ruleIndex * PenumbraSubHostRuleIndexStride + groupActionIndex);

        private bool TryParseSfwGroupRuleIndex(int ruleIndex, out int groupIndex)
        {
            groupIndex = -1;
            if (ruleIndex > SfwGroupRuleIndexBase)
                return false;

            groupIndex = -(ruleIndex - SfwGroupRuleIndexBase);
            var groups = Configuration.SfwModeActionGroups ?? [];
            return groupIndex >= 0 && groupIndex < groups.Count;
        }

        private bool TryParsePenumbraSubHostRuleIndex(int ruleIndex, out int hostRuleIndex, out int groupActionIndex)
        {
            hostRuleIndex = -1;
            groupActionIndex = -1;
            if (ruleIndex > PenumbraSubHostRuleIndexBase)
                return false;

            var encoded = -(ruleIndex - PenumbraSubHostRuleIndexBase);
            hostRuleIndex = encoded / PenumbraSubHostRuleIndexStride;
            groupActionIndex = encoded % PenumbraSubHostRuleIndexStride;
            var rules = ActiveRules;
            return hostRuleIndex >= 0
                && hostRuleIndex < rules.Count
                && groupActionIndex >= 0
                && groupActionIndex < rules[hostRuleIndex].Actions.Count;
        }

        private void MigrateSfwModeActionsToGroupsIfNeeded()
        {
            Configuration.SfwModeActionGroups ??= new List<PenumbraActionGroup>();
            if (Configuration.SfwModeActionGroups.Count > 0)
                return;

#pragma warning disable CS0618
            var legacyActions = Configuration.SfwModeActions ?? [];
#pragma warning restore CS0618
            if (legacyActions.Count == 0)
                return;

            var groupByKey = new Dictionary<string, PenumbraActionGroup>(StringComparer.OrdinalIgnoreCase);
            foreach (var action in legacyActions)
            {
                if (!ActionUsesPenumbra(action))
                    continue;

                var key = BuildPenumbraModKey(action.PenumbraCollection, action.PenumbraModName);
                if (!groupByKey.TryGetValue(key, out var group))
                {
                    group = new PenumbraActionGroup
                    {
                        PenumbraCollection = action.PenumbraCollection ?? "Default",
                        PenumbraModName = action.PenumbraModName ?? "",
                    };
                    groupByKey[key] = group;
                    Configuration.SfwModeActionGroups.Add(group);
                }

#pragma warning disable CS0618
                group.Actions.Add(action);
#pragma warning restore CS0618
            }
        }

        private void MigrateRulePenumbraActionsToGroups(HeelsRule rule)
        {
            if (rule.Actions == null || rule.Actions.Count == 0)
                return;

            var mergedActions = new List<HeelsRuleAction>();
            var penumbraGroupByKey = new Dictionary<string, HeelsRuleAction>(StringComparer.OrdinalIgnoreCase);

            foreach (var action in rule.Actions)
            {
                if (action.Type != ActionType.Penumbra)
                {
                    mergedActions.Add(action);
                    continue;
                }

                var group = EnsurePenumbraGroupOnAction(action);
                var key = BuildPenumbraModKey(group.PenumbraCollection, group.PenumbraModName);
                if (penumbraGroupByKey.TryGetValue(key, out var existingGroupAction))
                {
                    var existingGroup = EnsurePenumbraGroupOnAction(existingGroupAction);
                    existingGroup.SubActions.AddRange(group.SubActions);
                    continue;
                }

                mergedActions.Add(action);
                penumbraGroupByKey[key] = action;
            }

            rule.Actions = mergedActions;
        }

        private void MigrateAllPenumbraActionsToGroupsIfNeeded()
        {
            Configuration.SfwModeActionGroups ??= new List<PenumbraActionGroup>();
            foreach (var group in Configuration.SfwModeActionGroups)
                MigratePenumbraGroupLegacySubActions(group);

            foreach (var ruleSet in Configuration.RuleSets)
            {
                foreach (var rule in ruleSet.Rules)
                    MigrateRulePenumbraActionsToGroups(rule);
            }
        }

        /// <summary>
        /// 统一规则/行动折叠状态：旧版 IsActionsCollapsed 合并到 IsCollapsed；
        /// Penumbra 行动组 IsCollapsed 与外层 IsActionCollapsed 双向对齐。
        /// </summary>
        private void NormalizeCollapsePersistence()
        {
            foreach (var ruleSet in Configuration.RuleSets)
            {
                foreach (var rule in ruleSet.Rules)
                {
                    if (rule.IsActionsCollapsed && !rule.IsCollapsed)
                        rule.IsCollapsed = true;

                    foreach (var action in rule.Actions ?? [])
                    {
                        if (action.Type != ActionType.Penumbra)
                            continue;

                        var group = action.PenumbraGroup;
                        if (group == null)
                            continue;

                        var collapsed = action.IsActionCollapsed || group.IsCollapsed;
                        action.IsActionCollapsed = collapsed;
                        group.IsCollapsed = collapsed;
                    }
                }
            }
        }

        private static void SyncPenumbraActionCollapse(HeelsRuleAction action, PenumbraActionGroup group)
        {
            var collapsed = action.IsActionCollapsed || group.IsCollapsed;
            action.IsActionCollapsed = collapsed;
            group.IsCollapsed = collapsed;
        }

        private void ClearPenumbraGroupSubActionOptions(PenumbraActionGroup group)
        {
            foreach (var sub in group.SubActions)
            {
                sub.PenumbraOption = "";
                sub.PenumbraOptionName = "";
                sub.PenumbraOptionEnabled = false;
                sub.PenumbraMultiToggleStates.Clear();
            }
        }

        private void HandlePenumbraGroupTargetChanged(
            PenumbraActionGroup group,
            string previousCollection,
            string previousMod,
            bool modChanged,
            PenumbraApplyLayerConfig? dedupLayer)
        {
            if (modChanged)
                ClearPenumbraGroupSubActionOptions(group);

            SaveConfig();

            if (string.IsNullOrWhiteSpace(previousMod) || dedupLayer is not PenumbraApplyLayerConfig layer)
                return;

            ClearPenumbraDedupForMod(previousCollection, previousMod, layer);
            if (layer.IsSfwLayer && Configuration.SfwModeActive)
            {
                ClearPenumbraDedupForMod(previousCollection, previousMod, PenumbraApplyLayerConfig.Rule);
                lastApplyUtc = DateTime.MinValue;
            }
        }

        private List<HeelsRuleAction> GetAllSfwModePenumbraActions()
        {
            var result = new List<HeelsRuleAction>();
            foreach (var group in Configuration.SfwModeActionGroups ?? [])
            {
                MigratePenumbraGroupLegacySubActions(group);
                result.AddRange(FlattenPenumbraGroup(group).Where(ActionUsesPenumbra));
            }
            return result;
        }

        private HeelsRuleAction CreateDefaultPenumbraGroupRuleAction() =>
            new()
            {
                Type = ActionType.Penumbra,
                PenumbraGroup = CreateDefaultPenumbraActionGroup(),
            };

        private HeelsRuleAction CreateDefaultRuleActionForType(ActionType actionType) =>
            actionType switch
            {
                ActionType.Glamourer => new HeelsRuleAction { Type = ActionType.Glamourer },
                ActionType.Penumbra => CreateDefaultPenumbraGroupRuleAction(),
                ActionType.Honorific => new HeelsRuleAction { Type = ActionType.Honorific },
                ActionType.Moodles => new HeelsRuleAction { Type = ActionType.Moodles },
                ActionType.SoundMixer => new HeelsRuleAction { Type = ActionType.SoundMixer },
                _ => new HeelsRuleAction { Type = ActionType.Glamourer },
            };

        private static HeelsRule CreateEmptyRule() => new()
        {
            Name = "",
            IsCollapsed = false,
            IsActive = true,
            Actions = [new HeelsRuleAction { Type = ActionType.Glamourer }],
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

        private HeelsRuleAction CreateDefaultRuleAction() =>
            CreateDefaultRuleActionForType(ActionType.Glamourer);

        private static string BuildGlamourerActionKey(HeelsRuleAction action) =>
            $"G:{action.GlamourerDesign.Trim()}";

        private static string BuildSoundMixerActionKey(HeelsRuleAction action) =>
            action.SoundMixerActionKind switch
            {
                SoundMixerActionKind.TemporaryPreset =>
                    $"SM:Preset:{action.SoundMixerPresetName.Trim()}",
                SoundMixerActionKind.TemporaryGroupVolume =>
                    $"SM:Vol:{action.SoundMixerGroupName.Trim()}:{action.SoundMixerGroupVolume:F3}",
                _ => $"SM:{action.SoundMixerActionKind}",
            };

        private string BuildPenumbraActionKey(HeelsRuleAction action) =>
            BuildPenumbraActionKey(action, PenumbraApplyLayerConfig.Rule);

        private string BuildPenumbraActionKey(HeelsRuleAction action, PenumbraApplyLayerConfig layer) =>
            BuildPenumbraActionKeyWithPrefix(action, layer.KeyPrefix);

        private string BuildPenumbraActionKeyWithPrefix(HeelsRuleAction action, string keyPrefix)
        {
            var groupType = _penumbraInterop.GetOptionGroupType(
                action.PenumbraModName ?? "",
                action.PenumbraOption ?? "");
            if (PenumbraInterop.UsesBoolOptionValue(groupType))
            {
                var enabledNames = GetPenumbraMultiToggleEnabledNames(action);
                return $"{keyPrefix}{action.PenumbraCollection}|{action.PenumbraModName}|{action.PenumbraOption}|M:{string.Join(",", enabledNames)}";
            }

            return $"{keyPrefix}{action.PenumbraCollection}|{action.PenumbraModName}|{action.PenumbraOption}|{PenumbraInterop.BuildApplyStateKey(action.PenumbraOptionName, groupType, action.PenumbraOptionEnabled)}";
        }

        private string DescribePenumbraActionSummary(HeelsRuleAction action)
        {
            if (action.PenumbraActionKind == PenumbraActionKind.EnableMod)
                return $"Penumbra: [{action.PenumbraCollection}] {action.PenumbraModName} → Enable";
            if (action.PenumbraActionKind == PenumbraActionKind.DisableMod)
                return $"Penumbra: [{action.PenumbraCollection}] {action.PenumbraModName} → Disable";

            var groupType = _penumbraInterop.GetOptionGroupType(
                action.PenumbraModName ?? "",
                action.PenumbraOption ?? "");
            if (PenumbraInterop.UsesBoolOptionValue(groupType))
            {
                var toggleSummary = action.PenumbraMultiToggleStates
                    .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(pair =>
                        $"{pair.Key}={ (pair.Value ? Localization.PenumbraOptionEnabled : Localization.PenumbraOptionDisabled)}");
                return $"Penumbra: [{action.PenumbraCollection}] {action.PenumbraModName} → {action.PenumbraOption} = [{string.Join(", ", toggleSummary)}]";
            }

            return $"Penumbra: [{action.PenumbraCollection}] {action.PenumbraModName} → {action.PenumbraOption} = {action.PenumbraOptionName}";
        }

        private IReadOnlyList<string> GetPenumbraMultiToggleEnabledNames(HeelsRuleAction action) =>
            ResolvePenumbraMultiToggleEnabledNames(
                action.PenumbraCollection ?? "Default",
                action.PenumbraModName ?? "",
                action.PenumbraOption ?? "",
                action.PenumbraMultiToggleStates,
                GetActivePenumbraTempReadKey());

        private int GetActivePenumbraTempReadKey() =>
            GetActivePenumbraApplyLayer().IsSfwLayer
                ? PenumbraInterop.SfwModePenumbraLockKey
                : PenumbraInterop.HeelsDesignLinkerPenumbraLockKey;

        private List<string> ResolvePenumbraMultiToggleEnabledNames(
            string collection,
            string modDirectory,
            string optionGroup,
            IReadOnlyDictionary<string, bool> toggleStates,
            int tempReadKey)
        {
            var enabledSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<string> currentEnabled = [];
            if (_penumbraInterop.TryGetEffectiveEnabledOptionsForLayer(
                    collection,
                    modDirectory,
                    optionGroup,
                    tempReadKey,
                    out currentEnabled,
                    out _)
                || _penumbraInterop.TryGetCollectionEnabledOptions(
                    collection,
                    modDirectory,
                    optionGroup,
                    out currentEnabled,
                    out _))
            {
                foreach (var name in currentEnabled)
                {
                    if (!string.IsNullOrWhiteSpace(name))
                        enabledSet.Add(name.Trim());
                }
            }

            // 仅对规则里显式配置的 toggle 做 delta，未出现在 toggleStates 中的项保持当前生效状态。
            foreach (var (name, enabled) in toggleStates)
            {
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var trimmed = name.Trim();
                if (enabled)
                    enabledSet.Add(trimmed);
                else
                    enabledSet.Remove(trimmed);
            }

            return enabledSet
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

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
                    action.PenumbraMultiToggleStates[legacyOption] = true;
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

            return changed;
        }

        private static string BuildPenumbraGroupKey(HeelsRuleAction action) =>
            $"{action.PenumbraCollection}|{action.PenumbraModName}|{action.PenumbraOption}";

        private static List<string> GetPenumbraSingleSelectTargetNames(HeelsRuleAction action)
        {
            if (!string.IsNullOrWhiteSpace(action.PenumbraOptionName))
                return [action.PenumbraOptionName.Trim()];

            return [];
        }

        private static string FormatGlamourerDesignArgument(string designName)
        {
            var trimmed = designName.Trim();
            if (trimmed.Contains(' ') || trimmed.Contains('"'))
                return $"\"{trimmed.Replace("\"", "\\\"")}\"";

            return trimmed;
        }

        private void OnMatchedRuleSetChanged(
            List<HeelsRule> activeRules,
            List<int> appliedIndices,
            string newSignature,
            IPlayerCharacter? localPlayer)
        {
            if (newSignature == lastMatchedSetSignature)
                return;

            var hadMatch = !string.IsNullOrEmpty(lastMatchedSetSignature);
            var hasMatch = appliedIndices.Count > 0;

            ClearSoundMixerTemporaryOverrides();

            if (!hasMatch)
            {
                ClearPenumbraTemporaryOverrides();
                ClearPenumbraApplyTracking();
                lastAppliedHonorificJson = "";
                lastAppliedMoodleKey = "";
                lastApplyUtc = DateTime.MinValue;
            }
            else if (hadMatch)
            {
                // 命中集合 A → B：先清 -1211，再 apply；本周期 batch 禁止合并旧临时选项组
                ClearPenumbraTemporaryOverrides(forceRemove: true);
            }

            if (hasMatch)
            {
                // 与 UpdateRuleMatchStability 双保险：apply 前确保整组规则行动各 apply 一次
                ClearPenumbraApplyTracking();
                lastAppliedHonorificJson = "";
                lastAppliedMoodleKey = "";
            }

            // 清除 Honorific（如果新命中集合中没有任何规则需要它）
            var hadHonorific = !string.IsNullOrEmpty(lastAppliedHonorificJson);
            var wantsHonorific = false;
            foreach (var idx in appliedIndices)
            {
                if (idx >= 0 && idx < activeRules.Count && RuleUsesHonorific(activeRules[idx]))
                {
                    wantsHonorific = true;
                    break;
                }
            }

            if (hadHonorific
                && !wantsHonorific
                && localPlayer != null
                && localPlayer.IsValid())
            {
                _honorificInterop.TryClearLocalTitle(localPlayer, out _);
            }

            lastMatchedSetSignature = newSignature;
            lastMatchedRuleIndex = hasMatch ? appliedIndices[0] : -1;
        }

        /// <summary>
        /// 计算本帧命中并将被应用的规则索引集合（按列表顺序）。
        /// 规则间连接符：Or（默认）= 互斥（同一 OR 块 first-match-wins）；And = 共存（开新独立块继续匹配并叠加）。
        /// 旧配置全为 Or → 等价于原有 first-match-then-break 行为。
        /// </summary>
        private void CollectAppliedRuleIndices(List<HeelsRule> rules, float height, List<int> result)
        {
            result.Clear();
            var blockMatched = false;
            for (var i = 0; i < rules.Count; i++)
            {
                var independent = i == 0 || rules[i - 1].OperatorToNext == LogicOperator.And;
                if (independent)
                    blockMatched = false;

                if (blockMatched)
                    continue; // 同一 OR 块内已命中 → 互斥跳过

                if (TryMatchRule(rules[i], i, height, allowEquipmentEvaluation: true))
                {
                    result.Add(i);
                    blockMatched = true;
                }
            }
        }

        private static string BuildMatchedSetSignature(List<int> appliedIndices)
        {
            if (appliedIndices.Count == 0)
                return "";
            return string.Join(",", appliedIndices);
        }

        private static void FixMisplacedElseBranches(List<HeelsRule> rules)
        {
            // 分组首条必须是“如果”（条件分支）：它是全局首条，或上一条连接符为 And（开启新分组）。
            // “否则”只能作为其分组的最后一条：它是全局最后一条，或其连接符为 And（结束本分组）。
            // 处于分组中间（连接符为 Or 且非全局末尾）的否则会让组内后续规则不可达，降级为“否则如果”。
            for (var i = 0; i < rules.Count; i++)
            {
                var isGroupStart = i == 0 || rules[i - 1].OperatorToNext == LogicOperator.And;
                if (isGroupStart)
                {
                    // 分组首条不能是“否则”（否则会无条件命中）；统一规整为“如果”（即 ElseIf 条件分支，UI 渲染为“如果”）。
                    if (rules[i].BranchKind == RuleBranchKind.Else)
                        rules[i].BranchKind = RuleBranchKind.ElseIf;
                    continue;
                }

                if (rules[i].BranchKind != RuleBranchKind.Else)
                    continue;

                var isLastInGroup = i == rules.Count - 1 || rules[i].OperatorToNext == LogicOperator.And;
                if (!isLastInGroup)
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

        private bool RuleUsesSoundMixer(HeelsRule rule) =>
            GetRuleActions(rule).Any(ActionUsesSoundMixer);

        private static bool ActionUsesSoundMixer(HeelsRuleAction action)
        {
            if (action.Type != ActionType.SoundMixer)
                return false;

            return action.SoundMixerActionKind switch
            {
                SoundMixerActionKind.TemporaryPreset => !string.IsNullOrWhiteSpace(action.SoundMixerPresetName),
                SoundMixerActionKind.TemporaryGroupVolume => !string.IsNullOrWhiteSpace(action.SoundMixerGroupName),
                _ => false,
            };
        }

        private bool ConfigurationUsesGlamourer() =>
            Configuration.RuleSets.Any(rs => rs.Rules.Any(r => r.IsActive && RuleUsesGlamourer(r)));

        private bool ConfigurationUsesPenumbra() =>
            Configuration.RuleSets.Any(rs => rs.Rules.Any(r => r.IsActive && RuleUsesPenumbra(r)));

        private bool ConfigurationUsesHonorific() =>
            Configuration.RuleSets.Any(rs => rs.Rules.Any(r => r.IsActive && RuleUsesHonorific(r)));

        private bool ConfigurationUsesMoodles() =>
            Configuration.RuleSets.Any(rs => rs.Rules.Any(r => r.IsActive && RuleUsesMoodles(r)));

        private bool ConfigurationUsesSoundMixer() =>
            Configuration.RuleSets.Any(rs => rs.Rules.Any(r => r.IsActive && RuleUsesSoundMixer(r)));

        private bool HasConfiguredOutput() =>
            ConfigurationUsesGlamourer()
            || ConfigurationUsesPenumbra()
            || ConfigurationUsesHonorific()
            || ConfigurationUsesMoodles()
            || ConfigurationUsesSoundMixer();

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

            if (ConfigurationUsesSoundMixer() && isSoundMixerIpcReady)
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

            RefreshPenumbraDependencyState();
            UpdatePenumbraStatusDisplay();
            isMoodlesIpcReady = _moodlesInterop.IsIpcAvailable();
            isHonorificIpcReady = _honorificInterop.IsIpcAvailable();
            RefreshSoundMixerDependencyState();
            UpdateSoundMixerStatusDisplay();
            if (isSoundMixerIpcReady)
                _soundMixerInterop.RefreshData(force: false);
            // isCustomizePlusIpcReady = _customizePlusInterop.IsIpcAvailable();  // 已移除 Customize+ 支持

            var nowReady = IsReadyForWork();
            if (nowReady && !wasReady)
            {
                if (ConfigurationUsesPenumbra() && isPenumbraIpcReady)
                    _penumbraInterop.RefreshData(force: true);
            }
        }

        private void OnLogin()
        {
            loginSinceUtc = DateTime.UtcNow;
            isLoginProtectionActive = true;
            ResetApplyState();
            RestoreShutdownApplySnapshot();
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

        private bool TryGetActiveRuleSet(out RuleSet ruleSet)
        {
            ruleSet = null!;
            if (Configuration.RuleSets.Count == 0
                || Configuration.ActiveRuleSetIndex < 0
                || Configuration.ActiveRuleSetIndex >= Configuration.RuleSets.Count)
            {
                return false;
            }

            ruleSet = Configuration.RuleSets[Configuration.ActiveRuleSetIndex];
            return true;
        }

        /// <summary>
        /// 启用基准行动时，须等基准行动允许窗口到达后再与规则行动同周期 apply（同周期内基准先于规则）。
        /// 避免登录保护刚结束、基准仍有 PostLoginBaselineDelay 时规则已先 apply 一轮。
        /// </summary>
        private bool IsBaselineBlockingRuleApply()
        {
            if (!TryGetActiveRuleSet(out var ruleSet) || !ruleSet.UseBaselineActions)
                return false;

            return !IsBaselineApplyAllowed();
        }

        private bool IsBaselineGlamourerRevertAllowed()
        {
            if (!loginSinceUtc.HasValue)
                return true;

            return DateTime.UtcNow - loginSinceUtc.Value >= BaselineGlamourerRevertDelay;
        }

        private bool IsPlayerInWorld()
        {
            if (Condition[ConditionFlag.BetweenAreas] || Condition[ConditionFlag.BetweenAreas51])
                return false;

            return true;
        }

        private void OnLogout(int type, int code)
        {
            PersistShutdownApplySnapshot();
            loginSinceUtc = null;
            isLoginProtectionActive = false;
            ResetApplyState();
            ResetPenumbraStatusDisplay();
            soundMixerIpcLastReadyUtc = null;
            soundMixerLoadedLastTrueUtc = null;
            isSoundMixerIpcReady = false;
            isSoundMixerLoaded = false;
        }

        private void ResetApplyState()
        {
            localPlayerStableSinceUtc = null;
            appearancePopulatedSinceUtc = null;
            baselineActionsAllowedAfterUtc = null;
            lastApplyUtc = DateTime.MinValue;
            ClearPenumbraApplyTracking();
            // 勿 ResetPenumbraStatusDisplay：会强制 isPenumbraIpcReady=false，中途启用插件后须等 Refresh 间隔或开面板才恢复
            RefreshPenumbraDependencyState();
            RefreshSoundMixerDependencyState();
            ClearSoundMixerTemporaryOverrides();
            ClearPenumbraTemporaryOverrides(forceRemove: true);
            ClearSfwPenumbraTemporaryOverrides(forceRemove: true);
            lastAppliedHonorificJson = "";
            lastAppliedMoodleKey = "";
            lastMatchedRuleIndex = -1;
            stableTrackingRuleIndex = -1;
            currentAppliedRuleIndices.Clear();
            lastMatchedSetSignature = "";
            stableTrackingSignature = null;
            ruleMatchStableSinceUtc = null;
            lastRuleMatchingHeight = float.NaN;
            lastTempOffsetSeenUtc = null;
            lastKnownActualHeelsHeight = 0f;
            _appearanceChangeTracker.Reset();
        }

        private void ClearPenumbraApplyTracking()
        {
            lastAppliedActionKeys.Clear();
            penumbraApplyAttemptUtcByKey.Clear();
            penumbraDedupTrustedUntilUtcByKey.Clear();
            penumbraDriftStreakByKey.Clear();
        }

        private void ResetPenumbraStatusDisplay()
        {
            isPenumbraLoaded = false;
            isPenumbraIpcReady = false;
            penumbraIpcLastReadyUtc = null;
            penumbraLoadedLastTrueUtc = null;
            penumbraRawErrorSinceUtc = null;
            penumbraStatusDisplayError = false;
            penumbraStatusDisplayText = "";
        }

        private void RefreshPenumbraDependencyState()
        {
            var loadedNow = PenumbraInterop.IsPenumbraLoaded(PluginInterface);
            if (loadedNow)
            {
                isPenumbraLoaded = true;
                penumbraLoadedLastTrueUtc = DateTime.UtcNow;
            }
            else if (penumbraLoadedLastTrueUtc.HasValue
                     && DateTime.UtcNow - penumbraLoadedLastTrueUtc.Value < PenumbraIpcReadyGrace)
            {
            }
            else
            {
                isPenumbraLoaded = false;
            }

            var ipcNow = _penumbraInterop.IsIpcAvailable();
            if (ipcNow)
            {
                isPenumbraIpcReady = true;
                penumbraIpcLastReadyUtc = DateTime.UtcNow;
            }
            else if (penumbraIpcLastReadyUtc.HasValue
                     && DateTime.UtcNow - penumbraIpcLastReadyUtc.Value < PenumbraIpcReadyGrace)
            {
            }
            else
            {
                isPenumbraIpcReady = false;
            }
        }

        private void UpdatePenumbraStatusDisplay()
        {
            var rawHasError = !isPenumbraLoaded || !isPenumbraIpcReady;
            var statusText = !isPenumbraLoaded
                ? $"✗ {Localization.NotAvailable}"
                : isPenumbraIpcReady
                    ? $"✓ {Localization.PenumbraIpcReady}"
                    : Localization.PluginLoadedIpcPending;

            if (!rawHasError)
            {
                penumbraRawErrorSinceUtc = null;
                penumbraStatusDisplayError = false;
                penumbraStatusDisplayText = statusText;
                return;
            }

            if (!penumbraRawErrorSinceUtc.HasValue)
                penumbraRawErrorSinceUtc = DateTime.UtcNow;

            if (DateTime.UtcNow - penumbraRawErrorSinceUtc.Value < PenumbraStatusErrorDisplayDelay)
                return;

            penumbraStatusDisplayError = true;
            penumbraStatusDisplayText = statusText;
        }

        private void RefreshSoundMixerDependencyState()
        {
            var loadedNow = SoundMixerInterop.IsSoundMixerLoaded(PluginInterface);
            if (loadedNow)
            {
                isSoundMixerLoaded = true;
                soundMixerLoadedLastTrueUtc = DateTime.UtcNow;
            }
            else if (soundMixerLoadedLastTrueUtc.HasValue
                     && DateTime.UtcNow - soundMixerLoadedLastTrueUtc.Value < PenumbraIpcReadyGrace)
            {
            }
            else
            {
                isSoundMixerLoaded = false;
            }

            var ipcNow = _soundMixerInterop.TryGetApiVersion(out var apiVersion);
            if (ipcNow)
            {
                isSoundMixerIpcReady = true;
                soundMixerIpcLastReadyUtc = DateTime.UtcNow;
                soundMixerDetectedApiVersion = apiVersion;
                soundMixerApiVersionCompatible = _soundMixerInterop.IsApiVersionCompatible(apiVersion);
            }
            else if (soundMixerIpcLastReadyUtc.HasValue
                     && DateTime.UtcNow - soundMixerIpcLastReadyUtc.Value < PenumbraIpcReadyGrace)
            {
            }
            else
            {
                isSoundMixerIpcReady = false;
                soundMixerDetectedApiVersion = 0;
                soundMixerApiVersionCompatible = true;
            }
        }

        private void UpdateSoundMixerStatusDisplay()
        {
            var rawHasError = !isSoundMixerLoaded || !isSoundMixerIpcReady;
            var statusText = !isSoundMixerLoaded
                ? $"✗ {Localization.NotAvailable}"
                : isSoundMixerIpcReady
                    ? $"✓ {Localization.SoundMixerIpcReady}"
                    : Localization.PluginLoadedIpcPending;

            if (!rawHasError)
            {
                soundMixerRawErrorSinceUtc = null;
                soundMixerStatusDisplayError = false;
                soundMixerStatusDisplayText = statusText;
                return;
            }

            if (!soundMixerRawErrorSinceUtc.HasValue)
                soundMixerRawErrorSinceUtc = DateTime.UtcNow;

            if (DateTime.UtcNow - soundMixerRawErrorSinceUtc.Value < PenumbraStatusErrorDisplayDelay)
                return;

            soundMixerStatusDisplayError = true;
            soundMixerStatusDisplayText = statusText;
        }

        private void RefreshSoundMixerIpcGateProbesIfNeeded()
        {
            if (!isSoundMixerLoaded)
            {
                soundMixerIpcGateProbes = [];
                return;
            }

            var now = DateTime.UtcNow;
            if ((now - soundMixerIpcProbeUtc).TotalSeconds < 5)
                return;

            soundMixerIpcProbeUtc = now;
            soundMixerIpcGateProbes = _soundMixerInterop.ProbeReadOnlyIpcGates();
        }

        private void MarkPenumbraApplyAttempt(string applyKey) =>
            penumbraApplyAttemptUtcByKey[applyKey] = DateTime.UtcNow;

        private void MarkPenumbraDedupTrusted(string applyKey) =>
            penumbraDedupTrustedUntilUtcByKey[applyKey] = DateTime.UtcNow + PenumbraDedupTrustInterval;

        private static string BuildPenumbraAutoEnableApplyKey(string collection, string modDirectory, string keyPrefix = "P:") =>
            $"{keyPrefix}SetMod:{collection}|{modDirectory}|true";

        private void MarkPenumbraAutoEnableDedupSatisfied(string collection, string modDirectory, PenumbraApplyLayerConfig layer)
        {
            var enableKey = BuildPenumbraAutoEnableApplyKey(collection, modDirectory, layer.KeyPrefix);
            lastAppliedActionKeys.Add(enableKey);
            MarkPenumbraDedupTrusted(enableKey);
        }

        private void ClearPenumbraDedupForMod(string collection, string modDirectory, PenumbraApplyLayerConfig layer)
        {
            var prefix = $"{layer.KeyPrefix}{collection}|{modDirectory}|";
            var enableKey = BuildPenumbraAutoEnableApplyKey(collection, modDirectory, layer.KeyPrefix);
            foreach (var key in lastAppliedActionKeys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal) || k == enableKey).ToList())
            {
                lastAppliedActionKeys.Remove(key);
                penumbraDedupTrustedUntilUtcByKey.Remove(key);
                penumbraApplyAttemptUtcByKey.Remove(key);
                penumbraDriftStreakByKey.Remove(key);
            }
        }

        private bool IsPenumbraDedupTrusted(string applyKey) =>
            penumbraDedupTrustedUntilUtcByKey.TryGetValue(applyKey, out var until)
            && DateTime.UtcNow < until;

        private bool IsPenumbraApplyThrottled(string applyKey) =>
            penumbraApplyAttemptUtcByKey.TryGetValue(applyKey, out var lastAttempt)
            && DateTime.UtcNow - lastAttempt < PenumbraUnverifiedRetryInterval;

        private void UpdateRuleMatchStability(string matchedSignature, bool hasMatch)
        {
            // 命中集合相对上次已 apply 的集合变化时，须先清去重/冷却（不可被下方稳定计时 early-return 跳过）。
            // 否则 1+3→1+4 等重叠规则场景下，仍留在 dedup 中的旧键会导致仅新增规则被 apply。
            if (matchedSignature != lastMatchedSetSignature)
            {
                if (!hasMatch)
                {
                    // 短暂无匹配：不清 Penumbra 临时层（避免闪回永久 Shoes），保留 dedup
                    ClearSoundMixerTemporaryOverrides();
                }
                else
                {
                    ClearSoundMixerTemporaryOverrides();
                    ClearPenumbraApplyTracking();
                    lastAppliedHonorificJson = "";
                    lastAppliedMoodleKey = "";
                    lastApplyUtc = DateTime.MinValue;
                }
            }

            if (stableTrackingSignature != null && matchedSignature == stableTrackingSignature)
                return;

            stableTrackingSignature = matchedSignature;
            stableTrackingRuleIndex = hasMatch ? currentMatchedRuleIndex : -1;
            ruleMatchStableSinceUtc = DateTime.UtcNow;
        }

        private bool IsRuleMatchStableElapsed(out string status)
        {
            var delay = Math.Max(0f, Configuration.RuleMatchStableSeconds);
            if (loginSinceUtc.HasValue
                && DateTime.UtcNow - loginSinceUtc.Value < PostLoginFastMatchWindow)
            {
                delay = Math.Min(delay, PostLoginRuleMatchStableCapSeconds);
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

        private static string BuildRuleMoodleApplyKey(HeelsRuleAction action) =>
            $"M:{action.MoodleGuid}|{(action.MoodleIsPreset ? "preset" : "status")}";

        private void ClearRuleMoodleApplyDedupKeys()
        {
            foreach (var key in lastAppliedActionKeys
                         .Where(k => k.StartsWith("M:", StringComparison.Ordinal))
                         .ToList())
            {
                lastAppliedActionKeys.Remove(key);
            }
        }

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
            status = Localization.ApplyCooldownRemaining(remaining);
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
                status = Localization.WaitingForLocalPlayer;
                return false;
            }

            if (!TryGetMainHandEquippedItemId(out var mainHandItemId))
            {
                ResetAppearanceReadyTracking();
                status = Localization.WaitingForMainHandEquipmentData;
                return false;
            }

            if (!IsMainHandAppearanceAnchor(mainHandItemId))
            {
                ResetAppearanceReadyTracking();
                status = Localization.WaitingForMainHandEquipment;
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
                status = Localization.MainHandAnchorStabilizing(remaining.TotalSeconds);
                return false;
            }

            return true;
        }

        /// <summary>登录保护期内：会话就绪 + 最短预热 + 主手锚点稳定。</summary>
        private bool IsStartupSessionReady(out string gateStatus)
        {
            gateStatus = "";

            if (!TryGetBaseSessionReady(out gateStatus))
                return false;

            localPlayerStableSinceUtc ??= DateTime.UtcNow;

            if (loginSinceUtc.HasValue)
            {
                var loginElapsed = DateTime.UtcNow - loginSinceUtc.Value;
                if (loginElapsed < LoginProtectionMinDuration)
                {
                    gateStatus = Localization.LoginPrepRemaining(
                        (LoginProtectionMinDuration - loginElapsed).TotalSeconds);
                    return false;
                }
            }

            return IsMainHandAnchorReady(MainHandAnchorStableDelay, out gateStatus);
        }

        /// <summary>登录保护结束后：过图/加载与本地玩家恢复稳定（不重复登录预热）。</summary>
        private bool IsSessionReadyForRules(out string gateStatus)
        {
            gateStatus = "";

            if (!TryGetBaseSessionReady(out gateStatus))
                return false;

            var now = DateTime.UtcNow;
            localPlayerStableSinceUtc ??= now;
            var elapsed = now - localPlayerStableSinceUtc.Value;
            if (elapsed < SessionRecoveryStableDelay)
            {
                gateStatus = Localization.StartupDelayRemaining(
                    (SessionRecoveryStableDelay - elapsed).TotalSeconds);
                return false;
            }

            return true;
        }

        private bool TryGetBaseSessionReady(out string gateStatus)
        {
            gateStatus = "";

            if (!ClientState.IsLoggedIn)
            {
                localPlayerStableSinceUtc = null;
                gateStatus = Localization.NotLoggedIn;
                return false;
            }

            if (!IsLocalPlayerReady())
            {
                localPlayerStableSinceUtc = null;
                gateStatus = Localization.WaitingForLocalPlayer;
                return false;
            }

            if (!IsPlayerInWorld())
            {
                gateStatus = Localization.BetweenAreasLoading;
                return false;
            }

            return true;
        }

        /// <summary>刷新 SimpleHeels 高度。登录保护期内 IPC 未就绪不阻塞启动门控。</summary>
        private bool TryRefreshHeelsHeight(bool useSimpleHeels)
        {
            if (!useSimpleHeels || !isSimpleHeelsAvailable || !isSimpleHeelsIpcReady)
            {
                currentHeelsDefaultHeight = 0f;
                currentHeelsActualHeight = 0f;
                currentHeelsHasTempOffset = false;
                currentHeelsHeight = 0f;
                lastError = "";
                lastIpcData = !useSimpleHeels
                    ? Localization.SimpleHeelsDisabledIpc
                    : Localization.SimpleHeelsUnavailableIpc;
                return true;
            }

            try
            {
                var getLocalPlayerProvider = PluginInterface.GetIpcSubscriber<string>("SimpleHeels.GetLocalPlayer");
                var localPlayerData = getLocalPlayerProvider.InvokeFunc();
                lastIpcData = localPlayerData ?? "NULL";

                if (string.IsNullOrEmpty(localPlayerData))
                {
                    lastError = "SimpleHeels 返回空数据";
                    currentHeelsDefaultHeight = 0f;
                    currentHeelsActualHeight = 0f;
                    currentHeelsHasTempOffset = false;
                    currentHeelsHeight = 0f;
                    return false;
                }

                if (!TryParseHeelsHeights(localPlayerData, out var defaultHeight, out var actualHeight, out var hasTempOffset))
                    return false;

                currentHeelsDefaultHeight = defaultHeight;
                currentHeelsActualHeight = actualHeight;
                currentHeelsHasTempOffset = hasTempOffset;
                currentHeelsHeight = SelectHeelsHeightForMode(defaultHeight, actualHeight, hasTempOffset);
                lastError = "";
                return true;
            }
            catch (Exception ex)
            {
                lastError = $"IPC 错误: {ex.Message}";
                PluginLog.Error($"SimpleHeels IPC failed: {ex}");
                currentHeelsDefaultHeight = 0f;
                currentHeelsActualHeight = 0f;
                currentHeelsHasTempOffset = false;
                currentHeelsHeight = 0f;
                return false;
            }
        }

        private void OnFrameworkUpdate(IFramework framework)
        {
            RefreshDependenciesIfNeeded();
            try
            {
                if (!IsReadyForWork())
                    return;

                if (!ClientState.IsLoggedIn)
                {
                    applyGateStatus = Localization.NotLoggedIn;
                    loginSinceUtc = null;
                    isLoginProtectionActive = false;
                    ResetApplyState();
                    return;
                }

            if (!loginSinceUtc.HasValue)
            {
                loginSinceUtc = DateTime.UtcNow;
                isLoginProtectionActive = true;
                ResetApplyState();
                RestoreShutdownApplySnapshot();
                // 插件在游戏内中途启用：立即重检依赖，避免等 2s 间隔或开面板后才 apply
                RefreshDependencies(force: true);
            }

            var appearanceChanged = _appearanceChangeTracker.CheckChanged(
                ObjectTable?.LocalPlayer,
                out var appearanceModelIdsChanged);

            if (appearanceModelIdsChanged && IsLoginProtectionActive())
                ResetAppearanceReadyTracking();

            var useSimpleHeels = Configuration.RuleSets.Count > 0
                && Configuration.ActiveRuleSetIndex >= 0
                && Configuration.ActiveRuleSetIndex < Configuration.RuleSets.Count
                && Configuration.RuleSets[Configuration.ActiveRuleSetIndex].UseSimpleHeels;

            if (IsLoginProtectionActive())
            {
                if (!IsStartupSessionReady(out applyGateStatus))
                    return;

                EndLoginProtection();
            }

            var heelsReady = TryRefreshHeelsHeight(useSimpleHeels);
            _penumbraInterop.ReadIncludingTemporarySettings = Configuration.UsePenumbraTemporaryApply;

            var matchingHeightChanged = HasRuleMatchingHeightChanged(currentHeelsHeight);
            if (matchingHeightChanged)
                lastRuleMatchingHeight = currentHeelsHeight;

            if (matchingHeightChanged || appearanceChanged)
            {
                stableTrackingRuleIndex = -1;
                stableTrackingSignature = null;
                ruleMatchStableSinceUtc = null;
            }

            if (!heelsReady)
            {
                applyGateStatus = lastError;
                return;
            }

            if (!IsSessionReadyForRules(out applyGateStatus))
                return;

            var activeRules = ActiveRules;
            CollectAppliedRuleIndices(activeRules, currentHeelsHeight, currentAppliedRuleIndices);
            currentMatchedRuleIndex = currentAppliedRuleIndices.Count > 0 ? currentAppliedRuleIndices[0] : -1;
            HeelsRule? matchedRule = currentMatchedRuleIndex >= 0 ? activeRules[currentMatchedRuleIndex] : null;
            var matchedSignature = BuildMatchedSetSignature(currentAppliedRuleIndices);

            UpdateRuleMatchStability(matchedSignature, currentAppliedRuleIndices.Count > 0);

            var localPlayer = ObjectTable.LocalPlayer;

            if (matchedRule == null)
            {
                if (!IsRuleMatchStableElapsed(out var noMatchStableStatus))
                {
                    applyGateStatus = noMatchStableStatus;
                    return;
                }

                OnMatchedRuleSetChanged(activeRules, currentAppliedRuleIndices, "", localPlayer);

                if (localPlayer == null || !localPlayer.IsValid())
                    return;

                if (TryConsumeRestoredShutdownMatchSkip(""))
                    return;

                var noMatchAppliedAnything = false;
                var noMatchConfigDirty = false;
                appliedActionSummariesThisCycle.Clear();
                ApplySfwModeIfActive(ref noMatchAppliedAnything, ref noMatchConfigDirty);
                if (noMatchConfigDirty)
                    SaveConfig();
                if (noMatchAppliedAnything)
                    lastApplyUtc = DateTime.UtcNow;
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

            if (IsBaselineBlockingRuleApply())
            {
                applyGateStatus = Localization.BaselineApplyWaiting;
                return;
            }

            if (localPlayer == null || !localPlayer.IsValid())
                return;

            OnMatchedRuleSetChanged(activeRules, currentAppliedRuleIndices, matchedSignature, localPlayer);

            if (TryConsumeRestoredShutdownMatchSkip(matchedSignature))
                return;

            var appliedAnything = false;
            var configDirty = false;
            appliedActionSummariesThisCycle.Clear();
            baselineMoodleAppliedThisCycle = false;

            // 收集本周期全部命中规则（主规则 + AND 共存附加规则），按列表顺序（后覆盖前）。
            var appliedRules = new List<HeelsRule>(currentAppliedRuleIndices.Count);
            foreach (var appliedIndex in currentAppliedRuleIndices)
                appliedRules.Add(activeRules[appliedIndex]);

            var ruleActions = new List<HeelsRuleAction>();
            foreach (var appliedRule in appliedRules)
                ruleActions.AddRange(GetRuleActions(appliedRule));

            // 应用基准行动（如果启用；已在上方门控，此处与规则同周期且必定先于规则行动）
            if (TryGetActiveRuleSet(out var activeRuleSet) && activeRuleSet.UseBaselineActions)
            {
                var newCount = UpdateBaselineConfigs(activeRuleSet);
                if (newCount > 0)
                    PluginInterface.SavePluginConfig(Configuration);

                ApplyBaselineActions(activeRuleSet, matchedRule, ref appliedAnything);
            }

            // 基准 Moodles 可能清 buff / 覆盖状态：同周期内须再让规则 Moodles apply 一次，但不跨周期反复。
            if (baselineMoodleAppliedThisCycle)
                ClearRuleMoodleApplyDedupKeys();

            if (ApplyPenumbraActionsForRules(appliedRules, ref appliedAnything))
                configDirty = true;

            // Glamourer：按优先级升序应用（数值大者最后写入，冲突槽位胜出；非冲突槽位共存）
            ApplyGlamourerActionsWithPriority(ruleActions, ref appliedAnything);

            foreach (var action in ruleActions)
            {
                switch (action.Type)
                {
                    case ActionType.Glamourer:
                        break; // 已由 ApplyGlamourerActionsWithPriority 统一按优先级处理
                    case ActionType.Penumbra:
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
                                RecordAppliedActionSummary("Honorific");
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
                            var moodleKey = BuildRuleMoodleApplyKey(action);
                            if (!lastAppliedActionKeys.Contains(moodleKey))
                            {
                                if (_moodlesInterop.TryApply(localPlayer, moodleId, action.MoodleIsPreset, out var moodlesError))
                                {
                                    lastAppliedActionKeys.Add(moodleKey);
                                    lastAppliedMoodleKey = moodleKey[2..];
                                    appliedAnything = true;
                                    lastError = "";
                                    RecordAppliedActionSummary("Moodles");
                                }
                                else
                                {
                                    lastError = moodlesError;
                                    PluginLog.Warning($"Moodles IPC apply failed: {moodlesError}");
                                }
                            }
                        }
                        break;
                    case ActionType.SoundMixer:
                        if (isSoundMixerIpcReady)
                            ApplySoundMixerAction(action, ref appliedAnything);
                        break;
                }
            }

            ApplySfwModeIfActive(ref appliedAnything, ref configDirty);

            if (configDirty)
                SaveConfig();

            if (appliedAnything)
            {
                lastApplyUtc = DateTime.UtcNow;
                lastExecutedRuleIndex = currentMatchedRuleIndex;
                var samePrimaryDisplay =
                    lastExecutedActionUtc.HasValue
                    && lastExecutedActionSummaries.Count == appliedActionSummariesThisCycle.Count
                    && lastExecutedActionSummaries.SequenceEqual(appliedActionSummariesThisCycle);
                if (!samePrimaryDisplay)
                {
                    lastExecutedActionUtc = DateTime.UtcNow;
                    lastExecutedActionSummaries.Clear();
                    lastExecutedActionSummaries.AddRange(appliedActionSummariesThisCycle);
                }
            }
            }
            finally
            {
                UpdateDtrBar();
            }
        }

        private void RecordAppliedActionSummary(string summary)
        {
            if (!string.IsNullOrWhiteSpace(summary))
                appliedActionSummariesThisCycle.Add(summary);
        }

        private void ClearSoundMixerTemporaryOverrides()
        {
            if (!soundMixerOverridesActive)
                return;

            if (isSoundMixerIpcReady)
                _soundMixerInterop.TryRemoveTemporaryOverrides(out _);

            soundMixerOverridesActive = false;
        }

        private void ClearPenumbraTemporaryOverrides(bool forceRemove = false)
        {
            if (!forceRemove && !penumbraTemporaryOverridesActive)
                return;

            var localPlayer = ObjectTable.LocalPlayer;
            if (localPlayer != null
                && localPlayer.IsValid()
                && isPenumbraIpcReady)
            {
                _penumbraInterop.TryRemoveAllHeelsTemporaryModSettingsPlayer(
                    localPlayer.ObjectIndex,
                    out _);
                penumbraApplyWithoutTempMerge = true;
            }

            penumbraTemporaryOverridesActive = false;
        }

        private static int? GetLocalPlayerObjectIndex()
        {
            var localPlayer = ObjectTable.LocalPlayer;
            if (localPlayer == null || !localPlayer.IsValid())
                return null;

            return localPlayer.ObjectIndex;
        }

        private bool IsPenumbraModUnderGlamourerTakeover(string modDirectory)
        {
            if (string.IsNullOrWhiteSpace(modDirectory))
                return false;

            var playerIndex = GetLocalPlayerObjectIndex();
            if (playerIndex == null)
                return false;

            return _penumbraInterop.IsModUnderGlamourerTakeover(playerIndex.Value, modDirectory);
        }

        private bool IsPenumbraActionUnderGlamourerTakeover(HeelsRuleAction action)
        {
            if (action.Type != ActionType.Penumbra)
                return false;

            return IsPenumbraModUnderGlamourerTakeover(action.PenumbraModName ?? "");
        }

        /// <summary>
        /// 该 Mod 的临时层覆盖是否“来自自己”：本插件 key 直接写入（-1211/-1210），
        /// 或由当前命中规则中的某个 Glamourer 设计行动间接让 Glamourer 接管（设计关联了该 Mod）。
        /// </summary>
        private bool IsPenumbraModSelfManaged(string modDirectory)
        {
            if (string.IsNullOrWhiteSpace(modDirectory))
                return false;

            var playerIndex = GetLocalPlayerObjectIndex();
            if (playerIndex == null)
                return false;

            var owner = _penumbraInterop.GetModTemporaryOverrideOwner(playerIndex.Value, modDirectory);
            return owner switch
            {
                PenumbraInterop.PenumbraModTempOverrideOwner.Self => true,
                PenumbraInterop.PenumbraModTempOverrideOwner.Glamourer => DoesMatchedGlamourerDesignLinkMod(modDirectory),
                _ => false,
            };
        }

        /// <summary>当前命中的规则里，是否有 Glamourer 设计行动关联（Mod Associations）了该 Penumbra Mod。</summary>
        private bool DoesMatchedGlamourerDesignLinkMod(string modDirectory)
        {
            var dir = modDirectory.Trim();
            var rules = ActiveRules;
            foreach (var idx in currentAppliedRuleIndices)
            {
                if (idx < 0 || idx >= rules.Count)
                    continue;

                foreach (var action in GetRuleActions(rules[idx]))
                {
                    if (action.Type != ActionType.Glamourer || string.IsNullOrWhiteSpace(action.GlamourerDesign))
                        continue;

                    var mods = _glamourerInterop.GetDesignAssociatedModDirectoriesByName(action.GlamourerDesign);
                    if (mods != null && mods.Contains(dir))
                        return true;
                }
            }

            return false;
        }

        private static string BuildMatchedModConflictKey(string? collection, string? modName) =>
            $"{(string.IsNullOrWhiteSpace(collection) ? "Default" : collection.Trim())}|{(modName ?? "").Trim()}";

        /// <summary>当前命中的可共存规则之间，是否在该 Mod 上存在 Penumbra 选项冲突。</summary>
        private bool DoMatchedRulesConflictOnMod(string? collection, string? modName) =>
            matchedModConflicts.Contains(BuildMatchedModConflictKey(collection, modName));

        private void RefreshMatchedModConflictsIfNeeded()
        {
            if (DateTime.UtcNow - matchedModConflictsComputedUtc < GlamourerConflictRefreshInterval)
                return;

            matchedModConflictsComputedUtc = DateTime.UtcNow;
            matchedModConflicts = ComputeMatchedModConflicts();
        }

        private HashSet<string> ComputeMatchedModConflicts()
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rules = ActiveRules;

            var actions = new List<HeelsRuleAction>();
            foreach (var idx in currentAppliedRuleIndices)
            {
                if (idx < 0 || idx >= rules.Count)
                    continue;

                actions.AddRange(GetRuleActions(rules[idx]).Where(a => a.Type == ActionType.Penumbra));
            }

            if (actions.Count == 0)
                return result;

            var conflicts = RulePenumbraActionAnalysis.Analyze(
                actions,
                (mod, option) => _penumbraInterop.GetOptionGroupType(mod, option));

            foreach (var index in conflicts.Keys)
            {
                var action = actions[index];
                result.Add(BuildMatchedModConflictKey(action.PenumbraCollection, action.PenumbraModName));
            }

            return result;
        }

        private bool ShouldOverwriteGlamourerForPenumbraAction(HeelsRuleAction action) =>
            Configuration.AutoOverwriteGlamourerPenumbra || action.PenumbraOverwriteGlamourer;

        private bool TryResolvePenumbraGlamourerConflict(HeelsRuleAction action, int playerObjectIndex)
        {
            var modDirectory = action.PenumbraModName ?? "";
            if (string.IsNullOrWhiteSpace(modDirectory))
                return true;

            if (!_penumbraInterop.IsModUnderGlamourerTakeover(playerObjectIndex, modDirectory))
                return true;

            if (!ShouldOverwriteGlamourerForPenumbraAction(action))
                return false;

            if (_penumbraInterop.TryRemoveGlamourerTemporaryModSettings(
                    modDirectory,
                    playerObjectIndex,
                    out var clearError))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(clearError))
                PluginLog.Warning($"Failed to clear Glamourer temporary settings before apply: {clearError}");

            return false;
        }

        private PenumbraApplyLayerConfig GetActivePenumbraApplyLayer() =>
            _activePenumbraApplyLayer ?? PenumbraApplyLayerConfig.Rule;

        private bool GetPenumbraApplyWithoutTempMerge() =>
            GetActivePenumbraApplyLayer().IsSfwLayer ? sfwApplyWithoutTempMerge : penumbraApplyWithoutTempMerge;

        private void MarkPenumbraTemporaryOverridesActive()
        {
            if (!Configuration.UsePenumbraTemporaryApply)
                return;

            if (GetActivePenumbraApplyLayer().IsSfwLayer)
                sfwTemporaryOverridesActive = true;
            else
                penumbraTemporaryOverridesActive = true;
        }

        private static string BuildPenumbraModKey(string? collection, string? modDirectory) =>
            $"{collection ?? "Default"}|{modDirectory ?? ""}";

        private HashSet<string> GetSfwCoveredModKeys()
        {
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var group in Configuration.SfwModeActionGroups ?? [])
            {
                if (string.IsNullOrWhiteSpace(group.PenumbraModName))
                    continue;

                keys.Add(BuildPenumbraModKey(group.PenumbraCollection, group.PenumbraModName));
            }

            return keys;
        }

        private void ClearPenumbraDedupKeysWithPrefix(string prefix)
        {
            foreach (var key in lastAppliedActionKeys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList())
            {
                lastAppliedActionKeys.Remove(key);
                penumbraDedupTrustedUntilUtcByKey.Remove(key);
                penumbraApplyAttemptUtcByKey.Remove(key);
                penumbraDriftStreakByKey.Remove(key);
            }
        }

        private void ClearSfwPenumbraApplyTracking()
        {
            ClearPenumbraDedupKeysWithPrefix(PenumbraApplyLayerConfig.Sfw.KeyPrefix);
        }

        private void ClearPenumbraDedupForSfwMods()
        {
            foreach (var modKey in GetSfwCoveredModKeys())
            {
                var parts = modKey.Split('|', 2);
                if (parts.Length != 2)
                    continue;

                ClearPenumbraDedupForMod(parts[0], parts[1], PenumbraApplyLayerConfig.Rule);
            }
        }

        private void ClearSfwPenumbraTemporaryOverrides(bool forceRemove = false)
        {
            if (!forceRemove && !sfwTemporaryOverridesActive)
                return;

            var localPlayer = ObjectTable.LocalPlayer;
            if (localPlayer != null
                && localPlayer.IsValid()
                && isPenumbraIpcReady)
            {
                _penumbraInterop.TryRemoveAllSfwTemporaryModSettingsPlayer(localPlayer.ObjectIndex, out _);
                sfwApplyWithoutTempMerge = true;
            }

            sfwTemporaryOverridesActive = false;
        }

        private void SetSfwModeActive(bool active)
        {
            if (Configuration.SfwModeActive == active)
                return;

            Configuration.SfwModeActive = active;
            SaveConfig();

            if (!active)
            {
                ClearSfwPenumbraTemporaryOverrides();
                ClearSfwPenumbraApplyTracking();
                ClearPenumbraDedupKeysWithPrefix(PenumbraApplyLayerConfig.Rule.KeyPrefix);
                penumbraApplyWithoutTempMerge = true;
            }
            else
            {
                ClearPenumbraDedupForSfwMods();
            }

            lastApplyUtc = DateTime.MinValue;
        }

        private void ApplySfwModeIfActive(ref bool appliedAnything, ref bool configDirty)
        {
            if (!Configuration.SfwModeActive)
                return;

            if (ApplySfwModePenumbraActions(ref appliedAnything))
                configDirty = true;
        }

        private bool PenumbraTrySetModEnabled(
            string collectionName,
            string modDirectory,
            bool enabled,
            int playerObjectIndex,
            out PenumbraIpcEc result,
            out string error)
        {
            if (Configuration.UsePenumbraTemporaryApply)
            {
                if (GetActivePenumbraApplyLayer().IsSfwLayer)
                    return _penumbraInterop.TrySetSfwTemporaryModEnabledPlayer(
                        playerObjectIndex,
                        collectionName,
                        modDirectory,
                        enabled,
                        out result,
                        out error);

                return _penumbraInterop.TrySetTemporaryModEnabledPlayer(
                    playerObjectIndex,
                    collectionName,
                    modDirectory,
                    enabled,
                    out result,
                    out error);
            }

            return _penumbraInterop.TrySetModEnabled(
                collectionName,
                modDirectory,
                enabled,
                out result,
                out error);
        }

        private bool PenumbraTryApplyModSetting(
            HeelsRuleAction action,
            int playerObjectIndex,
            out PenumbraIpcEc result,
            out string error)
        {
            var collection = action.PenumbraCollection ?? "Default";
            var modDirectory = action.PenumbraModName ?? "";
            var optionGroup = action.PenumbraOption ?? "";
            var groupType = _penumbraInterop.GetOptionGroupType(modDirectory, optionGroup);

            if (Configuration.UsePenumbraTemporaryApply)
            {
                if (GetActivePenumbraApplyLayer().IsSfwLayer)
                    return _penumbraInterop.TryApplySfwTemporaryModSettingPlayer(
                        playerObjectIndex,
                        collection,
                        modDirectory,
                        optionGroup,
                        action.PenumbraOptionName,
                        action.PenumbraOptionEnabled,
                        groupType,
                        out result,
                        out error);

                return _penumbraInterop.TryApplyTemporaryModSettingPlayer(
                    playerObjectIndex,
                    collection,
                    modDirectory,
                    optionGroup,
                    action.PenumbraOptionName,
                    action.PenumbraOptionEnabled,
                    groupType,
                    out result,
                    out error);
            }

            return _penumbraInterop.TryApplyModSetting(
                collection,
                modDirectory,
                optionGroup,
                action.PenumbraOptionName,
                action.PenumbraOptionEnabled,
                out result,
                out error);
        }

        private bool PenumbraTryApplyMultiToggleSettings(
            HeelsRuleAction action,
            IReadOnlyList<string> enabledOptionNames,
            int playerObjectIndex,
            out PenumbraIpcEc result,
            out string error)
        {
            var collection = action.PenumbraCollection ?? "Default";
            var modDirectory = action.PenumbraModName ?? "";
            var optionGroup = action.PenumbraOption ?? "";

            if (Configuration.UsePenumbraTemporaryApply)
            {
                if (GetActivePenumbraApplyLayer().IsSfwLayer)
                    return _penumbraInterop.TryApplySfwTemporaryMultiToggleSettingsPlayer(
                        playerObjectIndex,
                        collection,
                        modDirectory,
                        optionGroup,
                        enabledOptionNames,
                        out result,
                        out error);

                return _penumbraInterop.TryApplyTemporaryMultiToggleSettingsPlayer(
                    playerObjectIndex,
                    collection,
                    modDirectory,
                    optionGroup,
                    enabledOptionNames,
                    out result,
                    out error);
            }

            return _penumbraInterop.TryApplyMultiToggleSettings(
                collection,
                modDirectory,
                optionGroup,
                enabledOptionNames,
                out result,
                out error);
        }

        private void ApplySoundMixerAction(HeelsRuleAction action, ref bool appliedAnything)
        {
            if (!ActionUsesSoundMixer(action))
                return;

            var applyKey = BuildSoundMixerActionKey(action);
            if (lastAppliedActionKeys.Contains(applyKey))
                return;

            var priority = action.SoundMixerPriority;
            switch (action.SoundMixerActionKind)
            {
                case SoundMixerActionKind.TemporaryPreset:
                {
                    var preset = action.SoundMixerPresetName.Trim();
                    var comparison = _soundMixerInterop.IsTemporaryPresetActive(preset);
                    if (comparison == true)
                    {
                        lastAppliedActionKeys.Add(applyKey);
                        return;
                    }

                    if (_soundMixerInterop.TrySetTemporaryPreset(
                            preset,
                            priority,
                            out var result,
                            out var error))
                    {
                        lastAppliedActionKeys.Add(applyKey);
                        soundMixerOverridesActive = true;
                        appliedAnything = true;
                        lastError = "";
                        RecordAppliedActionSummary($"SoundMixer: Preset → {preset}");
                    }
                    else
                    {
                        lastError = error;
                        PluginLog.Warning($"SoundMixer temporary preset failed: {error} ({result})");
                    }

                    break;
                }
                case SoundMixerActionKind.TemporaryGroupVolume:
                {
                    var group = action.SoundMixerGroupName.Trim();
                    var volume = action.SoundMixerGroupVolume;
                    var comparison = _soundMixerInterop.IsTemporaryGroupVolumeActive(group, volume);
                    if (comparison == true)
                    {
                        lastAppliedActionKeys.Add(applyKey);
                        return;
                    }

                    if (_soundMixerInterop.TrySetTemporaryGroupVolume(
                            group,
                            volume,
                            priority,
                            out var result,
                            out var error))
                    {
                        lastAppliedActionKeys.Add(applyKey);
                        soundMixerOverridesActive = true;
                        appliedAnything = true;
                        lastError = "";
                        RecordAppliedActionSummary($"SoundMixer: {group} → {volume:F2}");
                    }
                    else
                    {
                        lastError = error;
                        PluginLog.Warning($"SoundMixer temporary group volume failed: {error} ({result})");
                    }

                    break;
                }
            }
        }

        /// <summary>Mod 启用/禁用：信任窗口内无条件跳过重 apply（WithTemp 的 enabled 位常不可靠）。</summary>
        private bool ShouldSkipPenumbraModStateApplyDueToDedup(
            string applyKey,
            Func<bool?> compareState,
            string description)
        {
            if (lastAppliedActionKeys.Contains(applyKey) && IsPenumbraDedupTrusted(applyKey))
                return true;

            return ShouldSkipPenumbraApplyDueToDedup(applyKey, compareState, description);
        }

        private bool ShouldSkipPenumbraApplyDueToDedup(string applyKey, Func<bool?> compareState, string description)
        {
            if (!lastAppliedActionKeys.Contains(applyKey))
                return false;

            if (IsPenumbraDedupTrusted(applyKey))
            {
                var trustedComparison = compareState();
                if (trustedComparison == true)
                    return true;

                if (trustedComparison == false)
                    penumbraDedupTrustedUntilUtcByKey.Remove(applyKey);
                else
                    // 信任窗口内读不到 WithTemp 时勿每 2s 重 apply；仅 false 才视为漂移
                    return true;
            }

            var comparison = compareState();
            if (comparison == false)
            {
                var streak = penumbraDriftStreakByKey.TryGetValue(applyKey, out var current) ? current + 1 : 1;
                penumbraDriftStreakByKey[applyKey] = streak;
                if (streak < 2)
                    return true;

                penumbraDriftStreakByKey.Remove(applyKey);
                penumbraDedupTrustedUntilUtcByKey.Remove(applyKey);
                lastAppliedActionKeys.Remove(applyKey);
                PluginLog.Debug($"Penumbra re-applying (confirmed drift): {description}");
                return false;
            }

            penumbraDriftStreakByKey.Remove(applyKey);

            // 已符合目标或暂不可读：延长信任窗口，避免信任过期后每 20s 无意义重 apply
            MarkPenumbraDedupTrusted(applyKey);
            return true;
        }

        private bool ShouldThrottlePenumbraApply(string applyKey, Func<bool?> compareState)
        {
            if (lastAppliedActionKeys.Contains(applyKey))
                return false;

            var comparison = compareState();
            if (comparison == false)
                return false;

            return IsPenumbraApplyThrottled(applyKey);
        }

        private void NotePenumbraApplyResult(string description, PenumbraIpcEc result)
        {
            if (result == PenumbraIpcEc.NothingChanged)
                PluginLog.Debug($"Penumbra unchanged: {description}");
            else
                PluginLog.Information($"Penumbra applied: {description} ({result})");
        }

        private bool? ComparePenumbraModEnabled(string collection, string modDirectory)
        {
            if (!_penumbraInterop.TryGetCurrentModConfiguration(
                    collection,
                    modDirectory,
                    out var enabled,
                    out _,
                    out _))
                return null;

            return enabled;
        }

        private bool TryCommitPenumbraModStateApply(
            string applyKey,
            string summary,
            Func<bool?> compareTargetState,
            PenumbraIpcEc result,
            ref bool appliedAnything)
        {
            MarkPenumbraApplyAttempt(applyKey);

            if (result == PenumbraIpcEc.Success)
            {
                lastAppliedActionKeys.Add(applyKey);
                MarkPenumbraDedupTrusted(applyKey);
                appliedAnything = true;
                NotePenumbraApplyResult(summary, result);
                RecordAppliedActionSummary(summary);
                return true;
            }

            if (result != PenumbraIpcEc.NothingChanged)
                return false;

            if (compareTargetState() != false)
            {
                lastAppliedActionKeys.Add(applyKey);
                MarkPenumbraDedupTrusted(applyKey);
                return true;
            }

            return false;
        }

        private bool? ComparePenumbraOptionActionState(HeelsRuleAction action, int? playerObjectIndex = null)
        {
            var groupType = _penumbraInterop.GetOptionGroupType(
                action.PenumbraModName ?? "",
                action.PenumbraOption ?? "");

            if (PenumbraInterop.UsesBoolOptionValue(groupType))
            {
                return _penumbraInterop.CompareGroupSetting(
                    action.PenumbraCollection,
                    action.PenumbraModName ?? "",
                    action.PenumbraOption ?? "",
                    groupType,
                    null,
                    GetPenumbraMultiToggleEnabledNames(action),
                    playerObjectIndex);
            }

            var targetNames = GetPenumbraSingleSelectTargetNames(action);
            if (targetNames.Count == 0)
            {
                return _penumbraInterop.CompareGroupSetting(
                    action.PenumbraCollection,
                    action.PenumbraModName ?? "",
                    action.PenumbraOption ?? "",
                    groupType,
                    null,
                    [],
                    playerObjectIndex);
            }

            return _penumbraInterop.CompareGroupSetting(
                action.PenumbraCollection,
                action.PenumbraModName ?? "",
                action.PenumbraOption ?? "",
                groupType,
                targetNames[0],
                null,
                playerObjectIndex);
        }

        private bool TryCommitPenumbraOptionApply(
            string applyKey,
            string summary,
            Func<bool?> compareState,
            PenumbraIpcEc result,
            ref bool appliedAnything)
        {
            MarkPenumbraApplyAttempt(applyKey);

            if (result == PenumbraIpcEc.Success)
            {
                var comparison = compareState();
                if (comparison == false)
                {
                    PluginLog.Warning($"Penumbra reported Success but state mismatch: {summary}");
                    return false;
                }

                lastAppliedActionKeys.Add(applyKey);
                MarkPenumbraDedupTrusted(applyKey);
                appliedAnything = true;
                NotePenumbraApplyResult(summary, result);
                RecordAppliedActionSummary(summary);
                return true;
            }

            if (result != PenumbraIpcEc.NothingChanged)
                return false;

            if (compareState() == true)
            {
                lastAppliedActionKeys.Add(applyKey);
                MarkPenumbraDedupTrusted(applyKey);
                NotePenumbraApplyResult(summary, result);
                return true;
            }

            return false;
        }

        private bool TryCommitPenumbraOptionApply(
            string applyKey,
            HeelsRuleAction action,
            PenumbraIpcEc result,
            ref bool appliedAnything,
            int? playerObjectIndex = null) =>
            TryCommitPenumbraOptionApply(
                applyKey,
                DescribePenumbraActionSummary(action),
                () => ComparePenumbraOptionActionState(action, playerObjectIndex),
                result,
                ref appliedAnything);

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
                RecordAppliedActionSummary($"Glamourer: {action.GlamourerDesign}");
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

            var playerIndex = GetLocalPlayerObjectIndex();
            if (playerIndex == null || !TryResolvePenumbraGlamourerConflict(action, playerIndex.Value))
                return false;

            var configDirty = false;

            // 如果是启用/禁用 Mod 操作
            if (action.PenumbraActionKind == PenumbraActionKind.EnableMod 
                || action.PenumbraActionKind == PenumbraActionKind.DisableMod)
            {
                var enabled = action.PenumbraActionKind == PenumbraActionKind.EnableMod;
                var keyPrefix = GetActivePenumbraApplyLayer().KeyPrefix;
                var applyKey = $"{keyPrefix}SetMod:{action.PenumbraCollection}|{action.PenumbraModName}|{enabled}";

                if (!enabled && IsLoginProtectionActive())
                    return false;

                var setModSummary = enabled
                    ? $"Penumbra: [{action.PenumbraCollection}] {action.PenumbraModName} → Enable"
                    : $"Penumbra: [{action.PenumbraCollection}] {action.PenumbraModName} → Disable";
                Func<bool?> compareModState = () =>
                {
                    var current = ComparePenumbraModEnabled(action.PenumbraCollection, action.PenumbraModName ?? "");
                    return current == null ? null : current == enabled;
                };
                if (ShouldSkipPenumbraModStateApplyDueToDedup(applyKey, compareModState, setModSummary))
                    return configDirty;
                if (ShouldThrottlePenumbraApply(applyKey, compareModState))
                    return configDirty;

                if (PenumbraTrySetModEnabled(
                        action.PenumbraCollection,
                        action.PenumbraModName,
                        enabled,
                        playerIndex.Value,
                        out var setModEc,
                        out var setModError))
                {
                    if (!TryCommitPenumbraModStateApply(applyKey, setModSummary, compareModState, setModEc, ref appliedAnything)
                        && setModEc != PenumbraIpcEc.Success
                        && setModEc != PenumbraIpcEc.NothingChanged)
                    {
                        lastError = setModError;
                        PluginLog.Warning($"[Action] Penumbra SetMod failed: [{action.PenumbraCollection}] {action.PenumbraModName} → Result: {setModEc}");
                    }
                    else if (setModEc is PenumbraIpcEc.Success or PenumbraIpcEc.NothingChanged)
                    {
                        MarkPenumbraTemporaryOverridesActive();
                    }
                }
                else
                {
                    lastError = setModError;
                    PluginLog.Warning($"[Action] Penumbra SetMod IPC failed: {setModError}");
                }
                
                return configDirty;
            }

            // 否则是设置 Mod 选项操作
            var groupType = _penumbraInterop.GetOptionGroupType(
                action.PenumbraModName ?? "",
                action.PenumbraOption ?? "");

            if (PenumbraInterop.UsesBoolOptionValue(groupType))
            {
                var enabledNames = ResolvePenumbraMultiToggleEnabledNames(
                    action.PenumbraCollection ?? "Default",
                    action.PenumbraModName ?? "",
                    action.PenumbraOption ?? "",
                    action.PenumbraMultiToggleStates,
                    GetActivePenumbraTempReadKey());
                var keyPrefix = GetActivePenumbraApplyLayer().KeyPrefix;
                var applyKey =
                    $"{keyPrefix}{action.PenumbraCollection}|{action.PenumbraModName}|{action.PenumbraOption}|M:{string.Join(",", enabledNames)}";
                
                if (!lastAppliedActionKeys.Contains(applyKey))
                {
                    if (PenumbraTryApplyMultiToggleSettings(
                            action,
                            enabledNames,
                            playerIndex.Value,
                            out var multiEc,
                            out var multiError)
                        && (multiEc == PenumbraIpcEc.Success || multiEc == PenumbraIpcEc.NothingChanged))
                    {
                        lastAppliedActionKeys.Add(applyKey);
                        appliedAnything = true;
                        MarkPenumbraTemporaryOverridesActive();
                    }
                    else if (!string.IsNullOrEmpty(multiError))
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
                    var penumbraEc = PenumbraTryApplyModSetting(
                            action,
                            playerIndex.Value,
                            out var singleEc,
                            out var singleError);
                    
                    if (penumbraEc && (singleEc == PenumbraIpcEc.Success || singleEc == PenumbraIpcEc.NothingChanged))
                    {
                        lastAppliedActionKeys.Add(applyKey);
                        appliedAnything = true;
                        MarkPenumbraTemporaryOverridesActive();
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

        private bool ApplyPenumbraActionsForRule(HeelsRule rule, ref bool appliedAnything) =>
            ApplyPenumbraActionsForRules(new List<HeelsRule> { rule }, ref appliedAnything);

        /// <summary>
        /// 合并多条命中规则的 Penumbra 行动后统一应用（按规则顺序，后覆盖前由 Core 去重处理）。
        /// </summary>
        private bool ApplyPenumbraActionsForRules(List<HeelsRule> rules, ref bool appliedAnything)
        {
            var penumbraActions = new List<HeelsRuleAction>();
            foreach (var rule in rules)
                penumbraActions.AddRange(FlattenRulePenumbraActions(rule).Where(ActionUsesPenumbra));

            if (Configuration.SfwModeActive)
            {
                var sfwMods = GetSfwCoveredModKeys();
                penumbraActions = penumbraActions
                    .Where(a => !sfwMods.Contains(BuildPenumbraModKey(a.PenumbraCollection, a.PenumbraModName)))
                    .ToList();
            }

            return ApplyPenumbraActionsCore(penumbraActions, PenumbraApplyLayerConfig.Rule, ref appliedAnything);
        }

        /// <summary>
        /// 按 Glamourer 优先级（GlamourerPriority 升序，相等保持原顺序）应用所有 Glamourer 行动。
        /// 数值越大越后写入，冲突装备槽位由高优先级胜出；非冲突槽位天然共存（Glamourer 为部分应用）。
        /// </summary>
        private void ApplyGlamourerActionsWithPriority(List<HeelsRuleAction> actions, ref bool appliedAnything)
        {
            var glamourerActions = actions.Where(ActionUsesGlamourer).ToList();
            if (glamourerActions.Count == 0)
                return;

            var ordered = glamourerActions
                .Select((action, index) => (action, index))
                .OrderBy(t => t.action.GlamourerPriority)
                .ThenBy(t => t.index)
                .Select(t => t.action);

            foreach (var action in ordered)
                ApplyGlamourerAction(action, ref appliedAnything);
        }

        private bool ApplySfwModePenumbraActions(ref bool appliedAnything)
        {
            if (!Configuration.SfwModeActive || !Configuration.UsePenumbraTemporaryApply)
                return false;

            var penumbraActions = GetAllSfwModePenumbraActions()
                .Where(ActionUsesPenumbra)
                .ToList();
            if (penumbraActions.Count == 0)
                return false;

            return ApplyPenumbraActionsCore(penumbraActions, PenumbraApplyLayerConfig.Sfw, ref appliedAnything);
        }

        private bool ApplyPenumbraActionsCore(
            List<HeelsRuleAction> penumbraActions,
            PenumbraApplyLayerConfig layer,
            ref bool appliedAnything)
        {
            var configDirty = false;
            _activePenumbraApplyLayer = layer;
            if (Configuration.UsePenumbraTemporaryApply)
                _penumbraInterop.TemporarySettingsReadKey = layer.IsSfwLayer
                    ? PenumbraInterop.SfwModePenumbraLockKey
                    : PenumbraInterop.HeelsDesignLinkerPenumbraLockKey;
            try
            {
            if (penumbraActions.Count == 0)
                return false;

            var playerIndex = GetLocalPlayerObjectIndex();
            if (playerIndex == null)
                return false;

            // 分离启用/禁用 Mod 操作和设置选项操作
            var modStateActions = penumbraActions
                .Where(a => a.PenumbraActionKind == PenumbraActionKind.EnableMod 
                         || a.PenumbraActionKind == PenumbraActionKind.DisableMod)
                .ToList();
            var modOptionActions = penumbraActions
                .Where(a => a.PenumbraActionKind == PenumbraActionKind.SetModOption)
                .ToList();
            var modsWithOptionBatch = new HashSet<string>(
                modOptionActions.Select(a => $"{a.PenumbraCollection ?? "Default"}|{a.PenumbraModName}"),
                StringComparer.OrdinalIgnoreCase);

            // 先处理启用/禁用 Mod 操作（直接应用，不合并）
            foreach (var action in modStateActions)
            {
                if (Configuration.UsePenumbraTemporaryApply
                    && action.PenumbraActionKind == PenumbraActionKind.EnableMod
                    && modsWithOptionBatch.Contains($"{action.PenumbraCollection ?? "Default"}|{action.PenumbraModName}"))
                    continue;

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
                    if (!mergedMultiToggles.TryGetValue(groupKey, out var merged))
                    {
                        merged = (
                            action,
                            new Dictionary<string, bool>(
                                action.PenumbraMultiToggleStates,
                                StringComparer.OrdinalIgnoreCase));
                        mergedMultiToggles[groupKey] = merged;
                    }
                    else
                    {
                        foreach (var (optionName, enabled) in action.PenumbraMultiToggleStates)
                            mergedMultiToggles[groupKey].Toggles[optionName] = enabled;
                    }
                }
                else
                {
                    if (singleSelectByGroup.TryGetValue(groupKey, out var existing))
                    {
                        var existingHasName = !string.IsNullOrWhiteSpace(existing.PenumbraOptionName);
                        var incomingHasName = !string.IsNullOrWhiteSpace(action.PenumbraOptionName);
                        if (!incomingHasName && existingHasName)
                            continue;
                    }

                    singleSelectByGroup[groupKey] = action;
                }
            }

            if (Configuration.UsePenumbraTemporaryApply)
            {
                ApplyPenumbraRuleModOptionsBatchedTemporary(
                    modOptionActions,
                    modStateActions,
                    mergedMultiToggles,
                    singleSelectByGroup,
                    playerIndex.Value,
                    ref appliedAnything);
            }
            else
            {
                ApplyPenumbraRuleModOptionsIndividual(
                    mergedMultiToggles,
                    singleSelectByGroup,
                    modOptionActions,
                    modStateActions,
                    playerIndex.Value,
                    ref appliedAnything);
            }

            return configDirty;
            }
            finally
            {
                _activePenumbraApplyLayer = null;
                if (layer.IsSfwLayer)
                    sfwApplyWithoutTempMerge = false;
                else
                    penumbraApplyWithoutTempMerge = false;
            }
        }

        private void ApplyPenumbraRuleModOptionsBatchedTemporary(
            List<HeelsRuleAction> modOptionActions,
            List<HeelsRuleAction> modStateActions,
            Dictionary<string, (HeelsRuleAction Action, Dictionary<string, bool> Toggles)> mergedMultiToggles,
            Dictionary<string, HeelsRuleAction> singleSelectByGroup,
            int playerIndex,
            ref bool appliedAnything)
        {
            foreach (var modGroup in modOptionActions.GroupBy(
                         a => $"{a.PenumbraCollection ?? "Default"}|{a.PenumbraModName}",
                         StringComparer.OrdinalIgnoreCase))
            {
                var sample = modGroup.First();
                var collection = sample.PenumbraCollection ?? "Default";
                var modDirectory = sample.PenumbraModName ?? "";
                var modKey = modGroup.Key;

                var hasDisable = modStateActions.Any(a =>
                    string.Equals($"{a.PenumbraCollection ?? "Default"}|{a.PenumbraModName}", modKey,
                        StringComparison.OrdinalIgnoreCase)
                    && a.PenumbraActionKind == PenumbraActionKind.DisableMod);
                if (hasDisable)
                    continue;

                if (!TryResolvePenumbraGlamourerConflict(sample, playerIndex))
                    continue;

                var optionOverrides = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
                var trackedApplies = new List<(string ApplyKey, string Summary, Func<bool?> Compare)>();

                foreach (var (groupKey, merged) in mergedMultiToggles)
                {
                    var action = merged.Action;
                    if (!string.Equals($"{action.PenumbraCollection ?? "Default"}|{action.PenumbraModName}", modKey,
                            StringComparison.OrdinalIgnoreCase))
                        continue;

                    var enabledNames = ResolvePenumbraMultiToggleEnabledNames(
                        action.PenumbraCollection ?? "Default",
                        modDirectory,
                        action.PenumbraOption ?? "",
                        merged.Toggles,
                        GetActivePenumbraTempReadKey());
                    var multiGroupType = _penumbraInterop.GetOptionGroupType(modDirectory, action.PenumbraOption ?? "");
                    var keyPrefix = GetActivePenumbraApplyLayer().KeyPrefix;
                    var applyKey =
                        $"{keyPrefix}{action.PenumbraCollection}|{action.PenumbraModName}|{action.PenumbraOption}|M:{string.Join(",", enabledNames)}";
                    var summary =
                        $"Penumbra: [{action.PenumbraCollection}] {action.PenumbraModName} → {action.PenumbraOption} = [{string.Join(", ", enabledNames)}]";
                    Func<bool?> compare = () => _penumbraInterop.CompareGroupSetting(
                        action.PenumbraCollection,
                        modDirectory,
                        action.PenumbraOption ?? "",
                        multiGroupType,
                        null,
                        enabledNames,
                        playerIndex);

                    optionOverrides[action.PenumbraOption ?? ""] = enabledNames;
                    trackedApplies.Add((applyKey, summary, compare));
                }

                foreach (var action in singleSelectByGroup.Values)
                {
                    if (!string.Equals($"{action.PenumbraCollection ?? "Default"}|{action.PenumbraModName}", modKey,
                            StringComparison.OrdinalIgnoreCase))
                        continue;

                    var optionGroup = action.PenumbraOption ?? "";
                    var optionNames = GetPenumbraSingleSelectTargetNames(action);

                    // 不传入空值：未指定具体选项（空名）的单选组视为“不改动该组”，跳过写入，
                    // 避免该组以空值覆盖 Collection 基线 / 同 Mod 其它命中规则已指定好的选项。
                    if (string.IsNullOrWhiteSpace(optionGroup) || optionNames.Count == 0)
                        continue;

                    var applyKey = BuildPenumbraActionKey(action);
                    var summary = DescribePenumbraActionSummary(action);
                    Func<bool?> compare = () => ComparePenumbraOptionActionState(action, playerIndex);

                    optionOverrides[optionGroup] = optionNames;
                    trackedApplies.Add((applyKey, summary, compare));
                }

                if (trackedApplies.Count == 0)
                    continue;

                var needsApply = trackedApplies.Any(tracked =>
                    !ShouldSkipPenumbraApplyDueToDedup(tracked.ApplyKey, tracked.Compare, tracked.Summary)
                    && !ShouldThrottlePenumbraApply(tracked.ApplyKey, tracked.Compare));

                if (!needsApply)
                    continue;

                if (trackedApplies.Any(t => t.Compare() == false))
                    ClearPenumbraDedupForMod(collection, modDirectory, GetActivePenumbraApplyLayer());

                var layer = GetActivePenumbraApplyLayer();
                var batchApplied = layer.IsSfwLayer
                    ? _penumbraInterop.TryApplySfwTemporaryModConfigurationPlayer(
                        playerIndex,
                        collection,
                        modDirectory,
                        enabled: true,
                        optionOverrides,
                        !GetPenumbraApplyWithoutTempMerge(),
                        out var batchResult,
                        out var batchError)
                    : _penumbraInterop.TryApplyTemporaryModConfigurationPlayer(
                        playerIndex,
                        collection,
                        modDirectory,
                        enabled: true,
                        optionOverrides,
                        !GetPenumbraApplyWithoutTempMerge(),
                        out batchResult,
                        out batchError);

                if (batchApplied)
                {
                    var committedAny = false;
                    foreach (var tracked in trackedApplies)
                    {
                        if (TryCommitPenumbraOptionApply(
                                tracked.ApplyKey,
                                tracked.Summary,
                                tracked.Compare,
                                batchResult,
                                ref appliedAnything))
                        {
                            committedAny = true;
                        }
                    }

                    if (committedAny || batchResult is PenumbraIpcEc.Success or PenumbraIpcEc.NothingChanged)
                    {
                        MarkPenumbraAutoEnableDedupSatisfied(collection, modDirectory, layer);
                        MarkPenumbraTemporaryOverridesActive();
                        lastError = "";
                    }
                    else if (batchResult != PenumbraIpcEc.Success && batchResult != PenumbraIpcEc.NothingChanged)
                    {
                        lastError = $"Penumbra error: {batchResult}";
                    }
                }
                else
                {
                    lastError = batchError;
                    PluginLog.Warning($"Penumbra IPC batch apply failed: {batchError}");
                }
            }
        }

        private void ApplyPenumbraRuleModOptionsIndividual(
            Dictionary<string, (HeelsRuleAction Action, Dictionary<string, bool> Toggles)> mergedMultiToggles,
            Dictionary<string, HeelsRuleAction> singleSelectByGroup,
            List<HeelsRuleAction> modOptionActions,
            List<HeelsRuleAction> modStateActions,
            int playerIndex,
            ref bool appliedAnything)
        {
            foreach (var modGroup in modOptionActions.GroupBy(
                         a => $"{a.PenumbraCollection ?? "Default"}|{a.PenumbraModName}",
                         StringComparer.OrdinalIgnoreCase))
            {
                var sample = modGroup.First();
                var modKey = modGroup.Key;
                var hasDisable = modStateActions.Any(a =>
                    string.Equals($"{a.PenumbraCollection ?? "Default"}|{a.PenumbraModName}", modKey,
                        StringComparison.OrdinalIgnoreCase)
                    && a.PenumbraActionKind == PenumbraActionKind.DisableMod);
                var hasEnable = modStateActions.Any(a =>
                    string.Equals($"{a.PenumbraCollection ?? "Default"}|{a.PenumbraModName}", modKey,
                        StringComparison.OrdinalIgnoreCase)
                    && a.PenumbraActionKind == PenumbraActionKind.EnableMod);
                if (hasDisable || hasEnable)
                    continue;

                var enableKey = BuildPenumbraAutoEnableApplyKey(sample.PenumbraCollection ?? "Default", sample.PenumbraModName ?? "");
                var enableSummary =
                    $"Penumbra: [{sample.PenumbraCollection}] {sample.PenumbraModName} → Enable (auto)";
                Func<bool?> compareModEnabled = () =>
                    ComparePenumbraModEnabled(sample.PenumbraCollection, sample.PenumbraModName ?? "");
                if (ShouldSkipPenumbraModStateApplyDueToDedup(enableKey, compareModEnabled, enableSummary))
                    continue;
                if (ShouldThrottlePenumbraApply(enableKey, compareModEnabled))
                    continue;
                if (!TryResolvePenumbraGlamourerConflict(sample, playerIndex))
                    continue;

                if (PenumbraTrySetModEnabled(
                        sample.PenumbraCollection,
                        sample.PenumbraModName,
                        true,
                        playerIndex,
                        out var enableEc,
                        out var enableError))
                {
                    if (TryCommitPenumbraModStateApply(enableKey, enableSummary, compareModEnabled, enableEc, ref appliedAnything)
                        && enableEc is PenumbraIpcEc.Success or PenumbraIpcEc.NothingChanged)
                    {
                        MarkPenumbraTemporaryOverridesActive();
                    }
                }
                else if (!string.IsNullOrEmpty(enableError))
                {
                    PluginLog.Warning($"[Action] Penumbra auto-enable mod failed: [{sample.PenumbraCollection}] {sample.PenumbraModName} → {enableError}");
                }
            }

            foreach (var (_, merged) in mergedMultiToggles)
            {
                var action = merged.Action;
                var enabledNames = ResolvePenumbraMultiToggleEnabledNames(
                    action.PenumbraCollection ?? "Default",
                    action.PenumbraModName ?? "",
                    action.PenumbraOption ?? "",
                    merged.Toggles,
                    GetActivePenumbraTempReadKey());
                var keyPrefix = GetActivePenumbraApplyLayer().KeyPrefix;
                var applyKey =
                    $"{keyPrefix}{action.PenumbraCollection}|{action.PenumbraModName}|{action.PenumbraOption}|M:{string.Join(",", enabledNames)}";
                var multiGroupType = _penumbraInterop.GetOptionGroupType(
                    action.PenumbraModName ?? "",
                    action.PenumbraOption ?? "");
                var multiSummary =
                    $"Penumbra: [{action.PenumbraCollection}] {action.PenumbraModName} → {action.PenumbraOption} = [{string.Join(", ", enabledNames)}]";
                Func<bool?> compareMultiState = () => _penumbraInterop.CompareGroupSetting(
                    action.PenumbraCollection,
                    action.PenumbraModName ?? "",
                    action.PenumbraOption ?? "",
                    multiGroupType,
                    null,
                    enabledNames,
                    playerIndex);
                if (ShouldSkipPenumbraApplyDueToDedup(applyKey, compareMultiState, multiSummary))
                    continue;
                if (ShouldThrottlePenumbraApply(applyKey, compareMultiState))
                    continue;
                if (!TryResolvePenumbraGlamourerConflict(action, playerIndex))
                    continue;

                if (PenumbraTryApplyMultiToggleSettings(
                        action,
                        enabledNames,
                        playerIndex,
                        out var multiResult,
                        out var multiError))
                {
                    if (TryCommitPenumbraOptionApply(applyKey, multiSummary, compareMultiState, multiResult, ref appliedAnything))
                    {
                        lastError = "";
                        MarkPenumbraTemporaryOverridesActive();
                    }
                    else if (multiResult != PenumbraIpcEc.Success && multiResult != PenumbraIpcEc.NothingChanged)
                        lastError = $"Penumbra error: {multiResult}";
                }
                else
                {
                    lastError = multiError;
                    PluginLog.Warning($"Penumbra IPC apply failed: {multiError}");
                }
            }

            foreach (var action in singleSelectByGroup.Values)
            {
                // 不传入空值：未指定具体选项（空名）的单选组视为“不改动该组”，跳过。
                if (string.IsNullOrWhiteSpace(action.PenumbraOption)
                    || GetPenumbraSingleSelectTargetNames(action).Count == 0)
                    continue;

                var applyKey = BuildPenumbraActionKey(action);
                var singleSummary = DescribePenumbraActionSummary(action);
                Func<bool?> compareSingleState = () => ComparePenumbraOptionActionState(action, playerIndex);
                if (ShouldSkipPenumbraApplyDueToDedup(applyKey, compareSingleState, singleSummary))
                    continue;
                if (ShouldThrottlePenumbraApply(applyKey, compareSingleState))
                    continue;
                if (!TryResolvePenumbraGlamourerConflict(action, playerIndex))
                    continue;

                if (PenumbraTryApplyModSetting(
                        action,
                        playerIndex,
                        out var singleResult,
                        out var singleError))
                {
                    if (TryCommitPenumbraOptionApply(applyKey, singleSummary, compareSingleState, singleResult, ref appliedAnything))
                    {
                        lastError = "";
                        MarkPenumbraTemporaryOverridesActive();
                    }
                    else if (singleResult != PenumbraIpcEc.Success && singleResult != PenumbraIpcEc.NothingChanged)
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
        }

        private float SelectHeelsHeightForMode(float defaultHeight, float actualHeight, bool hasTempOffset)
        {
            // 使用活动 RuleSet 的 SimpleHeels 配置
            if (Configuration.RuleSets.Count > 0 && 
                Configuration.ActiveRuleSetIndex >= 0 && 
                Configuration.ActiveRuleSetIndex < Configuration.RuleSets.Count)
            {
                var activeRuleSet = Configuration.RuleSets[Configuration.ActiveRuleSetIndex];
                if (!activeRuleSet.UseSimpleHeels)
                    return 0.0f; // 不使用 SimpleHeels 时返回 0

                // TempOffset 表示当前穿着的临时鞋跟高度；存在时优先用 actual，避免 Default 模式误匹配 barefoot
                if (hasTempOffset)
                    return actualHeight;
                    
                return activeRuleSet.SimpleHeelsMode == SimpleHeelsHeightMode.Actual
                    ? actualHeight
                    : defaultHeight;
            }
            
            return hasTempOffset ? actualHeight : defaultHeight;
        }

        private bool HasRuleMatchingHeightChanged(float height)
        {
            if (float.IsNaN(lastRuleMatchingHeight))
                return true;

            var tolerance = MathF.Pow(10f, -Math.Clamp(Configuration.DecimalPrecision, 0, 5));
            return MathF.Abs(height - lastRuleMatchingHeight) > tolerance;
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
                            var penumbraGroup = action.PenumbraGroup;
                            if (penumbraGroup != null && !string.IsNullOrWhiteSpace(penumbraGroup.PenumbraModName))
                            {
                                param = new BaselineParameterId
                                {
                                    Type = ActionType.Penumbra,
                                    PenumbraCollection = penumbraGroup.PenumbraCollection,
                                    PenumbraModName = penumbraGroup.PenumbraModName
                                };
                            }
                            else if (!string.IsNullOrWhiteSpace(action.PenumbraModName))
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
        /// 插件启用时校验行动列表：将错放在非对应类型行动上的字段拆分为独立行动（插入原行动之后）。
        /// </summary>
        private int SanitizeMisplacedActionFields()
        {
            var extractedCount = 0;

            foreach (var ruleSet in Configuration.RuleSets)
            {
                foreach (var rule in ruleSet.Rules)
                    extractedCount += SanitizeMisplacedActionList(rule.Actions);
            }

            extractedCount += SanitizeMisplacedActionList(Configuration.SfwModeActions);

            return extractedCount;
        }

        private static int SanitizeMisplacedActionList(List<HeelsRuleAction> actions)
        {
            var extractedCount = 0;

            for (var i = 0; i < actions.Count; i++)
            {
                var extracted = ExtractMisplacedFieldsFromAction(actions[i]);
                if (extracted.Count == 0)
                    continue;

                extractedCount += extracted.Count;
                actions.InsertRange(i + 1, extracted);

                if (!ActionHasEffectivePayload(actions[i]))
                    actions.RemoveAt(i);

                i += extracted.Count;
            }

            return extractedCount;
        }

        private static List<HeelsRuleAction> ExtractMisplacedFieldsFromAction(HeelsRuleAction action)
        {
            var extracted = new List<HeelsRuleAction>();

            if (action.Type != ActionType.Glamourer && !string.IsNullOrWhiteSpace(action.GlamourerDesign))
            {
                extracted.Add(new HeelsRuleAction
                {
                    Type = ActionType.Glamourer,
                    GlamourerDesign = action.GlamourerDesign.Trim(),
                    GlamourerPriority = action.GlamourerPriority,
                    IsActionCollapsed = action.IsActionCollapsed,
                });
                action.GlamourerDesign = "";
                action.GlamourerPriority = 0;
            }

            if (action.Type != ActionType.Honorific && !string.IsNullOrWhiteSpace(action.HonorificTitleJson))
            {
                extracted.Add(new HeelsRuleAction
                {
                    Type = ActionType.Honorific,
                    HonorificTitleJson = action.HonorificTitleJson,
                    IsActionCollapsed = action.IsActionCollapsed,
                });
                action.HonorificTitleJson = "";
            }

            if (action.Type != ActionType.Moodles && !string.IsNullOrWhiteSpace(action.MoodleGuid))
            {
                extracted.Add(new HeelsRuleAction
                {
                    Type = ActionType.Moodles,
                    MoodleGuid = action.MoodleGuid,
                    MoodleIsPreset = action.MoodleIsPreset,
                    IsActionCollapsed = action.IsActionCollapsed,
                });
                action.MoodleGuid = "";
                action.MoodleIsPreset = false;
            }

            if (action.Type != ActionType.SoundMixer && HasMisplacedSoundMixerFields(action))
            {
                extracted.Add(new HeelsRuleAction
                {
                    Type = ActionType.SoundMixer,
                    SoundMixerActionKind = action.SoundMixerActionKind,
                    SoundMixerPresetName = action.SoundMixerPresetName ?? "",
                    SoundMixerGroupName = action.SoundMixerGroupName ?? "",
                    SoundMixerGroupVolume = action.SoundMixerGroupVolume,
                    SoundMixerPriority = action.SoundMixerPriority,
                    IsActionCollapsed = action.IsActionCollapsed,
                });
                ClearSoundMixerFields(action);
            }

            if (action.Type != ActionType.Penumbra && HasMisplacedPenumbraFields(action))
            {
                extracted.Add(CreatePenumbraActionFromMisplaced(action));
                ClearPenumbraFields(action);
            }

            return extracted;
        }

        private static bool ActionHasEffectivePayload(HeelsRuleAction action) =>
            action.Type switch
            {
                ActionType.Glamourer => !string.IsNullOrWhiteSpace(action.GlamourerDesign),
                ActionType.Penumbra => ActionHasConfiguredPenumbraPayload(action),
                ActionType.Honorific => !string.IsNullOrWhiteSpace(action.HonorificTitleJson),
                ActionType.Moodles => !string.IsNullOrWhiteSpace(action.MoodleGuid),
                ActionType.SoundMixer => !string.IsNullOrWhiteSpace(action.SoundMixerPresetName)
                    || !string.IsNullOrWhiteSpace(action.SoundMixerGroupName),
                _ => false,
            };

        private static bool ActionHasConfiguredPenumbraPayload(HeelsRuleAction action)
        {
            if (action.PenumbraGroup != null)
            {
                var group = action.PenumbraGroup;
                if (!string.IsNullOrWhiteSpace(group.PenumbraModName))
                    return true;

                return group.SubActions.Any(sub => !string.IsNullOrWhiteSpace(sub.PenumbraOption));
            }

            return HasLegacyFlatPenumbraFields(action);
        }

        private static bool HasMisplacedPenumbraFields(HeelsRuleAction action) =>
            action.Type != ActionType.Penumbra && ActionHasConfiguredPenumbraPayload(action);

        private static bool HasMisplacedSoundMixerFields(HeelsRuleAction action) =>
            action.Type != ActionType.SoundMixer
            && (!string.IsNullOrWhiteSpace(action.SoundMixerPresetName)
                || !string.IsNullOrWhiteSpace(action.SoundMixerGroupName));

        private static void ClearSoundMixerFields(HeelsRuleAction action)
        {
            action.SoundMixerActionKind = SoundMixerActionKind.TemporaryPreset;
            action.SoundMixerPresetName = "";
            action.SoundMixerGroupName = "";
            action.SoundMixerGroupVolume = 1.0f;
            action.SoundMixerPriority = SoundMixerInterop.DefaultPriority;
        }

        private static void ClearPenumbraFields(HeelsRuleAction action)
        {
            action.PenumbraGroup = null;
            action.PenumbraActionKind = PenumbraActionKind.SetModOption;
            action.PenumbraCollection = "Default";
            action.PenumbraModName = "";
            action.PenumbraOption = "";
            action.PenumbraOptionName = "";
            action.PenumbraOptionEnabled = true;
            action.PenumbraOverwriteGlamourer = false;
            action.PenumbraMultiToggleStates.Clear();
        }

        private static HeelsRuleAction CreatePenumbraActionFromMisplaced(HeelsRuleAction source)
        {
            if (source.PenumbraGroup != null)
            {
                return new HeelsRuleAction
                {
                    Type = ActionType.Penumbra,
                    PenumbraGroup = ClonePenumbraActionGroup(source.PenumbraGroup),
                    IsActionCollapsed = source.IsActionCollapsed,
                };
            }

            return new HeelsRuleAction
            {
                Type = ActionType.Penumbra,
                PenumbraActionKind = source.PenumbraActionKind,
                PenumbraCollection = string.IsNullOrWhiteSpace(source.PenumbraCollection)
                    ? "Default"
                    : source.PenumbraCollection,
                PenumbraModName = source.PenumbraModName ?? "",
                PenumbraOption = source.PenumbraOption ?? "",
                PenumbraOptionName = source.PenumbraOptionName ?? "",
                PenumbraOptionEnabled = source.PenumbraOptionEnabled,
                PenumbraOverwriteGlamourer = source.PenumbraOverwriteGlamourer,
                PenumbraMultiToggleStates = new Dictionary<string, bool>(
                    source.PenumbraMultiToggleStates,
                    StringComparer.OrdinalIgnoreCase),
                IsActionCollapsed = source.IsActionCollapsed,
            };
        }

        private static PenumbraActionGroup ClonePenumbraActionGroup(PenumbraActionGroup source) =>
            new()
            {
                PenumbraCollection = string.IsNullOrWhiteSpace(source.PenumbraCollection)
                    ? "Default"
                    : source.PenumbraCollection,
                PenumbraModName = source.PenumbraModName ?? "",
                IsCollapsed = source.IsCollapsed,
                SubActions = source.SubActions
                    .Select(sub => new PenumbraSubAction
                    {
                        PenumbraActionKind = sub.PenumbraActionKind,
                        PenumbraOption = sub.PenumbraOption ?? "",
                        PenumbraOptionName = sub.PenumbraOptionName ?? "",
                        PenumbraOptionEnabled = sub.PenumbraOptionEnabled,
                        PenumbraOverwriteGlamourer = sub.PenumbraOverwriteGlamourer,
                        PenumbraMultiToggleStates = new Dictionary<string, bool>(
                            sub.PenumbraMultiToggleStates,
                            StringComparer.OrdinalIgnoreCase),
                        IsCollapsed = sub.IsCollapsed,
                    })
                    .ToList(),
            };

        /// <summary>当前匹配规则是否已使用该基准参数（非 Penumbra；整条跳过）。</summary>
        private bool MatchedRuleUsesBaselineParameter(HeelsRule rule, BaselineParameterId param)
        {
            foreach (var action in EnumerateEffectiveRuleActions(rule))
            {
                if (BaselineActionTargetsParameter(action, param))
                    return true;
            }

            return false;
        }

        private void RestoreShutdownApplySnapshot()
        {
            if (Configuration.LastShutdownActiveRuleSetIndex < 0)
                return;

            var signature = Configuration.LastShutdownMatchedRuleSignature ?? "";
            lastMatchedSetSignature = signature;
            stableTrackingSignature = signature;
            lastAppliedHonorificJson = Configuration.LastShutdownLastAppliedHonorificJson ?? "";
            lastAppliedActionKeys.Clear();
            foreach (var key in Configuration.LastShutdownAppliedActionKeys ?? [])
            {
                if (!string.IsNullOrEmpty(key))
                    lastAppliedActionKeys.Add(key);
            }

            var restoredMoodleKey = Configuration.LastShutdownAppliedMoodleKey ?? "";
            if (!string.IsNullOrEmpty(restoredMoodleKey))
            {
                var legacyApplyKey = restoredMoodleKey.StartsWith("M:", StringComparison.Ordinal)
                    ? restoredMoodleKey
                    : $"M:{restoredMoodleKey}";
                lastAppliedActionKeys.Add(legacyApplyKey);
                lastAppliedMoodleKey = legacyApplyKey.StartsWith("M:", StringComparison.Ordinal)
                    ? legacyApplyKey[2..]
                    : restoredMoodleKey;
            }
            else
            {
                lastAppliedMoodleKey = "";
            }

            skipReapplyForRestoredShutdownMatch = true;
        }

        private void PersistShutdownApplySnapshot()
        {
            Configuration.LastShutdownMatchedRuleSignature = lastMatchedSetSignature ?? "";
            Configuration.LastShutdownActiveRuleSetIndex = Configuration.ActiveRuleSetIndex;
            Configuration.LastShutdownAppliedMoodleKey = lastAppliedMoodleKey ?? "";
            Configuration.LastShutdownLastAppliedHonorificJson = lastAppliedHonorificJson ?? "";
            Configuration.LastShutdownAppliedActionKeys = lastAppliedActionKeys.ToList();
            try
            {
                PluginInterface.SavePluginConfig(Configuration);
            }
            catch (Exception ex)
            {
                PluginLog.Warning($"Failed to persist shutdown apply snapshot: {ex.Message}");
            }
        }

        private bool MatchesRestoredShutdownMatch(string matchedSignature)
        {
            if (Configuration.LastShutdownActiveRuleSetIndex != Configuration.ActiveRuleSetIndex)
                return false;

            return string.Equals(
                matchedSignature ?? "",
                Configuration.LastShutdownMatchedRuleSignature ?? "",
                StringComparison.Ordinal);
        }

        private bool TryConsumeRestoredShutdownMatchSkip(string matchedSignature)
        {
            if (!skipReapplyForRestoredShutdownMatch)
                return false;

            if (!MatchesRestoredShutdownMatch(matchedSignature))
            {
                skipReapplyForRestoredShutdownMatch = false;
                return false;
            }

            skipReapplyForRestoredShutdownMatch = false;
            lastApplyUtc = DateTime.UtcNow;
            applyGateStatus = Localization.SessionRestoredNoReapply;
            return true;
        }

        private static bool PenumbraActionTargetsMod(HeelsRuleAction action, BaselineParameterId param) =>
            action.Type == ActionType.Penumbra
            && !string.IsNullOrWhiteSpace(action.PenumbraModName)
            && string.Equals(action.PenumbraModName, param.PenumbraModName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(action.PenumbraCollection ?? "Default", param.PenumbraCollection ?? "Default",
                StringComparison.OrdinalIgnoreCase);

        private bool MatchedRuleHasPenumbraActionsOnMod(HeelsRule rule, BaselineParameterId param) =>
            EnumerateEffectiveRuleActions(rule).Any(action => PenumbraActionTargetsMod(action, param));

        private bool MatchedRuleHasPenumbraOptionActionsOnMod(HeelsRule rule, BaselineParameterId param) =>
            EnumerateEffectiveRuleActions(rule).Any(action =>
                PenumbraActionTargetsMod(action, param)
                && action.PenumbraActionKind == PenumbraActionKind.SetModOption);

        private bool MatchedRuleSetsPenumbraModEnabled(HeelsRule rule, BaselineParameterId param, bool enabled) =>
            EnumerateEffectiveRuleActions(rule).Any(action =>
                PenumbraActionTargetsMod(action, param)
                && ((action.PenumbraActionKind == PenumbraActionKind.EnableMod && enabled)
                    || (action.PenumbraActionKind == PenumbraActionKind.DisableMod && !enabled)));

        /// <summary>按 action 目标状态判断：规则已显式设置同 Mod 启用/禁用时，或规则仅设选项时不应关 Mod。</summary>
        private bool ShouldApplyBaselinePenumbraModState(HeelsRule rule, BaselineParameterId param, bool baselineEnabled)
        {
            if (MatchedRuleSetsPenumbraModEnabled(rule, param, baselineEnabled))
                return false;

            if (!baselineEnabled && MatchedRuleHasPenumbraOptionActionsOnMod(rule, param))
                return false;

            if (baselineEnabled
                && MatchedRuleHasPenumbraOptionActionsOnMod(rule, param)
                && !MatchedRuleSetsPenumbraModEnabled(rule, param, false))
                return false;

            return true;
        }

        private Dictionary<string, bool> GetMatchedRuleMultiToggleStatesForGroup(
            HeelsRule rule,
            BaselineParameterId param,
            string optionGroup)
        {
            var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(optionGroup))
                return result;

            foreach (var action in EnumerateEffectiveRuleActions(rule))
            {
                if (action.PenumbraActionKind != PenumbraActionKind.SetModOption)
                    continue;
                if (!PenumbraActionTargetsMod(action, param))
                    continue;
                if (!string.Equals(action.PenumbraOption, optionGroup, StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (var (subName, enabled) in action.PenumbraMultiToggleStates)
                {
                    var trimmed = subName.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed))
                        continue;
                    result[trimmed] = enabled;
                }
            }

            return result;
        }

        private bool MatchedRuleCoversBaselineSingleSelectOption(
            HeelsRule rule,
            BaselineParameterId param,
            BaselinePenumbraOptionSetting optionSetting)
        {
            if (string.IsNullOrWhiteSpace(optionSetting.PenumbraOptionName))
                return false;

            foreach (var action in EnumerateEffectiveRuleActions(rule))
            {
                if (action.PenumbraActionKind != PenumbraActionKind.SetModOption)
                    continue;
                if (!PenumbraActionTargetsMod(action, param))
                    continue;
                if (!string.Equals(action.PenumbraOption, optionSetting.PenumbraOption, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (string.Equals(action.PenumbraOptionName, optionSetting.PenumbraOptionName, StringComparison.OrdinalIgnoreCase)
                    && action.PenumbraOptionEnabled == optionSetting.PenumbraOptionEnabled)
                    return true;
            }

            return false;
        }

        private List<string> BuildBaselineMultiToggleEnabledNames(
            HeelsRule matchedRule,
            BaselineParameterId param,
            BaselinePenumbraOptionSetting optionSetting)
        {
            var ruleStates = GetMatchedRuleMultiToggleStatesForGroup(
                matchedRule, param, optionSetting.PenumbraOption);
            var enabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (name, on) in optionSetting.PenumbraMultiToggleStates)
            {
                var trimmed = name.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || ruleStates.ContainsKey(trimmed))
                    continue;
                if (on)
                    enabled.Add(trimmed);
            }

            foreach (var (name, on) in ruleStates)
            {
                if (on)
                    enabled.Add(name);
            }

            return enabled.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private bool BaselineHasMultiToggleSubOptionsOutsideRule(
            HeelsRule matchedRule,
            BaselineParameterId param,
            BaselinePenumbraOptionSetting optionSetting)
        {
            var ruleStates = GetMatchedRuleMultiToggleStatesForGroup(
                matchedRule, param, optionSetting.PenumbraOption);
            return optionSetting.PenumbraMultiToggleStates.Keys.Any(subName =>
            {
                var trimmed = subName.Trim();
                return !string.IsNullOrWhiteSpace(trimmed) && !ruleStates.ContainsKey(trimmed);
            });
        }

        private static bool BaselineActionTargetsParameter(HeelsRuleAction action, BaselineParameterId param)
        {
            switch (param.Type)
            {
                case ActionType.Penumbra:
                    return action.Type == ActionType.Penumbra
                           && !string.IsNullOrWhiteSpace(action.PenumbraModName)
                           && string.Equals(action.PenumbraModName, param.PenumbraModName, StringComparison.OrdinalIgnoreCase)
                           && string.Equals(action.PenumbraCollection ?? "Default", param.PenumbraCollection ?? "Default",
                               StringComparison.OrdinalIgnoreCase);
                case ActionType.Glamourer:
                    return action.Type == ActionType.Glamourer
                           && !string.IsNullOrWhiteSpace(action.GlamourerDesign)
                           && string.Equals(action.GlamourerDesign, param.GlamourerDesign, StringComparison.OrdinalIgnoreCase);
                case ActionType.Moodles:
                    return action.Type == ActionType.Moodles
                           && !string.IsNullOrWhiteSpace(action.MoodleGuid)
                           && string.Equals(action.MoodleGuid, param.MoodleGuid, StringComparison.OrdinalIgnoreCase);
                case ActionType.Honorific:
                    return action.Type == ActionType.Honorific
                           && !string.IsNullOrWhiteSpace(action.HonorificTitleJson)
                           && string.Equals(action.HonorificTitleJson, param.HonorificTitleJson, StringComparison.OrdinalIgnoreCase);
                default:
                    return false;
            }
        }

        /// <summary>
        /// 应用基准行动（在匹配规则的 Actions 之前调用；与规则共用参数时跳过以免妨碍规则 apply）。
        /// </summary>
        private void ApplyBaselineActions(RuleSet ruleSet, HeelsRule matchedRule, ref bool appliedAnything)
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

                if (param.Type != ActionType.Penumbra
                    && MatchedRuleUsesBaselineParameter(matchedRule, config.ParameterId))
                    continue;
                
                switch (param.Type)
                {
                    case ActionType.Penumbra:
                        ApplyBaselinePenumbra(ruleSet, config, matchedRule, ref appliedAnything);
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

        private void ApplyBaselinePenumbra(
            RuleSet ruleSet,
            BaselineActionConfig config,
            HeelsRule matchedRule,
            ref bool appliedAnything)
        {
            if (Configuration.UsePenumbraTemporaryApply)
                return;

            var param = config.ParameterId;
            if (!isPenumbraIpcReady || string.IsNullOrWhiteSpace(param.PenumbraModName))
                return;

            if (config.Mode == BaselineMode.Auto)
            {
                if (!MatchedRuleHasPenumbraActionsOnMod(matchedRule, param))
                    ApplyBaselinePenumbraModState(param, false, ref appliedAnything);
                return;
            }

            if (ShouldApplyBaselinePenumbraModState(matchedRule, param, config.ManualModEnabled))
                ApplyBaselinePenumbraModState(param, config.ManualModEnabled, ref appliedAnything);

            if (config.ManualPenumbraOptions.Count == 0)
                SyncBaselinePenumbraManualOptionsFromRules(ruleSet, config);

            foreach (var optionSetting in config.ManualPenumbraOptions)
                ApplyBaselinePenumbraOptionSetting(param, optionSetting, matchedRule, ref appliedAnything);
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
            HeelsRule matchedRule,
            ref bool appliedAnything)
        {
            if (string.IsNullOrWhiteSpace(optionSetting.PenumbraOption))
                return;

            var modName = param.PenumbraModName ?? "";
            var collection = param.PenumbraCollection ?? "Default";
            var groupType = _penumbraInterop.GetOptionGroupType(modName, optionSetting.PenumbraOption);

            if (PenumbraInterop.UsesBoolOptionValue(groupType))
            {
                if (!BaselineHasMultiToggleSubOptionsOutsideRule(matchedRule, param, optionSetting))
                    return;

                var enabledNames = BuildBaselineMultiToggleEnabledNames(matchedRule, param, optionSetting);
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
                if (!optionSetting.PenumbraOptionEnabled
                    || string.IsNullOrWhiteSpace(optionSetting.PenumbraOptionName))
                    return;

                if (MatchedRuleCoversBaselineSingleSelectOption(matchedRule, param, optionSetting))
                    return;

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
                foreach (var action in FlattenRulePenumbraActions(rule))
                {
                    if (action.PenumbraActionKind != PenumbraActionKind.SetModOption
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
                baselineMoodleAppliedThisCycle = true;
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
            ClearPenumbraApplyTracking();
            lastAppliedHonorificJson = "";
            lastAppliedMoodleKey = "";
            lastMatchedRuleIndex = -1;
            stableTrackingRuleIndex = -1;
            lastMatchedSetSignature = "";
            stableTrackingSignature = null;
            ruleMatchStableSinceUtc = null;
            lastRuleMatchingHeight = float.NaN;
        }

        private void InvalidateApplyStateForBaselineChange()
        {
            ClearPenumbraApplyTracking();
            lastApplyUtc = DateTime.MinValue;
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
                        lastError = Localization.DefaultOffsetNotFound;
                        return false;
                    }
                }

                hasTempOffset = false;
                if (json["TempOffset"] is JObject tempOffset
                    && tempOffset["Y"] is JToken tempY)
                {
                    actualHeight = tempY.Value<float>();
                    hasTempOffset = true;
                    lastTempOffsetSeenUtc = DateTime.UtcNow;
                    lastKnownActualHeelsHeight = actualHeight;
                }
                else if (lastTempOffsetSeenUtc.HasValue
                         && (DateTime.UtcNow - lastTempOffsetSeenUtc.Value).TotalSeconds
                             < TempOffsetAbsenceGraceSeconds)
                {
                    // SimpleHeels 上报 IPC 时 TempOffset 可能短暂缺失，勿立刻回落 barefoot 规则
                    hasTempOffset = true;
                    actualHeight = lastKnownActualHeelsHeight;
                }
                else
                {
                    actualHeight = defaultHeight;
                    lastTempOffsetSeenUtc = null;
                }

                return true;
            }
            catch (Exception ex)
            {
                lastError = Localization.ParseError(ex.Message);
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
                _groupDragSourceIndex = null;
                _groupDragTargetIndex = null;
                _actionDragSourceRuleIndex = null;
                _actionDragSourceIndex = null;
                _actionDragTargetIndex = null;
                restoreDefaultsPending = false;
                wasSettingsTabActive = false;
                return;
            }

            ImGui.SetNextWindowSize(GetSavedWindowSize(), ImGuiCond.Appearing);
            // ### 固定 ImGui 窗口 ID，避免动态标题每帧变化时被识别为新窗口
            if (ImGui.Begin($"{Localization.WindowTitle}###{ConfigurationWindowImGuiId}", ref drawConfigUi))
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

                if (ConfigurationUsesMoodles() && !isMoodlesIpcReady)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.3f, 0.3f, 1.0f));
                    var moodlesDetail = !MoodlesInterop.IsMoodlesLoaded(PluginInterface)
                        ? Localization.NotAvailable
                        : Localization.LoadedIpcNotReady;
                    ImGui.TextWrapped($"{Localization.MoodlesStatus}: {moodlesDetail}");
                    ImGui.PopStyleColor();
                    ImGui.Separator();
                }

                if (ConfigurationUsesHonorific() && !isHonorificIpcReady)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.3f, 0.3f, 1.0f));
                    var honorificDetail = !HonorificInterop.IsHonorificLoaded(PluginInterface)
                        ? Localization.NotAvailable
                        : Localization.LoadedIpcNotReady;
                    ImGui.TextWrapped($"{Localization.HonorificStatus}: {honorificDetail}");
                    ImGui.PopStyleColor();
                    ImGui.Separator();
                }
                
                if (!isSimpleHeelsAvailable || !isSimpleHeelsIpcReady)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.3f, 0.3f, 1.0f));
                    var shDetail = !isSimpleHeelsAvailable
                        ? Localization.NotAvailable
                        : Localization.LoadedIpcNotReady;
                    ImGui.TextWrapped($"{Localization.SimpleHeelsStatus}: {shDetail}");
                    ImGui.PopStyleColor();
                    ImGui.Separator();
                }

                DrawPersistentStatusBar();

                var settingsTabActive = false;
                if (ImGui.BeginTabBar("MainTabs"))
                {
                    if (ImGui.BeginTabItem(Localization.TabRules))
                    {
                        DrawRulesTab();
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem(Localization.TabSfwMode))
                    {
                        DrawSfwModeTab();
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
            
            if (Configuration.RuleSets.Count > 0 &&
                Configuration.ActiveRuleSetIndex >= 0 &&
                Configuration.ActiveRuleSetIndex < Configuration.RuleSets.Count)
            {
                var activeRuleSet = Configuration.RuleSets[Configuration.ActiveRuleSetIndex];
                DrawSimpleHeelsConfig(activeRuleSet);
                DrawBaselineActionsConfig(activeRuleSet);
            }
        }

        private static string BuildSimpleHeelsSectionSummary(RuleSet ruleSet)
        {
            if (!ruleSet.UseSimpleHeels)
                return Localization.SimpleHeelsSectionDisabled;

            return ruleSet.SimpleHeelsMode == SimpleHeelsHeightMode.Actual
                ? Localization.SimpleHeelsHeightActual
                : Localization.SimpleHeelsHeightDefault;
        }

        private void DrawSimpleHeelsConfig(RuleSet activeRuleSet)
        {
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            var summary = BuildSimpleHeelsSectionSummary(activeRuleSet);
            ImGui.SetNextItemOpen(activeRuleSet.IsSimpleHeelsSectionExpanded, ImGuiCond.Always);
            var headerOpen = ImGui.CollapsingHeader($"{Localization.SimpleHeelsSection} — {summary}");
            if (ImGui.IsItemToggledOpen())
            {
                activeRuleSet.IsSimpleHeelsSectionExpanded = !activeRuleSet.IsSimpleHeelsSectionExpanded;
                SaveConfig();
            }

            if (!headerOpen)
                return;

            ImGui.Indent();

            var useSimpleHeels = activeRuleSet.UseSimpleHeels;
            if (ImGui.Checkbox(Localization.UseSimpleHeels, ref useSimpleHeels))
            {
                activeRuleSet.UseSimpleHeels = useSimpleHeels;
                InvalidateApplyStateForHeightSourceChange();
                SaveConfig();
            }

            if (activeRuleSet.UseSimpleHeels && (!isSimpleHeelsAvailable || !isSimpleHeelsIpcReady))
            {
                ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.2f, 0.2f, 1.0f));
                ImGui.TextWrapped(Localization.SimpleHeelsConnectionFailedWarning);
                ImGui.PopStyleColor();
            }

            if (activeRuleSet.UseSimpleHeels)
            {
                var mode = (int)activeRuleSet.SimpleHeelsMode;
                if (ImGui.RadioButton(Localization.SimpleHeelsHeightDefault, mode == (int)SimpleHeelsHeightMode.Default))
                {
                    if (activeRuleSet.SimpleHeelsMode != SimpleHeelsHeightMode.Default)
                    {
                        activeRuleSet.SimpleHeelsMode = SimpleHeelsHeightMode.Default;
                        currentHeelsHeight = SelectHeelsHeightForMode(
                            currentHeelsDefaultHeight,
                            currentHeelsActualHeight,
                            currentHeelsHasTempOffset);
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
                        currentHeelsHeight = SelectHeelsHeightForMode(
                            currentHeelsDefaultHeight,
                            currentHeelsActualHeight,
                            currentHeelsHasTempOffset);
                        InvalidateApplyStateForHeightSourceChange();
                        SaveConfig();
                    }
                }

                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(Localization.SimpleHeelsHeightActualTooltip);
            }

            ImGui.Unindent();
        }

        private void BuildStatusBarTexts(out string heightText, out string matchText, out bool hasMatch)
        {
            heightText = isSimpleHeelsAvailable && isSimpleHeelsIpcReady
                ? currentHeelsHeight.ToString($"F{Configuration.DecimalPrecision}")
                : "-";

            // 状态栏显示“最后一条命中规则”（按列表顺序最后匹配、即最终生效覆盖者），而非第一条。
            var displayMatchedIndex = currentAppliedRuleIndices.Count > 0
                ? currentAppliedRuleIndices[^1]
                : -1;
            if (displayMatchedIndex >= 0 && displayMatchedIndex < ActiveRules.Count)
            {
                hasMatch = true;
                matchText = Localization.WindowMatchSummary(
                    displayMatchedIndex,
                    ActiveRules[displayMatchedIndex].Name);
            }
            else
            {
                hasMatch = false;
                matchText = Localization.NoRuleMatched;
            }
        }

        private string BuildDtrStatusText()
        {
            BuildStatusBarTexts(out var heightText, out var matchText, out _);
            return $"{Localization.StatusBarHeight(heightText)} | {matchText}";
        }

        private static SeString ToDtrSeString(string text) => new(new TextPayload(text));

        private IDtrBarEntry AcquireDtrBarEntry(string text)
        {
            if (dtrBarEntry != null)
                return dtrBarEntry;

            try
            {
                dtrBarEntry = DtrBar.Get(DtrBarEntryTitle, ToDtrSeString(text));
            }
            catch (ArgumentException)
            {
                // Remove 可能尚未生效，或插件重载后条目仍残留在 DTR
                DtrBar.Remove(DtrBarEntryTitle);
                dtrBarEntry = DtrBar.Get(DtrBarEntryTitle, ToDtrSeString(text));
            }

            dtrBarEntry.Tooltip = ToDtrSeString(Localization.DtrBarTooltip);
            dtrBarEntry.OnClick = _ => drawConfigUi = true;
            return dtrBarEntry;
        }

        private void UpdateDtrBar()
        {
            try
            {
                if (!Configuration.ShowDtrStatusBar)
                {
                    RemoveDtrBarEntry();
                    RemoveDtrSfwBarEntry();
                    return;
                }

                if (!ClientState.IsLoggedIn)
                {
                    if (dtrBarEntry != null)
                        dtrBarEntry.Shown = false;
                    if (dtrSfwBarEntry != null)
                        dtrSfwBarEntry.Shown = false;
                    return;
                }

                var text = BuildDtrStatusText();
                var entry = AcquireDtrBarEntry(text);
                entry.Shown = true;
                if (text != lastDtrBarText)
                {
                    entry.Text = ToDtrSeString(text);
                    lastDtrBarText = text;
                }

                UpdateDtrSfwBar();
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to update DTR status bar");
                RemoveDtrBarEntry();
                RemoveDtrSfwBarEntry();
            }
        }

        private IDtrBarEntry AcquireDtrSfwBarEntry(string text)
        {
            if (dtrSfwBarEntry != null)
                return dtrSfwBarEntry;

            try
            {
                dtrSfwBarEntry = DtrBar.Get(DtrSfwBarEntryTitle, ToDtrSeString(text));
            }
            catch (ArgumentException)
            {
                DtrBar.Remove(DtrSfwBarEntryTitle);
                dtrSfwBarEntry = DtrBar.Get(DtrSfwBarEntryTitle, ToDtrSeString(text));
            }

            dtrSfwBarEntry.Tooltip = ToDtrSeString(Localization.DtrSfwBarTooltip);
            dtrSfwBarEntry.OnClick = _ => SetSfwModeActive(!Configuration.SfwModeActive);
            return dtrSfwBarEntry;
        }

        private void UpdateDtrSfwBar()
        {
            var text = Configuration.SfwModeActive
                ? Localization.DtrSfwBarActive
                : Localization.DtrSfwBarInactive;
            var entry = AcquireDtrSfwBarEntry(text);
            entry.Shown = true;
            if (text == lastDtrSfwBarText)
                return;

            entry.Text = ToDtrSeString(text);
            lastDtrSfwBarText = text;
        }

        private void RemoveDtrBarEntry()
        {
            try
            {
                dtrBarEntry?.Remove();
                DtrBar.Remove(DtrBarEntryTitle);
            }
            catch (Exception ex)
            {
                PluginLog.Debug(ex, "DTR entry remove failed (may already be gone)");
            }

            dtrBarEntry = null;
            lastDtrBarText = "";
        }

        private void RemoveDtrSfwBarEntry()
        {
            try
            {
                dtrSfwBarEntry?.Remove();
                DtrBar.Remove(DtrSfwBarEntryTitle);
            }
            catch (Exception ex)
            {
                PluginLog.Debug(ex, "DTR SFW entry remove failed (may already be gone)");
            }

            dtrSfwBarEntry = null;
            lastDtrSfwBarText = "";
        }

        private void DrawPersistentStatusBar()
        {
            BuildStatusBarTexts(out var heightText, out var matchText, out var hasMatch);

            ImGui.TextDisabled(Localization.StatusBarHeight(heightText));
            ImGui.SameLine();
            ImGui.TextDisabled("|");
            ImGui.SameLine();

            if (hasMatch)
                ImGui.TextColored(MatchedRuleStatusColor, matchText);
            else
                ImGui.TextDisabled(matchText);

            ImGui.SameLine();
            ImGui.TextDisabled("|");
            ImGui.SameLine();

            var sfwActive = Configuration.SfwModeActive;
            if (ImGui.Checkbox(Localization.SfwModeToggleLabel, ref sfwActive))
                SetSfwModeActive(sfwActive);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Localization.SfwModeToggleTooltip);

            ImGui.Separator();
        }

        private void DrawSfwModeTab()
        {
            ImGui.TextWrapped(Localization.SfwModeTabDescription);
            ImGui.Spacing();

            var sfwActive = Configuration.SfwModeActive;
            if (ImGui.Checkbox(Localization.SfwModeActiveLabel, ref sfwActive))
                SetSfwModeActive(sfwActive);
            ImGuiComponents.HelpMarker(Localization.SfwModeActiveTooltip);

            if (!Configuration.UsePenumbraTemporaryApply)
            {
                ImGui.Spacing();
                ImGui.TextColored(RuleUnreachableWarningColor, Localization.SfwModeRequiresTemporaryApply);
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            Configuration.SfwModeActionGroups ??= new List<PenumbraActionGroup>();
            DrawSfwModeActionsList();
        }

        private void DrawSfwModeActionsList()
        {
            var groups = Configuration.SfwModeActionGroups!;

            if (ImGui.Button(Localization.AddPenumbraActionGroup))
            {
                groups.Add(CreateDefaultPenumbraActionGroup());
                SaveConfig();
            }

            if (groups.Count == 0)
            {
                ImGui.Spacing();
                ImGui.TextDisabled(Localization.SfwModeActionsEmpty);
                return;
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                var group = groups[groupIndex];
                if (groupIndex > 0)
                {
                    ImGui.Spacing();
                    ImGui.Separator();
                    ImGui.Spacing();
                }

                ImGui.PushID(groupIndex);
                var subHostRuleIndex = ToSfwGroupRuleIndex(groupIndex);
                DrawPenumbraActionGroup(
                    group,
                    subHostRuleIndex,
                    PenumbraApplyLayerConfig.Sfw,
                    canDeleteGroup: true,
                    onDeleteGroup: () =>
                    {
                        Configuration.SfwModeActionGroups!.RemoveAt(groupIndex);
                        ClearPenumbraDedupForMod(
                            group.PenumbraCollection,
                            group.PenumbraModName,
                            PenumbraApplyLayerConfig.Sfw);
                        SaveConfig();
                    },
                    idPrefix: $"SfwG{groupIndex}",
                    groupReorderRuleIndex: SfwPenumbraGroupsReorderRuleIndex,
                    groupReorderIndex: groupIndex);
                ImGui.PopID();
            }

            ProcessActionDragReorder(SfwPenumbraGroupsReorderRuleIndex);
        }

        private string GetPenumbraGroupModLabel(PenumbraActionGroup group)
        {
            var modEntry = _penumbraInterop.GetMods()
                .FirstOrDefault(m => string.Equals(m.Directory, group.PenumbraModName, StringComparison.OrdinalIgnoreCase));
            return modEntry != null
                ? Localization.PenumbraModLabel(modEntry.DisplayName, modEntry.Directory)
                : group.PenumbraModName;
        }

        private Dictionary<int, string> GetPenumbraSubActionConflictHints(PenumbraActionGroup group)
        {
            var flatActions = FlattenPenumbraGroup(group);
            return RulePenumbraActionAnalysis.AnalyzeWithPartners(
                flatActions,
                (mod, option) => _penumbraInterop.GetOptionGroupType(mod, option))
                .ToDictionary(kv => kv.Key, kv => BuildPenumbraConflictHint(kv.Value));
        }

        private static string BuildPenumbraConflictHint(PenumbraActionConflictInfo info)
        {
            var baseHint = info.Kind switch
            {
                PenumbraActionConflictKind.ModEnableDisable => Localization.RulePenumbraModEnableDisableConflictHint,
                PenumbraActionConflictKind.DisableModBlocksOption => Localization.RulePenumbraDisableBlocksOptionHint,
                PenumbraActionConflictKind.OptionSetting => Localization.RulePenumbraOptionConflictHint,
                PenumbraActionConflictKind.MultiToggleSubOption => Localization.RulePenumbraMultiToggleConflictHint,
                _ => "",
            };

            if (info.Partners.Count == 0)
                return baseHint;

            var conflictWith = Localization.RulePenumbraConflictWithActions(info.Partners.Select(p => p + 1));
            return string.IsNullOrEmpty(baseHint) ? conflictWith : $"{baseHint} {conflictWith}";
        }

        private void DrawPenumbraActionGroup(
            PenumbraActionGroup group,
            int subHostRuleIndex,
            PenumbraApplyLayerConfig? dedupLayer,
            bool canDeleteGroup,
            Action onDeleteGroup,
            string idPrefix,
            int? groupReorderRuleIndex = null,
            int? groupReorderIndex = null,
            HeelsRuleAction? syncCollapseAction = null)
        {
            MigratePenumbraGroupLegacySubActions(group);
            if (syncCollapseAction != null)
                SyncPenumbraActionCollapse(syncCollapseAction, group);

            var modLabel = GetPenumbraGroupModLabel(group);

            ImGui.AlignTextToFramePadding();
            if (groupReorderRuleIndex.HasValue && groupReorderIndex.HasValue)
            {
                DrawActionReorderHandle(groupReorderRuleIndex.Value, groupReorderIndex.Value);
                ImGui.SameLine();
            }

            var expanded = !group.IsCollapsed;
            if (ImGui.ArrowButton($"##PenGroupCollapse{idPrefix}", expanded ? ImGuiDir.Down : ImGuiDir.Right))
            {
                group.IsCollapsed = !group.IsCollapsed;
                if (syncCollapseAction != null)
                    syncCollapseAction.IsActionCollapsed = group.IsCollapsed;
                SaveConfig();
            }

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Localization.PenumbraActionGroupCollapseTooltip);

            ImGui.SameLine();
            ImGui.TextColored(
                new Vector4(0.6f, 0.8f, 1.0f, 1.0f),
                Localization.PenumbraActionGroupTitle(group.PenumbraCollection, modLabel));

            if (canDeleteGroup)
            {
                ImGui.SameLine();
                if (ImGui.SmallButton($"{Localization.DeletePenumbraActionGroup}##PenGroupDel{idPrefix}"))
                {
                    onDeleteGroup();
                    return;
                }

                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(Localization.DeletePenumbraActionGroupTooltip);
            }

            if (group.IsCollapsed)
                return;

            ImGui.Indent();

            if (_penumbraInterop.IsIpcAvailable())
            {
                ImGui.SetNextItemWidth(-1);
                DrawPenumbraCollectionSelector(idPrefix, group, dedupLayer);
                ImGui.SetNextItemWidth(-1);
                DrawPenumbraModSelector(idPrefix, group, dedupLayer);
                ImGui.Spacing();
            }

            var penumbraConflictHints = GetPenumbraSubActionConflictHints(group);
            for (var subIndex = 0; subIndex < group.SubActions.Count; subIndex++)
            {
                if (subIndex > 0)
                {
                    ImGui.Spacing();
                    ImGui.Separator();
                    ImGui.Spacing();
                }

                ImGui.PushID(subIndex);
                DrawPenumbraSubActionRow(
                    subHostRuleIndex,
                    subIndex,
                    group,
                    group.SubActions[subIndex],
                    penumbraConflictHints,
                    out var deleted);
                ImGui.PopID();
                if (deleted)
                    break;
            }

            ProcessActionDragReorder(subHostRuleIndex);

            ImGui.Spacing();
            if (ImGui.SmallButton($"{Localization.AddPenumbraSubAction}##AddSub{idPrefix}"))
            {
                group.SubActions.Add(CreateDefaultPenumbraSubAction());
                SaveConfig();
            }

            ImGui.Unindent();
        }

        private void DrawRulePenumbraActionGroup(int ruleIndex, int actionIndex, HeelsRuleAction groupAction)
        {
            var group = EnsurePenumbraGroupOnAction(groupAction);
            SyncPenumbraActionCollapse(groupAction, group);
            var subHostRuleIndex = ToPenumbraSubHostRuleIndex(ruleIndex, actionIndex);
            DrawPenumbraActionGroup(
                group,
                subHostRuleIndex,
                PenumbraApplyLayerConfig.Rule,
                canDeleteGroup: true,
                onDeleteGroup: () =>
                {
                    ActiveRules[ruleIndex].Actions.RemoveAt(actionIndex);
                    PersistRuleSetAfterActionMutation();
                },
                idPrefix: $"R{ruleIndex}G{actionIndex}",
                groupReorderRuleIndex: ruleIndex,
                groupReorderIndex: actionIndex,
                syncCollapseAction: groupAction);
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
            ImGuiComponents.HelpMarker(Localization.BaselineActionsDesc);
            
            if (!headerOpen)
                return;
            
            ImGui.Indent();
            
            // 启用开关
            var useBaselineActions = ruleSet.UseBaselineActions;
            if (ImGui.Checkbox(Localization.BaselineActionsEnable, ref useBaselineActions))
            {
                ruleSet.UseBaselineActions = useBaselineActions;
                InvalidateApplyStateForBaselineChange();
                SaveConfig();
            }
            ImGuiComponents.HelpMarker(Localization.BaselineActionsEnableTooltip);
            
            if (!ruleSet.UseBaselineActions)
            {
                ImGui.Unindent();
                return;
            }

            if (Configuration.UsePenumbraTemporaryApply)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.75f, 0.75f, 0.75f, 1.0f));
                ImGui.TextWrapped(Localization.BaselinePenumbraDisabledInTemporaryMode);
                ImGui.PopStyleColor();
                ImGui.Spacing();
            }
            
            ImGui.Spacing();
            
            // 按钮：刷新扫描、忽略所有新参数
            if (ImGui.Button(Localization.BaselineRefresh))
            {
                UpdateBaselineConfigs(ruleSet);
                SaveConfig();
            }
            ImGuiComponents.HelpMarker(Localization.BaselineRefreshTooltip);
            
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
            ImGuiComponents.HelpMarker(Localization.BaselineDismissAllTooltip);
            
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
                    .Where(c => !Configuration.UsePenumbraTemporaryApply || c.ParameterId.Type != ActionType.Penumbra)
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
            
            var (displayName, fullInfo) = GetBaselineParameterDisplayInfo(param);
            ImGui.AlignTextToFramePadding();
            if (config.IsNew)
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.8f, 0.0f, 1.0f));
            if (config.IsNew)
                ImGui.Text($"{Localization.BaselineNewParameter} {displayName}");
            else
                ImGui.Text(displayName);
            if (config.IsNew)
                ImGui.PopStyleColor();
            if (!string.IsNullOrEmpty(fullInfo))
                ImGuiComponents.HelpMarker(fullInfo);
            
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
                InvalidateApplyStateForBaselineChange();
                SaveConfig();
            }
            
            var modeTooltip = config.Mode switch
            {
                BaselineMode.Auto => Localization.BaselineModeAutoTooltip,
                BaselineMode.Manual => Localization.BaselineModeManualTooltip,
                BaselineMode.Ignore => Localization.BaselineModeIgnoreTooltip,
                _ => ""
            };
            if (!string.IsNullOrEmpty(modeTooltip))
                ImGuiComponents.HelpMarker(modeTooltip);
            
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
                        $"{Localization.BaselinePreviewCollection(param.PenumbraCollection)}\n{Localization.BaselinePreviewModName(param.PenumbraModName)}");
                case ActionType.Glamourer:
                    var designName = _glamourerInterop.GetDesignNames(force: false)
                        .FirstOrDefault(name => name.Equals(param.GlamourerDesign, StringComparison.OrdinalIgnoreCase));
                    return (
                        designName ?? (param.GlamourerDesign?.Length > 40 ? param.GlamourerDesign.Substring(0, 37) + "..." : param.GlamourerDesign) ?? "",
                        Localization.BaselinePreviewDesign(param.GlamourerDesign));
                case ActionType.Moodles:
                    var moodleItems = _moodlesInterop.GetItems(force: false);
                    var moodleItem = moodleItems.FirstOrDefault(item =>
                        item.Id.ToString().Equals(param.MoodleGuid, StringComparison.OrdinalIgnoreCase));
                    return (
                        moodleItem != null ? moodleItem.Title : (param.MoodleGuid ?? ""),
                        moodleItem != null
                            ? $"{Localization.BaselinePreviewName(moodleItem.Title)}\nGUID: {param.MoodleGuid}"
                            : $"GUID: {param.MoodleGuid}");
                case ActionType.Honorific:
                    var titleText = Localization.BaselinePreviewTitle;
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
            ImGuiComponents.HelpMarker(Localization.BaselineManualStateTooltip);
        }

        private void DrawBaselinePenumbraManualSettings(BaselineActionConfig config, RuleSet ruleSet, string paramKey)
        {
            var modEnabled = config.ManualModEnabled;
            if (ImGui.Checkbox($"{Localization.BaselineManualModEnabled}##{paramKey}", ref modEnabled))
            {
                config.ManualModEnabled = modEnabled;
                InvalidateApplyStateForBaselineChange();
                SaveConfig();
            }
            
            ImGui.SameLine();
            if (ImGui.SmallButton($"{Localization.BaselineSyncFromRules}##{paramKey}"))
            {
                SyncBaselinePenumbraManualOptionsFromRules(ruleSet, config);
                SaveConfig();
            }
            ImGuiComponents.HelpMarker(Localization.BaselineSyncFromRulesTooltip);
            
            if (config.ManualPenumbraOptions.Count == 0)
            {
                ImGui.TextDisabled(Localization.BaselineNoOptionsSyncFromRules);
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

                        DrawPenumbraMultiToggleOnOffCombo(
                            optionName,
                            $"##BaselineMulti{paramKey}{i}{optionName}",
                            optionSetting.PenumbraMultiToggleStates,
                            optionName);
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
                                optionSetting.PenumbraOptionEnabled = true;
                                InvalidateApplyStateForBaselineChange();
                                SaveConfig();
                            }
                        }
                        ImGui.EndCombo();
                    }
                    
                    ImGui.SameLine();
                    var optionEnabled = optionSetting.PenumbraOptionEnabled;
                    var optionEnabledLabel = Localization.OptionOnShort;
                    if (ImGui.Checkbox($"{optionEnabledLabel}##{paramKey}{i}", ref optionEnabled))
                    {
                        optionSetting.PenumbraOptionEnabled = optionEnabled;
                        InvalidateApplyStateForBaselineChange();
                        SaveConfig();
                    }
                }
                
                ImGui.PopID();
            }
        }
        
        private bool scrollToLastRule = false;

        private void DrawRulesActionGuide()
        {
            if (!ImGui.CollapsingHeader(Localization.RulesActionGuideHeader))
                return;

            ImGui.Indent();
            ImGui.TextDisabled(Localization.PerActionTypeHint);
            ImGui.Spacing();
            ImGui.TextWrapped(Localization.ActionTypeHint);
            ImGui.TextWrapped(Localization.PenumbraPriorityWarning);
            ImGui.TextWrapped(Localization.GlamourerSlotConflictNote);
            ImGui.TextWrapped(Localization.OptionalAddonsHint);
            ImGui.TextDisabled(Localization.MoodlesRemoteApplyHint);
            ImGui.Unindent();
        }
        
        /// <summary>节流刷新 Glamourer 装备槽位冲突分析（设计槽位查询走 IPC，避免每帧调用）。</summary>
        private void RefreshGlamourerConflictsIfNeeded()
        {
            if (DateTime.UtcNow - glamourerConflictsComputedUtc < GlamourerConflictRefreshInterval)
                return;

            glamourerConflictsComputedUtc = DateTime.UtcNow;
            glamourerConflicts = RuleGlamourerConflictAnalysis.Analyze(
                ActiveRules,
                designName => _glamourerInterop.GetDesignAppliedEquipmentSlotsByName(designName));
        }

        private void DrawRulesTab()
        {
            RefreshGlamourerConflictsIfNeeded();
            RefreshMatchedModConflictsIfNeeded();

            // RuleSet 选择器
            DrawRuleSetSelector();

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
            var groups = ComputeRuleGroups();
            var ruleDeleted = false;
            for (var gi = 0; gi < groups.Count; gi++)
            {
                var (gStart, gEnd) = groups[gi];
                if (gStart < 0 || gStart >= activeRules.Count)
                    continue;

                // 分组之间用双分割线分隔
                if (gi > 0)
                {
                    ImGui.Spacing();
                    ImGui.Separator();
                    ImGui.Separator();
                    ImGui.Spacing();
                }

                var headerRule = activeRules[gStart];
                ImGui.PushID($"Group{gi}");
                DrawRuleGroupHeader(gStart, gEnd, gi);
                ImGui.PopID();

                if (headerRule.GroupCollapsed)
                    continue;

                for (var i = gStart; i <= gEnd && i < activeRules.Count; i++)
                {
                    ImGui.PushID(i);
                    DrawRuleRow(i, out var thisDeleted);
                    ImGui.PopID();

                    if (thisDeleted)
                    {
                        ruleDeleted = true;
                        break; // 删除了规则，立即退出循环避免索引错误
                    }

                    // 如果是最后一个规则且需要滚动到它
                    if (i == activeRules.Count - 1 && scrollToLastRule)
                    {
                        ImGui.SetScrollHereY(1.0f);
                        scrollToLastRule = false;
                    }
                }

                if (ruleDeleted)
                    break;
            }

            ProcessRuleDragReorder();
            ProcessGroupDragReorder();
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

            var showDtrStatusBar = Configuration.ShowDtrStatusBar;
            if (ImGui.Checkbox(Localization.ShowDtrStatusBar, ref showDtrStatusBar))
            {
                Configuration.ShowDtrStatusBar = showDtrStatusBar;
                SaveConfig();
                if (!showDtrStatusBar)
                    RemoveDtrBarEntry();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Localization.ShowDtrStatusBarTooltip);

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            DrawPenumbraSettingsSection();

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            DrawRestoreDefaultsSection();
            DrawDisablePenumbraTemporaryConfirmModal();
        }

        private void DrawPenumbraSettingsSection()
        {
            ImGui.Text(Localization.PenumbraSettingsSection);
            ImGuiComponents.HelpMarker(Localization.PenumbraSettingsSectionTooltip);

            var useTemporaryApply = Configuration.UsePenumbraTemporaryApply;
            if (ImGui.Checkbox(Localization.PenumbraTemporaryApply, ref useTemporaryApply))
            {
                if (useTemporaryApply)
                {
                    Configuration.UsePenumbraTemporaryApply = true;
                    SaveConfig();
                }
                else
                {
                    ImGui.OpenPopup("ConfirmDisablePenumbraTemporary");
                }
            }

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Localization.PenumbraTemporaryApplyTooltip);

            var autoOverwriteGlamourer = Configuration.AutoOverwriteGlamourerPenumbra;
            if (ImGui.Checkbox(Localization.PenumbraAutoOverwriteGlamourer, ref autoOverwriteGlamourer))
            {
                Configuration.AutoOverwriteGlamourerPenumbra = autoOverwriteGlamourer;
                SaveConfig();
            }

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Localization.PenumbraAutoOverwriteGlamourerTooltip);
        }

        private void DrawDisablePenumbraTemporaryConfirmModal()
        {
            var popupOpen = true;
            if (!ImGui.BeginPopupModal("ConfirmDisablePenumbraTemporary", ref popupOpen, ImGuiWindowFlags.AlwaysAutoResize))
                return;

            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.55f, 0.25f, 1.0f));
            ImGui.TextWrapped(Localization.PenumbraDisableTemporaryConfirmMessage);
            ImGui.PopStyleColor();
            ImGui.Spacing();

            if (ImGui.Button(Localization.Confirm))
            {
                Configuration.UsePenumbraTemporaryApply = false;
                _penumbraInterop.ReadIncludingTemporarySettings = false;
                ClearPenumbraTemporaryOverrides();
                SaveConfig();
                ImGui.CloseCurrentPopup();
            }

            ImGui.SameLine();
            if (ImGui.Button(Localization.Cancel))
                ImGui.CloseCurrentPopup();

            ImGui.EndPopup();
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
            Configuration.ApplyCooldownSeconds = 0.5f;
            Configuration.RuleMatchStableSeconds = 0.25f;
            Configuration.UiLanguage = UiLanguagePreference.System;
            Configuration.SimpleHeelsHeightMode = SimpleHeelsHeightMode.Default; // 保留用于兼容性
            Configuration.WindowWidth = DefaultWindowWidth;
            Configuration.WindowHeight = DefaultWindowHeight;
            Configuration.UsePenumbraTemporaryApply = true;
            Configuration.AutoOverwriteGlamourerPenumbra = false;
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
            
            // 在每条规则之间绘制分界线（第一条规则、以及分组首条除外，后者已有分组头分隔）
            if (ruleIndex > 0 && !IsRuleGroupStart(ruleIndex))
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

            var ruleIsApplied = IsRuleApplied(ruleIndex);
            if (ruleIsApplied)
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
                    if (ruleIsApplied)
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

            if (ruleIsApplied)
                ImGui.PopStyleColor(3);

            if (ruleIndex < activeRules.Count - 1)
                DrawRuleConnector(ruleIndex, currentRule);
        }

        /// <summary>分组头：显示折叠箭头、分组序号、可编辑名称、规则条数与整组拖拽手柄。</summary>
        private void DrawRuleGroupHeader(int groupStartIndex, int groupEndIndex, int groupZeroBasedIndex)
        {
            var rules = ActiveRules;
            if (groupStartIndex < 0 || groupStartIndex >= rules.Count)
                return;

            var rule = rules[groupStartIndex];
            var ordinal = groupZeroBasedIndex + 1;
            var ruleCount = Math.Max(1, groupEndIndex - groupStartIndex + 1);

            var headerTop = ImGui.GetCursorScreenPos();
            ImGui.Spacing();
            ImGui.PushID($"RuleGroupHeader{groupStartIndex}");

            // 折叠/展开整个分组
            if (ImGui.ArrowButton("##GroupCollapse", rule.GroupCollapsed ? ImGuiDir.Right : ImGuiDir.Down))
            {
                rule.GroupCollapsed = !rule.GroupCollapsed;
                SaveConfig();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(rule.GroupCollapsed ? Localization.ExpandGroupTooltip : Localization.CollapseGroupTooltip);

            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.TextColored(new Vector4(0.55f, 0.8f, 1.0f, 1.0f), Localization.RuleGroupHeader(ordinal));
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Localization.RuleGroupHeaderTooltip);

            ImGui.SameLine();
            ImGui.SetNextItemWidth(220);
            var name = rule.GroupName ?? "";
            if (ImGui.InputTextWithHint($"##GroupName{groupStartIndex}", Localization.RuleGroupNameHint, ref name, 64))
            {
                rule.GroupName = name;
                SaveConfig();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Localization.RuleGroupNameTooltip);

            // 规则条数
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f), Localization.RuleGroupRuleCount(ruleCount));

            // 整组拖拽手柄
            ImGui.SameLine();
            if (ImGui.Button($"{Localization.RuleDragHandle}##GroupDrag"))
            {
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Localization.GroupDragHandleTooltip);
            if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                _groupDragSourceIndex ??= groupZeroBasedIndex;

            ImGui.Separator();

            // 整组拖拽目标命中检测（覆盖整个分组头区域，含上下判断）
            var rectMin = new Vector2(headerTop.X, headerTop.Y);
            var rectMax = new Vector2(headerTop.X + ImGui.GetContentRegionAvail().X, ImGui.GetCursorScreenPos().Y);
            UpdateGroupDragTarget(groupZeroBasedIndex, rectMin, rectMax);

            ImGui.PopID();
        }

        /// <summary>绘制规则之间的分组边界控制（同一分组 / 结束本组并开启新分组）。</summary>
        private void DrawRuleConnector(int ruleIndex, HeelsRule rule)
        {
            var startsNewGroup = rule.OperatorToNext == LogicOperator.And;
            ImGui.PushID($"RuleConnector{ruleIndex}");

            ImGui.Spacing();

            var color = startsNewGroup
                ? new Vector4(0.4f, 0.7f, 1.0f, 1.0f)
                : new Vector4(0.7f, 0.7f, 0.7f, 1.0f);

            ImGui.AlignTextToFramePadding();
            ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f), Localization.RuleConnectorPrefix);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(startsNewGroup ? Localization.RuleConnectorAndTooltip : Localization.RuleConnectorOrTooltip);

            ImGui.SameLine();
            ImGui.SetNextItemWidth(260);
            ImGui.PushStyleColor(ImGuiCol.Text, color);
            var comboOpen = ImGui.BeginCombo("##RuleOperatorToNext", startsNewGroup ? Localization.RuleConnectorAnd : Localization.RuleConnectorOr);
            ImGui.PopStyleColor();
            if (comboOpen)
            {
                if (ImGui.Selectable(Localization.RuleConnectorOr, !startsNewGroup))
                {
                    rule.OperatorToNext = LogicOperator.Or;
                    FixMisplacedElseBranches(ActiveRules);
                    SaveConfig();
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(Localization.RuleConnectorOrTooltip);

                if (ImGui.Selectable(Localization.RuleConnectorAnd, startsNewGroup))
                {
                    rule.OperatorToNext = LogicOperator.And;
                    FixMisplacedElseBranches(ActiveRules);
                    SaveConfig();
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(Localization.RuleConnectorAndTooltip);

                ImGui.EndCombo();
            }

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(startsNewGroup ? Localization.RuleConnectorAndTooltip : Localization.RuleConnectorOrTooltip);

            ImGui.Spacing();
            ImGui.PopID();
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
                ImGui.Text(Localization.ConfirmDeleteRuleMessage);
                ImGui.Spacing();
                
                if (ImGui.Button(Localization.Confirm, new Vector2(120, 0)))
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
                
                if (ImGui.Button(Localization.Cancel, new Vector2(120, 0)))
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
            // 上下判断：鼠标在目标行中线以下则放到目标之后，否则放到目标之前。
            _ruleDropAfter = mousePos.Y > (rowMin.Y + rowMax.Y) * 0.5f;
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
                var insertIndex = ResolveDropInsertIndex(
                    _ruleDragSourceIndex.Value, _ruleDragTargetIndex.Value, _ruleDropAfter);
                ReorderRule(_ruleDragSourceIndex.Value, insertIndex);
            }

            _ruleDragSourceIndex = null;
            _ruleDragTargetIndex = null;
        }

        /// <summary>
        /// 根据拖拽源、目标与“放到目标之后/之前”计算移除源元素后应插入的最终下标。
        /// 适用于所有单列表拖拽（规则、行动、子行动等），from/to 必须是同一列表的下标。
        /// </summary>
        private static int ResolveDropInsertIndex(int fromIndex, int toIndex, bool dropAfter)
        {
            var insertPos = dropAfter ? toIndex + 1 : toIndex;
            if (fromIndex < insertPos)
                insertPos--;
            return insertPos < 0 ? 0 : insertPos;
        }

        /// <summary>按分组边界（OperatorToNext==And 或末尾）把当前规则列表切分为 [起,止] 区间。</summary>
        private List<(int Start, int End)> ComputeRuleGroups()
        {
            var rules = ActiveRules;
            var groups = new List<(int Start, int End)>();
            if (rules.Count == 0)
                return groups;

            var start = 0;
            for (var i = 0; i < rules.Count; i++)
            {
                if (rules[i].OperatorToNext == LogicOperator.And || i == rules.Count - 1)
                {
                    groups.Add((start, i));
                    start = i + 1;
                }
            }

            return groups;
        }

        private void UpdateGroupDragTarget(int groupIndex, Vector2 rectMin, Vector2 rectMax)
        {
            if (!_groupDragSourceIndex.HasValue || !ImGui.IsMouseDown(ImGuiMouseButton.Left))
                return;

            var mousePos = ImGui.GetIO().MousePos;
            if (mousePos.X < rectMin.X || mousePos.X > rectMax.X
                || mousePos.Y < rectMin.Y || mousePos.Y > rectMax.Y)
            {
                return;
            }

            _groupDragTargetIndex = groupIndex;
            _groupDropAfter = mousePos.Y > (rectMin.Y + rectMax.Y) * 0.5f;
        }

        private void ProcessGroupDragReorder()
        {
            if (!_groupDragSourceIndex.HasValue)
                return;

            if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                ImGui.SetTooltip(Localization.GroupDragPreview(_groupDragSourceIndex.Value + 1));
                return;
            }

            if (_groupDragTargetIndex.HasValue
                && _groupDragSourceIndex.Value != _groupDragTargetIndex.Value)
            {
                ReorderRuleGroup(_groupDragSourceIndex.Value, _groupDragTargetIndex.Value, _groupDropAfter);
            }

            _groupDragSourceIndex = null;
            _groupDragTargetIndex = null;
        }

        /// <summary>整组拖拽排序：把第 fromGroup 个分组整体移动到目标分组之前/之后，并重建分组边界。</summary>
        private void ReorderRuleGroup(int fromGroup, int toGroup, bool dropAfter)
        {
            var rules = ActiveRules;
            var groups = ComputeRuleGroups();
            if (fromGroup < 0 || fromGroup >= groups.Count
                || toGroup < 0 || toGroup >= groups.Count
                || fromGroup == toGroup)
            {
                return;
            }

            // 拆分为分组块
            var blocks = new List<List<HeelsRule>>();
            foreach (var (s, e) in groups)
            {
                var block = new List<HeelsRule>();
                for (var i = s; i <= e && i < rules.Count; i++)
                    block.Add(rules[i]);
                blocks.Add(block);
            }

            var moving = blocks[fromGroup];
            blocks.RemoveAt(fromGroup);

            var insertPos = ResolveDropInsertIndex(fromGroup, toGroup, dropAfter);
            if (insertPos > blocks.Count)
                insertPos = blocks.Count;
            blocks.Insert(insertPos, moving);

            // 重建规则列表，并按“块尾=And 开新组、块内=Or 同组、最后一块块尾=Or”重设边界。
            rules.Clear();
            for (var b = 0; b < blocks.Count; b++)
            {
                var block = blocks[b];
                for (var k = 0; k < block.Count; k++)
                {
                    var r = block[k];
                    var isBlockLast = k == block.Count - 1;
                    r.OperatorToNext = isBlockLast && b < blocks.Count - 1
                        ? LogicOperator.And
                        : LogicOperator.Or;
                    rules.Add(r);
                }
            }

            FixMisplacedElseBranches(rules);
            SaveConfig();
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
            // “否则”应是其分组的最后一条。仅当同一分组内它后面还有已启用规则时才不可达（黄色警告）；
            // 跨分组（AND 边界之后）的规则属于独立分组、仍可达，不应警告。
            var isInvalidElse = ruleIndex > 0
                && currentRule.BranchKind == RuleBranchKind.Else
                && HasActiveRuleLaterInSameGroup(ruleIndex);
            var hasUnreachableWarning = isUnreachable || isInvalidElse;
            var isMatched = IsRuleApplied(ruleIndex);
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
                
                // 新行：显示分支类型和条件组信息（分组首条始终显示为“如果”）
                var branchLabel = IsRuleGroupStart(ruleIndex) ? Localization.RuleOrderIf : Localization.RuleBranchLabel(currentRule.BranchKind);
                
                if (currentRule.BranchKind == RuleBranchKind.Else && !IsRuleGroupStart(ruleIndex))
                {
                    // 否则分支，显示 "否则" 与 SFW 参与状态
                    var sfwBadge = Localization.RuleSfwModeStatusBadge(currentRule.SfwModeEnabled);
                    ImGui.Text($"{branchLabel} {sfwBadge}");
                }
                else
                {
                    // 否则如果或如果，显示条件组数量与 SFW 参与状态
                    var groupCount = currentRule.ConditionGroups?.Count ?? 0;
                    var groupText = Localization.ConditionGroupCount(groupCount);
                    var sfwBadge = Localization.RuleSfwModeStatusBadge(currentRule.SfwModeEnabled);
                    ImGui.Text($"{branchLabel} - {groupText} {sfwBadge}");
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
            // 分组首条（列表首条或上一条为“新分组”）始终是“如果”，不提供分支下拉。
            if (IsRuleGroupStart(ruleIndex))
            {
                // 规整：分组首条不能是“否则”（否则会无条件命中）。
                if (rule.BranchKind == RuleBranchKind.Else)
                {
                    rule.BranchKind = RuleBranchKind.ElseIf;
                    SaveConfig();
                }

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

            // 否则分支无独立条件组，在条件区顶部单独显示 SFW 参与开关
            if (isElseBranch)
            {
                DrawRuleSfwModeParticipationControl(ruleIndex, rule);
                ImGui.Spacing();
            }
            
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
        
        /// <summary>规则在 SFW 模式下是否参与匹配（显示在首个条件组标题行，或否则分支条件区顶部）。</summary>
        private void DrawRuleSfwModeParticipationControl(int ruleIndex, HeelsRule rule)
        {
            var enabled = rule.SfwModeEnabled;
            if (ImGui.Checkbox($"{Localization.RuleSfwModeEnabledLabel}##SfwRule{ruleIndex}", ref enabled))
            {
                rule.SfwModeEnabled = enabled;
                lastApplyUtc = DateTime.MinValue;
                SaveConfig();
            }

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Localization.RuleSfwModeEnabledTooltip);
        }

        /// <summary>绘制条件组标题栏（包含组内操作符选择器和删除按钮）</summary>
        private void DrawConditionGroupHeader(int ruleIndex, int groupIndex, ConditionGroup group, bool hasUnreachableWarning)
        {
            var rule = ActiveRules[ruleIndex];

            // 首个条件组标题行：SFW 参与开关 + 状态徽章（折叠规则摘要也会显示徽章）
            if (groupIndex == 0)
            {
                DrawRuleSfwModeParticipationControl(ruleIndex, rule);
                ImGui.SameLine();
                ImGui.AlignTextToFramePadding();
                var badgeColor = rule.SfwModeEnabled
                    ? new Vector4(0.55f, 0.85f, 0.55f, 1.0f)
                    : new Vector4(0.75f, 0.75f, 0.75f, 1.0f);
                ImGui.TextColored(badgeColor, Localization.RuleSfwModeStatusBadge(rule.SfwModeEnabled));
                ImGui.SameLine();
            }
            
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
                    
                    if (ImGui.Button(Localization.Confirm))
                    {
                        rule.ConditionGroups.RemoveAt(groupIndex);
                        SaveConfig();
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.SameLine();
                    if (ImGui.Button(Localization.Cancel))
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
            var heightTolerance = MathF.Pow(10f, -Math.Clamp(Configuration.DecimalPrecision, 0, 5));
            var conditionConflicts = RuleConditionConflictAnalysis.Analyze(group, heightTolerance);

            if (conditions.Count > 0)
                DrawSubtleConditionSeparator();

            // 绘制每个条件
            for (int i = 0; i < conditions.Count; i++)
            {
                if (i > 0)
                    DrawSubtleConditionSeparator();

                ImGui.PushID($"Condition_{i}");
                
                var condition = conditions[i];
                
                // 拖拽手柄
                if (ImGui.Button(Localization.RuleDragHandle))
                {
                }
                
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(Localization.DragToReorderConditions);
                
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
                    ImGui.SetTooltip(Localization.DeleteThisCondition);
                
                // 删除确认对话框（必须在同一个上下文中渲染）
                bool popupOpen = true;
                if (ImGui.BeginPopupModal($"ConfirmDeleteCondition_{ruleIndex}_{groupIndex}_{i}", ref popupOpen, ImGuiWindowFlags.AlwaysAutoResize))
                {
                    ImGui.Text(Localization.ConfirmDeleteCondition);
                    ImGui.Spacing();
                    
                    if (ImGui.Button(Localization.Confirm))
                    {
                        group.Conditions.RemoveAt(i);
                        SaveConfig();
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.SameLine();
                    if (ImGui.Button(Localization.Cancel))
                    {
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.EndPopup();
                }
                
                ImGui.SameLine();

                var hasConditionConflict = conditionConflicts.TryGetValue(i, out var conflictKind);
                if (hasConditionConflict)
                {
                    ImGui.TextColored(RuleConditionConflictColor, "!");
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip(GetConditionConflictHint(conflictKind));
                    ImGui.SameLine();
                }
                
                if (hasUnreachableWarning)
                    ImGui.BeginDisabled();

                if (hasConditionConflict)
                    ImGui.PushStyleColor(ImGuiCol.Text, RuleConditionConflictColor);
                
                // 根据条件类型绘制编辑器
                if (condition is HeightCondition heightCond)
                {
                    DrawHeightConditionEditor(ruleIndex, groupIndex, i, heightCond, formatString);
                }
                else if (condition is EquipmentCondition equipCond)
                {
                    DrawEquipmentConditionEditor(ruleIndex, groupIndex, i, equipCond);
                }

                if (hasConditionConflict)
                    ImGui.PopStyleColor();
                
                if (hasUnreachableWarning)
                    ImGui.EndDisabled();
                
                ImGui.PopID();
            }

            if (conditions.Count > 0)
                DrawSubtleConditionSeparator();
            
            // 处理条件拖拽重排序
            ProcessConditionDragReorder(ruleIndex, groupIndex);
            
            // 删除确认对话框已移至主配置窗口统一处理
            
            // 添加条件按钮
            if (ImGui.Button($"+##AddCondition_{groupIndex}"))
            {
                ImGui.OpenPopup($"AddConditionPopup_{groupIndex}");
            }
            
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Localization.AddNewConditionTooltip);
            
            // 添加条件弹出菜单
            if (ImGui.BeginPopup($"AddConditionPopup_{groupIndex}"))
            {
                if (ImGui.Selectable(Localization.ConditionTypeHeight))
                {
                    group.Conditions.Add(new HeightCondition
                    {
                        Comparison = HeightComparison.LessThanOrEqual,
                        Value = 0.0f
                    });
                    SaveConfig();
                }
                
                if (ImGui.Selectable(Localization.ConditionTypeEquipment))
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
            ImGui.TextDisabled($"({Localization.ToNextGroupLabel})");
            
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
                ImGui.SetTooltip(Localization.MovingCondition(_conditionDragSourceIndex.Value));
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
                ImGui.Text(Localization.ConfirmDeleteActionMessage);
                ImGui.Spacing();
                
                if (ImGui.Button(Localization.Confirm, new Vector2(120, 0)))
                {
                    if (TryParsePenumbraSubHostRuleIndex(
                            actionToDeleteRuleIndex,
                            out var hostRuleIndex,
                            out var groupActionIndex)
                        && actionToDeleteIndex >= 0
                        && hostRuleIndex >= 0
                        && hostRuleIndex < ActiveRules.Count
                        && groupActionIndex >= 0
                        && groupActionIndex < ActiveRules[hostRuleIndex].Actions.Count)
                    {
                        var groupAction = ActiveRules[hostRuleIndex].Actions[groupActionIndex];
                        var group = groupAction.PenumbraGroup;
                        if (group != null
                            && actionToDeleteIndex < group.SubActions.Count)
                        {
                            if (group.SubActions.Count == 1)
                                ActiveRules[hostRuleIndex].Actions.RemoveAt(groupActionIndex);
                            else
                                group.SubActions.RemoveAt(actionToDeleteIndex);

                            PersistRuleSetAfterActionMutation();
                            deleted = true;
                        }
                    }
                    else if (TryParseSfwGroupRuleIndex(actionToDeleteRuleIndex, out var sfwGroupIndex)
                        && actionToDeleteIndex >= 0)
                    {
                        Configuration.SfwModeActionGroups ??= new List<PenumbraActionGroup>();
                        if (sfwGroupIndex < Configuration.SfwModeActionGroups.Count)
                        {
                            var sfwGroup = Configuration.SfwModeActionGroups[sfwGroupIndex];
                            if (actionToDeleteIndex < sfwGroup.SubActions.Count)
                            {
                                if (sfwGroup.SubActions.Count == 1)
                                {
                                    ClearPenumbraDedupForMod(
                                        sfwGroup.PenumbraCollection,
                                        sfwGroup.PenumbraModName,
                                        PenumbraApplyLayerConfig.Sfw);
                                    Configuration.SfwModeActionGroups.RemoveAt(sfwGroupIndex);
                                    if (Configuration.SfwModeActionGroups.All(g => g.SubActions.Count == 0))
                                        ClearSfwPenumbraApplyTracking();
                                }
                                else
                                {
                                    sfwGroup.SubActions.RemoveAt(actionToDeleteIndex);
                                }

                                PersistRuleSetAfterActionMutation();
                                deleted = true;
                            }
                        }
                    }
                    else if (actionToDeleteRuleIndex >= 0 
                        && actionToDeleteRuleIndex < ActiveRules.Count
                        && actionToDeleteIndex >= 0)
                    {
                        var rule = ActiveRules[actionToDeleteRuleIndex];
                        if (actionToDeleteIndex < rule.Actions.Count)
                        {
                            rule.Actions.RemoveAt(actionToDeleteIndex);
                            PersistRuleSetAfterActionMutation();
                            deleted = true;
                        }
                    }
                    
                    actionToDeleteRuleIndex = -1;
                    actionToDeleteIndex = -1;
                    ImGui.CloseCurrentPopup();
                }
                
                ImGui.SameLine();
                
                if (ImGui.Button(Localization.Cancel, new Vector2(120, 0)))
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

        private void UpdateActionDragTarget(int ruleIndex, int actionIndex)
        {
            if (_actionDragSourceRuleIndex != ruleIndex
                || !_actionDragSourceIndex.HasValue
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

            _actionDragTargetIndex = actionIndex;
            // 上下判断：鼠标在目标行中线以下则放到目标之后，否则放到目标之前。
            _actionDropAfter = mousePos.Y > (rowMin.Y + rowMax.Y) * 0.5f;
        }

        private void ProcessActionDragReorder(int ruleIndex)
        {
            if (_actionDragSourceRuleIndex != ruleIndex || !_actionDragSourceIndex.HasValue)
                return;

            if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                ImGui.SetTooltip(Localization.MovingAction(_actionDragSourceIndex.Value));
                return;
            }

            if (_actionDragTargetIndex.HasValue
                && _actionDragSourceIndex.Value != _actionDragTargetIndex.Value)
            {
                var insertIndex = ResolveDropInsertIndex(
                    _actionDragSourceIndex.Value, _actionDragTargetIndex.Value, _actionDropAfter);
                ReorderRuleAction(ruleIndex, _actionDragSourceIndex.Value, insertIndex);
            }

            _actionDragSourceRuleIndex = null;
            _actionDragSourceIndex = null;
            _actionDragTargetIndex = null;
        }

        private void ReorderRuleAction(int ruleIndex, int fromIndex, int toIndex)
        {
            if (ruleIndex == SfwPenumbraGroupsReorderRuleIndex)
            {
                Configuration.SfwModeActionGroups ??= new List<PenumbraActionGroup>();
                ReorderPenumbraActionGroups(Configuration.SfwModeActionGroups, fromIndex, toIndex);
                return;
            }

            if (TryParsePenumbraSubHostRuleIndex(ruleIndex, out var hostRuleIndex, out var groupActionIndex))
            {
                var groupAction = ActiveRules[hostRuleIndex].Actions[groupActionIndex];
                var group = EnsurePenumbraGroupOnAction(groupAction);
                ReorderPenumbraSubActions(group.SubActions, fromIndex, toIndex);
                return;
            }

            if (TryParseSfwGroupRuleIndex(ruleIndex, out var sfwGroupIndex))
            {
                Configuration.SfwModeActionGroups ??= new List<PenumbraActionGroup>();
                ReorderPenumbraSubActions(
                    Configuration.SfwModeActionGroups[sfwGroupIndex].SubActions,
                    fromIndex,
                    toIndex);
                return;
            }

            if (ruleIndex < 0 || ruleIndex >= ActiveRules.Count)
                return;

            EnsureRuleHasActions(ruleIndex);
            var actions = ActiveRules[ruleIndex].Actions;
            if (fromIndex < 0 || toIndex < 0
                || fromIndex >= actions.Count
                || toIndex >= actions.Count
                || fromIndex == toIndex)
            {
                return;
            }

            var action = actions[fromIndex];
            actions.RemoveAt(fromIndex);
            actions.Insert(toIndex, action);
            SaveConfig();
        }

        private void ReorderPenumbraSubActions(List<PenumbraSubAction> subActions, int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || toIndex < 0
                || fromIndex >= subActions.Count
                || toIndex >= subActions.Count
                || fromIndex == toIndex)
            {
                return;
            }

            var subAction = subActions[fromIndex];
            subActions.RemoveAt(fromIndex);
            subActions.Insert(toIndex, subAction);
            SaveConfig();
        }

        private void ReorderPenumbraActionGroups(List<PenumbraActionGroup> groups, int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || toIndex < 0
                || fromIndex >= groups.Count
                || toIndex >= groups.Count
                || fromIndex == toIndex)
            {
                return;
            }

            var group = groups[fromIndex];
            groups.RemoveAt(fromIndex);
            groups.Insert(toIndex, group);
            SaveConfig();
        }

        private void DrawActionReorderHandle(int ruleIndex, int actionIndex)
        {
            if (ImGui.Button($"{Localization.RuleDragHandle}##ActionDrag{ruleIndex}_{actionIndex}"))
            {
            }

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Localization.DragToReorderActions);

            if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
            {
                if (!_actionDragSourceIndex.HasValue)
                {
                    _actionDragSourceRuleIndex = ruleIndex;
                    _actionDragSourceIndex = actionIndex;
                }
            }

            UpdateActionDragTarget(ruleIndex, actionIndex);

            if (_actionDragSourceRuleIndex == ruleIndex
                && _actionDragSourceIndex.HasValue
                && _actionDragTargetIndex == actionIndex
                && _actionDragSourceIndex != actionIndex)
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

        private static string GetConditionConflictHint(ConditionConflictKind kind) =>
            kind switch
            {
                ConditionConflictKind.HeightRange => Localization.RuleConditionHeightConflictHint,
                ConditionConflictKind.EquipmentSlot => Localization.RuleConditionEquipmentConflictHint,
                _ => Localization.RuleConditionEquipmentConflictHint,
            };

        private static void DrawSubtleConditionSeparator()
        {
            ImGui.Spacing();
            var separator = ImGui.GetStyle().Colors[(int)ImGuiCol.Separator];
            ImGui.PushStyleColor(ImGuiCol.Separator, new Vector4(
                separator.X,
                separator.Y,
                separator.Z,
                separator.W * 0.4f));
            ImGui.Separator();
            ImGui.PopStyleColor();
            ImGui.Spacing();
        }

        private void NormalizeEquipmentConditionSlot(EquipmentCondition condition)
        {
            if (EquipmentCondition.IsRuleEquipmentSlotSupported(condition.Slot))
                return;

            condition.Slot = EquipSlot.Feet;
            condition.MatchMode = EquipmentMatchMode.Any;
            SaveConfig();
        }

        private void DrawEquipmentConditionEditor(int ruleIndex, int groupIndex, int conditionIndex, EquipmentCondition condition)
        {
            NormalizeEquipmentConditionSlot(condition);

            ImGui.Text(Localization.ConditionTypeEquipment);
            ImGui.SameLine();

            ImGui.SetNextItemWidth(120);
            var slotPreview = Localization.EquipSlotName(condition.Slot);
            if (ImGui.BeginCombo($"##EquipSlot{ruleIndex}_{groupIndex}_{conditionIndex}", slotPreview))
            {
                foreach (EquipSlot slot in Enum.GetValues<EquipSlot>())
                {
                    if (!EquipmentCondition.IsRuleEquipmentSlotSupported(slot))
                        continue;

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
                    condition.MatchMode = EquipmentMatchMode.Any;
                    SaveConfig();
                }

                ImGui.EndCombo();
            }

            if (!condition.MustBeEquipped)
                return;

            ImGui.SetNextItemWidth(180);
            var matchPreview = condition.MatchMode == EquipmentMatchMode.SpecificModelId
                ? Localization.EquipmentMatchSpecificModelId
                : Localization.EquipmentMatchAny;
            if (ImGui.BeginCombo($"##EquipMatchMode{ruleIndex}_{groupIndex}_{conditionIndex}", matchPreview))
            {
                if (ImGui.Selectable(Localization.EquipmentMatchAny, condition.MatchMode == EquipmentMatchMode.Any))
                {
                    condition.MatchMode = EquipmentMatchMode.Any;
                    SaveConfig();
                }

                if (ImGui.Selectable(
                        Localization.EquipmentMatchSpecificModelId,
                        condition.MatchMode == EquipmentMatchMode.SpecificModelId))
                {
                    condition.MatchMode = EquipmentMatchMode.SpecificModelId;
                    SaveConfig();
                }

                ImGui.EndCombo();
            }

            if (!condition.UsesSpecificModelId)
                return;

            ImGui.SetNextItemWidth(88);
            var modelIdInput = (int)condition.TargetModelId;
            if (ImGui.InputInt($"##EquipModelId{ruleIndex}_{groupIndex}_{conditionIndex}", ref modelIdInput, 1, 10))
            {
                condition.TargetModelId = (ushort)Math.Clamp(modelIdInput, 0, ushort.MaxValue);
                SaveConfig();
            }

            ImGui.SameLine();
            ImGui.TextDisabled(Localization.EquipmentTargetModelIdLabel);
            ImGui.SameLine();

            if (ImGui.Button($"{Localization.EquipmentReadCurrentModelId}##EquipRead{ruleIndex}_{groupIndex}_{conditionIndex}"))
            {
                if (TryReadEquipmentConditionModelIdFromAppearance(condition.Slot, out var readModelId, out var readError))
                {
                    condition.TargetModelId = readModelId;
                    SaveConfig();
                }
                else if (!string.IsNullOrWhiteSpace(readError))
                {
                    lastError = readError;
                }
            }

            ImGui.SameLine();
            DrawEquipmentItemSearchField(ruleIndex, groupIndex, conditionIndex, condition);

            DrawEquipmentConditionModelIdPreview(condition.Slot, condition.TargetModelId);
        }

        private void DrawEquipmentItemSearchField(
            int ruleIndex,
            int groupIndex,
            int conditionIndex,
            EquipmentCondition condition)
        {
            var searchKey = $"{ruleIndex}:{groupIndex}:{conditionIndex}";
            if (!_equipmentItemSearchQueries.TryGetValue(searchKey, out var searchQuery))
                searchQuery = "";

            ImGui.SetNextItemWidth(200);
            ImGui.InputTextWithHint(
                $"##EquipItemSearch{ruleIndex}_{groupIndex}_{conditionIndex}",
                Localization.EquipmentItemSearchHint,
                ref searchQuery,
                128);
            _equipmentItemSearchQueries[searchKey] = searchQuery;

            if (string.IsNullOrWhiteSpace(searchQuery)
                || !EquipSlotRenderedMapping.TryToRendered(condition.Slot, out var renderedSlot))
            {
                return;
            }

            var results = ItemModelNameLookup.SearchByName(searchQuery, renderedSlot, DataManager);
            if (results.Count == 0)
            {
                ImGui.TextDisabled(Localization.EquipmentItemSearchNoResults);
                return;
            }

            ImGui.PushID($"EquipSearchResults{ruleIndex}_{groupIndex}_{conditionIndex}");
            var rowHeight = GameItemIconUi.RowHeight;
            var maxHeight = Math.Min(rowHeight * 6.5f, results.Count * rowHeight + ImGui.GetStyle().FramePadding.Y * 2f);
            ImGui.BeginChild($"##EquipSearchResults{ruleIndex}_{groupIndex}_{conditionIndex}", new Vector2(0, maxHeight), true);
            if (ImGui.BeginTable(
                    "EquipSearchResultTable",
                    2,
                    ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg | ImGuiTableFlags.PadOuterX))
            {
                ImGui.TableSetupColumn("Icon", ImGuiTableColumnFlags.WidthFixed, GameItemIconUi.IconSize + 4f);
                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);

                for (var resultIndex = 0; resultIndex < results.Count; resultIndex++)
                {
                    var result = results[resultIndex];
                    ImGui.PushID(resultIndex);
                    ImGui.TableNextRow(0, rowHeight);

                    ImGui.TableNextColumn();
                    ImGui.AlignTextToFramePadding();
                    GameItemIconUi.TryDraw(TextureProvider, result.IconId);

                    ImGui.TableNextColumn();
                    ImGui.AlignTextToFramePadding();
                    var label = result.Variant != 0
                        ? Localization.EquipmentItemSearchResult(result.Name, result.ModelId, result.Variant)
                        : Localization.EquipmentItemSearchResultNoVariant(result.Name, result.ModelId);
                    if (ImGui.Selectable(label, condition.TargetModelId == result.ModelId))
                    {
                        condition.TargetModelId = result.ModelId;
                        condition.MatchMode = EquipmentMatchMode.SpecificModelId;
                        _equipmentItemSearchQueries[searchKey] = "";
                        SaveConfig();
                    }

                    ImGui.PopID();
                }

                ImGui.EndTable();
            }

            ImGui.EndChild();
            ImGui.PopID();
        }

        private bool TryReadEquipmentConditionModelIdFromAppearance(
            EquipSlot slot,
            out ushort modelId,
            out string? error)
        {
            modelId = 0;
            error = null;

            if (!EquipmentCondition.IsRuleEquipmentSlotSupported(slot))
            {
                error = Localization.EquipmentModelIdReadFailed;
                return false;
            }

            var localPlayer = ObjectTable?.LocalPlayer;
            if (!DrawObjectEquipmentReader.TryReadLocalPlayerSlots(localPlayer, out var slots, out error))
            {
                error ??= Localization.EquipmentModelIdReadFailed;
                return false;
            }

            if (!EquipSlotRenderedMapping.TryToRendered(slot, out var renderedSlot))
            {
                error = Localization.EquipmentModelIdReadFailed;
                return false;
            }

            foreach (var slotInfo in slots)
            {
                if (slotInfo.Slot != renderedSlot)
                    continue;

                modelId = slotInfo.ModelId;
                return true;
            }

            error = Localization.EquipmentModelIdReadFailed;
            return false;
        }

        private void DrawEquipmentConditionModelIdPreview(EquipSlot slot, ushort modelId)
        {
            var previewText = BuildEquipmentConditionModelIdPreviewText(slot, modelId);
            if (EquipSlotRenderedMapping.TryToRendered(slot, out var renderedSlot)
                && ItemModelNameLookup.TryGetIconIdForRenderedSlot(modelId, renderedSlot, DataManager, out var iconId)
                && GameItemIconUi.TryDraw(TextureProvider, iconId))
            {
                ImGui.SameLine(0, 6);
            }

            ImGui.AlignTextToFramePadding();
            ImGui.TextDisabled(previewText);
        }

        private string BuildEquipmentConditionModelIdPreviewText(EquipSlot slot, ushort modelId)
        {
            if (!EquipSlotRenderedMapping.TryToRendered(slot, out var renderedSlot))
                return Localization.EquipmentModelIdPreviewNoMatch(modelId);

            var itemName = ItemModelNameLookup.LookupForRenderedSlot(modelId, renderedSlot, DataManager)
                .Select(e => e.Name)
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(itemName))
            {
                if (modelId == 0)
                    return Localization.EquipmentModelIdPreview(Localization.DebugDrawObjectSmallclothes, modelId);

                if (EmperorsNewItems.IsEmperorsNewByModelId(modelId))
                    return Localization.EquipmentModelIdPreview(Localization.DebugDrawObjectEmperorsNew, modelId);

                return Localization.EquipmentModelIdPreviewNoMatch(modelId);
            }

            return Localization.EquipmentModelIdPreview(itemName, modelId);
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
            var conflicts = RulePenumbraActionAnalysis.AnalyzeWithPartners(
                rule.Actions ?? [],
                (mod, option) => _penumbraInterop.GetOptionGroupType(mod, option));

            var hints = new Dictionary<int, string>();
            foreach (var (index, info) in conflicts)
                hints[index] = BuildPenumbraConflictHint(info);

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
                var action = actions[actionIndex];
                if (action.Type == ActionType.Penumbra)
                {
                    DrawRulePenumbraActionGroup(ruleIndex, actionIndex, action);
                }
                else
                {
                    DrawRuleActionRow(
                        ruleIndex,
                        actionIndex,
                        action,
                        actions.Count > 1,
                        penumbraConflictHints,
                        out var deleted);
                    if (deleted)
                        break;
                }
                ImGui.PopID();
            }

            ProcessActionDragReorder(ruleIndex);

            if (ImGui.SmallButton($"{Localization.AddPenumbraActionGroup}##PenGroup{ruleIndex}"))
            {
                actions.Add(CreateDefaultPenumbraGroupRuleAction());
                SaveConfig();
            }

            ImGui.SameLine();

            if (ImGui.SmallButton($"{Localization.AddRuleAction}##Rule{ruleIndex}"))
                ImGui.OpenPopup($"AddActionPopup_{ruleIndex}");

            DrawAddRuleActionPopup(ruleIndex, actions);
        }

        private void DrawAddRuleActionPopup(int ruleIndex, List<HeelsRuleAction> actions)
        {
            if (!ImGui.BeginPopup($"AddActionPopup_{ruleIndex}"))
                return;

            foreach (var actionType in new[]
                     {
                         ActionType.Glamourer,
                         ActionType.Penumbra,
                         ActionType.Honorific,
                         ActionType.Moodles,
                         ActionType.SoundMixer,
                     })
            {
                ImGui.PushStyleColor(ImGuiCol.Text, GetActionTypeTextColor(actionType));
                if (ImGui.Selectable(Localization.ActionTypeLabel(actionType)))
                {
                    actions.Add(CreateDefaultRuleActionForType(actionType));
                    SaveConfig();
                    ImGui.CloseCurrentPopup();
                }
                ImGui.PopStyleColor();
            }

            ImGui.EndPopup();
        }

        private void DrawPenumbraSubActionRow(
            int subHostRuleIndex,
            int subIndex,
            PenumbraActionGroup group,
            PenumbraSubAction sub,
            IReadOnlyDictionary<int, string> penumbraConflictHints,
            out bool deleted)
        {
            deleted = false;
            var flatAction = FlattenPenumbraSubAction(group, sub);
            var modDirectory = group.PenumbraModName ?? "";

            // 仅当临时层覆盖确实来自外部 Glamourer（非自己写入、也非本插件设计触发）时才视为“接管”警告。
            var hasGlamourerTakeover = IsPenumbraModUnderGlamourerTakeover(modDirectory)
                && !IsPenumbraModSelfManaged(modDirectory);

            var withinGroupConflict = penumbraConflictHints.TryGetValue(subIndex, out var penumbraConflictHint);
            var crossRuleConflict = DoMatchedRulesConflictOnMod(group.PenumbraCollection, modDirectory);
            if (!withinGroupConflict && crossRuleConflict)
                penumbraConflictHint = Localization.RulePenumbraMatchedRuleConflictHint;
            var hasPenumbraConflict = withinGroupConflict || crossRuleConflict;

            var rowStartPos = ImGui.GetCursorScreenPos();
            var contentWidth = ImGui.GetContentRegionAvail().X;

            ImGui.AlignTextToFramePadding();

            if (ImGui.Button($"{Localization.RuleDragHandle}##SubDrag{subHostRuleIndex}_{subIndex}"))
            {
            }

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Localization.DragToReorderActions);

            if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
            {
                if (!_actionDragSourceIndex.HasValue)
                {
                    _actionDragSourceRuleIndex = subHostRuleIndex;
                    _actionDragSourceIndex = subIndex;
                }
            }

            UpdateActionDragTarget(subHostRuleIndex, subIndex);

            if (_actionDragSourceRuleIndex == subHostRuleIndex
                && _actionDragSourceIndex.HasValue
                && _actionDragTargetIndex == subIndex
                && _actionDragSourceIndex != subIndex)
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

            var expanded = !sub.IsCollapsed;
            if (ImGui.ArrowButton($"##SubCollapse{subHostRuleIndex}{subIndex}", expanded ? ImGuiDir.Down : ImGuiDir.Right))
            {
                sub.IsCollapsed = !sub.IsCollapsed;
                SaveConfig();
            }

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Localization.RuleActionCollapseTooltip);

            ImGui.SameLine();

            if (hasGlamourerTakeover)
                ImGui.TextColored(PenumbraGlamourerTakeoverWarningColor, Localization.RuleActionLabel(subIndex + 1));
            else if (hasPenumbraConflict)
                ImGui.TextColored(RulePenumbraConflictWarningColor, Localization.RuleActionLabel(subIndex + 1));
            else
                ImGui.TextDisabled(Localization.RuleActionLabel(subIndex + 1));

            if (sub.IsCollapsed)
            {
                ImGui.SameLine();
                var summary = GetPenumbraSubActionSummary(sub);
                if (hasGlamourerTakeover)
                    ImGui.TextColored(PenumbraGlamourerTakeoverWarningColor, summary);
                else if (hasPenumbraConflict)
                    ImGui.TextColored(RulePenumbraConflictWarningColor, summary);
                else
                    ImGui.TextColored(new Vector4(0.9f, 0.9f, 0.9f, 1.0f), summary);
            }

            ImGui.SameLine();
            if (ImGui.SmallButton($"{Localization.DeleteRuleAction}##SubDel{subHostRuleIndex}{subIndex}"))
            {
                actionToDeleteRuleIndex = subHostRuleIndex;
                actionToDeleteIndex = subIndex;
                ImGui.OpenPopup("ConfirmDeleteAction");
            }

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Localization.DeleteThisAction);

            DrawActionDeleteConfirmation(out var actionDeleted);
            if (actionDeleted)
            {
                deleted = true;
                return;
            }

            if (sub.IsCollapsed)
            {
                DrawRuleActionWarningBackground(
                    rowStartPos,
                    contentWidth,
                    false,
                    hasGlamourerTakeover,
                    hasPenumbraConflict);
                return;
            }

            ImGui.Indent();

            if (!_penumbraInterop.IsIpcAvailable())
            {
                ImGui.TextDisabled(Localization.PenumbraListUnavailable);
                ImGui.Unindent();
                return;
            }

            if (hasGlamourerTakeover)
            {
                ImGui.TextColored(PenumbraGlamourerTakeoverWarningColor, $"! {Localization.PenumbraModGlamourerTakeoverWarning}");
                ImGui.Spacing();
            }

            if (hasPenumbraConflict)
            {
                ImGui.TextColored(RulePenumbraConflictWarningColor, $"! {penumbraConflictHint}");
                ImGui.Spacing();
            }

            var idSuffix = $"Sub{subHostRuleIndex}_{subIndex}";

            ImGui.Text(Localization.PenumbraActionTypeLabel);
            ImGui.SetNextItemWidth(-1);
            var penumbraActionTypes = new[]
            {
                (Localization.PenumbraActionSetModOption, PenumbraActionKind.SetModOption),
                (Localization.PenumbraActionEnableMod, PenumbraActionKind.EnableMod),
                (Localization.PenumbraActionDisableMod, PenumbraActionKind.DisableMod),
            };
            var currentActionTypeDisplay = penumbraActionTypes
                .FirstOrDefault(t => t.Item2 == sub.PenumbraActionKind).Item1
                ?? penumbraActionTypes[0].Item1;
            if (ImGui.BeginCombo($"##PenumbraActionKind{idSuffix}", currentActionTypeDisplay))
            {
                foreach (var (display, kind) in penumbraActionTypes)
                {
                    if (ImGui.Selectable(display, sub.PenumbraActionKind == kind))
                    {
                        sub.PenumbraActionKind = kind;
                        SaveConfig();
                    }
                }
                ImGui.EndCombo();
            }

            DrawPenumbraGlamourerTakeoverControls(flatAction, idSuffix, sub);
            sub.PenumbraOverwriteGlamourer = flatAction.PenumbraOverwriteGlamourer;

            if (sub.PenumbraActionKind == PenumbraActionKind.SetModOption)
            {
                ImGui.SetNextItemWidth(-1);
                DrawPenumbraOptionGroupSelector(idSuffix, flatAction, modDirectory, sub);

                var groupType = _penumbraInterop.GetOptionGroupType(modDirectory, sub.PenumbraOption ?? "");
                if (PenumbraInterop.UsesBoolOptionValue(groupType))
                    DrawPenumbraMultiToggleList(idSuffix, flatAction, modDirectory, sub);
                else
                {
                    ImGui.SetNextItemWidth(-1);
                    DrawPenumbraOptionCombo(idSuffix, flatAction, modDirectory, sub);
                }

                ApplyFlatPenumbraSubActionToSub(sub, flatAction);
            }

            ImGui.Unindent();

            DrawRuleActionWarningBackground(
                rowStartPos,
                contentWidth,
                false,
                hasGlamourerTakeover,
                hasPenumbraConflict);
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
            var canReorder = canDelete;
            
            var hasFeet = action.Type == ActionType.Glamourer && DoesGlamourerDesignHaveFeet(action.GlamourerDesign);
            var hasPenumbraConflict = penumbraConflictHints.TryGetValue(actionIndex, out var penumbraConflictHint);
            var hasGlamourerTakeover = false;

            GlamourerConflictKind? glamourerConflict = action.Type == ActionType.Glamourer
                && glamourerConflicts.TryGetValue((ruleIndex, actionIndex), out var glamConflictKind)
                ? glamConflictKind
                : null;
            var glamourerConflictColor = glamourerConflict switch
            {
                GlamourerConflictKind.Unresolved => GlamourerSlotConflictUnresolvedColor,
                GlamourerConflictKind.Resolved => GlamourerSlotConflictResolvedColor,
                _ => (Vector4?)null,
            };
            var glamourerConflictHint = glamourerConflict switch
            {
                GlamourerConflictKind.Unresolved => Localization.GlamourerSlotConflictUnresolvedHint,
                GlamourerConflictKind.Resolved => Localization.GlamourerSlotConflictResolvedHint,
                _ => "",
            };
            
            // 记录起始位置（用于绘制背景）
            var actionStartPos = ImGui.GetCursorScreenPos();
            var contentWidth = ImGui.GetContentRegionAvail().X;
            
            ImGui.AlignTextToFramePadding();

            if (canReorder)
            {
                if (ImGui.Button($"{Localization.RuleDragHandle}##ActionDrag{ruleIndex}_{actionIndex}"))
                {
                }

                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(Localization.DragToReorderActions);

                if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                {
                    if (!_actionDragSourceIndex.HasValue)
                    {
                        _actionDragSourceRuleIndex = ruleIndex;
                        _actionDragSourceIndex = actionIndex;
                    }
                }

                UpdateActionDragTarget(ruleIndex, actionIndex);

                if (_actionDragSourceRuleIndex == ruleIndex
                    && _actionDragSourceIndex.HasValue
                    && _actionDragTargetIndex == actionIndex
                    && _actionDragSourceIndex != actionIndex)
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
            }

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
            
            // 脚部装备红色 > Glamourer 槽位未解决冲突红色 > Glamourer 接管黄色 > Penumbra 规则内冲突橙色 > Glamourer 槽位已解决冲突浅蓝 > 默认
            if (hasFeet)
                ImGui.TextColored(new Vector4(1.0f, 0.3f, 0.3f, 1.0f), Localization.RuleActionLabel(actionIndex + 1));
            else if (glamourerConflict == GlamourerConflictKind.Unresolved)
                ImGui.TextColored(GlamourerSlotConflictUnresolvedColor, Localization.RuleActionLabel(actionIndex + 1));
            else if (hasGlamourerTakeover)
                ImGui.TextColored(PenumbraGlamourerTakeoverWarningColor, Localization.RuleActionLabel(actionIndex + 1));
            else if (hasPenumbraConflict)
                ImGui.TextColored(RulePenumbraConflictWarningColor, Localization.RuleActionLabel(actionIndex + 1));
            else if (glamourerConflict == GlamourerConflictKind.Resolved)
                ImGui.TextColored(GlamourerSlotConflictResolvedColor, Localization.RuleActionLabel(actionIndex + 1));
            else
                ImGui.TextDisabled(Localization.RuleActionLabel(actionIndex + 1));

            if (hasGlamourerTakeover && ImGui.IsItemHovered())
                ImGui.SetTooltip(Localization.PenumbraModGlamourerTakeoverWarning);
            else if (hasPenumbraConflict && ImGui.IsItemHovered())
                ImGui.SetTooltip(penumbraConflictHint);
            else if (glamourerConflict != null && ImGui.IsItemHovered())
                ImGui.SetTooltip(glamourerConflictHint);
            
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
                    ActionType.SoundMixer => ("[S]", new Vector4(0.7f, 0.85f, 1.0f, 1.0f), Localization.ActionTypeLabel(ActionType.SoundMixer)),
                    _ => ("[?]", new Vector4(0.7f, 0.7f, 0.7f, 1.0f), "Unknown")
                };
                
                if (hasFeet)
                    typeColor = new Vector4(1.0f, 0.3f, 0.3f, 1.0f);
                else if (glamourerConflict == GlamourerConflictKind.Unresolved)
                    typeColor = GlamourerSlotConflictUnresolvedColor;
                else if (hasGlamourerTakeover)
                    typeColor = PenumbraGlamourerTakeoverWarningColor;
                else if (hasPenumbraConflict)
                    typeColor = RulePenumbraConflictWarningColor;
                else if (glamourerConflict == GlamourerConflictKind.Resolved)
                    typeColor = GlamourerSlotConflictResolvedColor;
                
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
                else if (glamourerConflict == GlamourerConflictKind.Unresolved)
                    ImGui.TextColored(GlamourerSlotConflictUnresolvedColor, summary);
                else if (hasGlamourerTakeover)
                    ImGui.TextColored(PenumbraGlamourerTakeoverWarningColor, summary);
                else if (hasPenumbraConflict)
                    ImGui.TextColored(RulePenumbraConflictWarningColor, summary);
                else if (glamourerConflict == GlamourerConflictKind.Resolved)
                    ImGui.TextColored(GlamourerSlotConflictResolvedColor, summary);
                else
                    ImGui.TextColored(new Vector4(0.9f, 0.9f, 0.9f, 1.0f), summary);

                if (hasGlamourerTakeover && ImGui.IsItemHovered())
                    ImGui.SetTooltip(Localization.PenumbraModGlamourerTakeoverWarning);
                else if (hasPenumbraConflict && ImGui.IsItemHovered())
                    ImGui.SetTooltip(penumbraConflictHint);
                else if (glamourerConflict != null && ImGui.IsItemHovered())
                    ImGui.SetTooltip(glamourerConflictHint);
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
                    ImGui.SetTooltip(Localization.DeleteThisAction);
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
                    hasGlamourerTakeover,
                    hasPenumbraConflict,
                    glamourerConflict);
                return;
            }

            ImGui.Indent();

            if (hasGlamourerTakeover)
            {
                ImGui.TextColored(PenumbraGlamourerTakeoverWarningColor, $"! {Localization.PenumbraModGlamourerTakeoverWarning}");
                ImGui.Spacing();
            }

            if (hasPenumbraConflict)
            {
                ImGui.TextColored(RulePenumbraConflictWarningColor, $"! {penumbraConflictHint}");
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

                    // 优先级（数值越大越优先：越后应用、冲突装备槽位胜出）
                    ImGui.SetNextItemWidth(120);
                    var glamPriority = action.GlamourerPriority;
                    if (ImGui.InputInt($"{Localization.GlamourerPriorityLabel}##GlamPrio{ruleIndex}_{actionIndex}", ref glamPriority))
                    {
                        action.GlamourerPriority = glamPriority;
                        SaveConfig();
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip(Localization.GlamourerPriorityTooltip);

                    // 装备槽位冲突提示
                    if (glamourerConflict == GlamourerConflictKind.Unresolved)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, GlamourerSlotConflictUnresolvedColor);
                        ImGui.TextWrapped($"! {Localization.GlamourerSlotConflictUnresolvedHint}");
                        ImGui.PopStyleColor();
                    }
                    else if (glamourerConflict == GlamourerConflictKind.Resolved)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, GlamourerSlotConflictResolvedColor);
                        ImGui.TextWrapped($"ℹ {Localization.GlamourerSlotConflictResolvedHint}");
                        ImGui.PopStyleColor();
                    }
                    
                    // 如果包含脚部装备，显示警告
                    if (hasFeet)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.5f, 0.2f, 1.0f));
                        ImGui.TextWrapped(Localization.GlamourerDesignFeetConflictWarning);
                        ImGui.PopStyleColor();
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

                case ActionType.SoundMixer:
                    DrawSoundMixerActionEditor(ruleIndex, actionIndex, action, idSuffix);
                    break;
            }
            
            ImGui.Unindent();
            
            DrawRuleActionWarningBackground(
                actionStartPos,
                contentWidth,
                hasFeet,
                hasGlamourerTakeover,
                hasPenumbraConflict,
                glamourerConflict);
        }

        private static void DrawRuleActionWarningBackground(
            Vector2 actionStartPos,
            float contentWidth,
            bool hasFeetWarning,
            bool hasGlamourerTakeover,
            bool hasPenumbraConflict,
            GlamourerConflictKind? glamourerConflict = null)
        {
            if (!hasFeetWarning && !hasGlamourerTakeover && !hasPenumbraConflict && glamourerConflict == null)
                return;

            var actionEndPos = ImGui.GetCursorScreenPos();
            var drawList = ImGui.GetWindowDrawList();
            var fillColor = hasFeetWarning
                ? new Vector4(0.4f, 0.0f, 0.0f, 0.3f)
                : glamourerConflict == GlamourerConflictKind.Unresolved
                    ? new Vector4(0.4f, 0.0f, 0.0f, 0.3f)
                    : hasGlamourerTakeover
                        ? new Vector4(0.35f, 0.3f, 0.0f, 0.28f)
                        : hasPenumbraConflict
                            ? new Vector4(0.45f, 0.2f, 0.0f, 0.28f)
                            : new Vector4(0.1f, 0.25f, 0.4f, 0.28f); // Glamourer 已解决冲突：浅蓝
            drawList.ChannelsSplit(2);
            drawList.ChannelsSetCurrent(0);
            drawList.AddRectFilled(
                actionStartPos,
                new Vector2(actionStartPos.X + contentWidth, actionEndPos.Y),
                ImGui.GetColorU32(fillColor),
                3.0f);
            if (hasGlamourerTakeover)
            {
                drawList.AddRect(
                    actionStartPos,
                    new Vector2(actionStartPos.X + contentWidth, actionEndPos.Y),
                    ImGui.GetColorU32(PenumbraGlamourerTakeoverWarningColor),
                    3.0f,
                    ImDrawFlags.None,
                    1.5f);
            }
            else if (hasPenumbraConflict)
            {
                drawList.AddRect(
                    actionStartPos,
                    new Vector2(actionStartPos.X + contentWidth, actionEndPos.Y),
                    ImGui.GetColorU32(RulePenumbraConflictWarningColor),
                    3.0f,
                    ImDrawFlags.None,
                    1.5f);
            }
            else if (!hasFeetWarning && glamourerConflict != null)
            {
                drawList.AddRect(
                    actionStartPos,
                    new Vector2(actionStartPos.X + contentWidth, actionEndPos.Y),
                    ImGui.GetColorU32(glamourerConflict == GlamourerConflictKind.Unresolved
                        ? GlamourerSlotConflictUnresolvedColor
                        : GlamourerSlotConflictResolvedColor),
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
                ImGui.TextDisabled(Localization.HonorificUnavailable);
                return;
            }
            
            var localPlayer = ObjectTable.LocalPlayer;
            if (localPlayer == null)
            {
                ImGui.TextDisabled(Localization.PlayerNotLoggedIn);
                return;
            }
            
            // 获取称号列表
            var titleOptions = _honorificInterop.GetTitleOptions(localPlayer, force: false);
            
            ImGui.Text(Localization.HonorificTitleLabel);
            
            // 当前选择的称号
            var currentTitle = string.IsNullOrWhiteSpace(action.HonorificTitleJson) 
                ? Localization.NoneSelected
                : ExtractTitleFromJson(action.HonorificTitleJson);
            
            ImGui.SetNextItemWidth(-1);
            if (ImGui.BeginCombo($"##HonorificTitle{idSuffix}", currentTitle))
            {
                // "无" 选项
                if (ImGui.Selectable(Localization.NoneSelected, string.IsNullOrWhiteSpace(action.HonorificTitleJson)))
                {
                    action.HonorificTitleJson = "";
                    SaveConfig();
                }
                
                // 称号列表
                if (titleOptions.Count == 0)
                {
                    ImGui.TextDisabled(Localization.NoTitlesAvailable);
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
            if (ImGui.SmallButton($"{Localization.Refresh}##RefreshHonorific{idSuffix}"))
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

        private void DrawSoundMixerActionEditor(int ruleIndex, int actionIndex, HeelsRuleAction action, string idSuffix)
        {
            if (!isSoundMixerIpcReady)
            {
                ImGui.TextDisabled(Localization.SoundMixerUnavailable);
                return;
            }

            ImGui.Text(Localization.SoundMixerActionKindHeader);
            ImGui.SetNextItemWidth(-1);
            var kindLabel = Localization.SoundMixerActionKindLabel(action.SoundMixerActionKind);
            if (ImGui.BeginCombo($"##SoundMixerKind{idSuffix}", kindLabel))
            {
                foreach (SoundMixerActionKind kind in Enum.GetValues<SoundMixerActionKind>())
                {
                    if (ImGui.Selectable(Localization.SoundMixerActionKindLabel(kind), action.SoundMixerActionKind == kind))
                    {
                        action.SoundMixerActionKind = kind;
                        SaveConfig();
                    }
                }

                ImGui.EndCombo();
            }

            if (action.SoundMixerActionKind == SoundMixerActionKind.TemporaryPreset)
            {
                var presets = _soundMixerInterop.GetPresetNames(force: false);
                var currentPreset = string.IsNullOrWhiteSpace(action.SoundMixerPresetName)
                    ? Localization.NoneSelected
                    : action.SoundMixerPresetName;

                ImGui.Text(Localization.SoundMixerPresetLabel);
                ImGui.SetNextItemWidth(-1);
                if (ImGui.BeginCombo($"##SoundMixerPreset{idSuffix}", currentPreset))
                {
                    if (ImGui.Selectable(Localization.NoneSelected, string.IsNullOrWhiteSpace(action.SoundMixerPresetName)))
                    {
                        action.SoundMixerPresetName = "";
                        SaveConfig();
                    }

                    foreach (var preset in presets)
                    {
                        if (ImGui.Selectable(preset, string.Equals(action.SoundMixerPresetName, preset, StringComparison.OrdinalIgnoreCase)))
                        {
                            action.SoundMixerPresetName = preset;
                            SaveConfig();
                        }
                    }

                    ImGui.EndCombo();
                }
            }
            else
            {
                var groups = _soundMixerInterop.GetGroups(force: false);
                var currentGroup = string.IsNullOrWhiteSpace(action.SoundMixerGroupName)
                    ? Localization.NoneSelected
                    : action.SoundMixerGroupName;

                ImGui.Text(Localization.SoundMixerGroupLabel);
                ImGui.SetNextItemWidth(-1);
                if (ImGui.BeginCombo($"##SoundMixerGroup{idSuffix}", currentGroup))
                {
                    if (ImGui.Selectable(Localization.NoneSelected, string.IsNullOrWhiteSpace(action.SoundMixerGroupName)))
                    {
                        action.SoundMixerGroupName = "";
                        SaveConfig();
                    }

                    foreach (var group in groups)
                    {
                        var label = string.IsNullOrWhiteSpace(group.Name) ? group.Id : group.Name;
                        if (ImGui.Selectable(label, string.Equals(action.SoundMixerGroupName, label, StringComparison.OrdinalIgnoreCase)
                                || string.Equals(action.SoundMixerGroupName, group.Id, StringComparison.OrdinalIgnoreCase)))
                        {
                            action.SoundMixerGroupName = label;
                            if (action.SoundMixerGroupVolume <= 0f)
                                action.SoundMixerGroupVolume = group.EffectiveVolume;
                            SaveConfig();
                        }
                    }

                    ImGui.EndCombo();
                }

                var volumePercent = action.SoundMixerGroupVolume * 100f;
                ImGui.SetNextItemWidth(-1);
                if (SliderFloatWithManualInput(
                        Localization.SoundMixerVolumeLabel,
                        ref volumePercent,
                        SoundMixerInterop.VolumePercentMin,
                        SoundMixerInterop.VolumePercentMax,
                        "%.0f%%",
                        $"sound_mixer_volume_{idSuffix}"))
                {
                    action.SoundMixerGroupVolume = volumePercent / 100f;
                    SaveConfig();
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(Localization.SoundMixerVolumeRangeTooltip);
            }

            var priority = action.SoundMixerPriority;
            ImGui.Text(Localization.SoundMixerPriorityLabel);
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputInt($"##SoundMixerPriority{idSuffix}", ref priority))
            {
                action.SoundMixerPriority = priority;
                SaveConfig();
            }

            ImGui.SameLine();
            if (ImGui.SmallButton($"{Localization.Refresh}##RefreshSoundMixer{idSuffix}"))
                _soundMixerInterop.RefreshData(force: true);

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Localization.SoundMixerTemporaryHint);
        }

        private static bool MoodleSelectionExists(string? guid, bool isPreset, IReadOnlyList<MoodleListItem> items)
        {
            if (string.IsNullOrWhiteSpace(guid))
                return true;

            if (!Guid.TryParse(guid, out var id) || id == Guid.Empty)
                return false;

            return items.Any(m => m.Id == id && m.IsPreset == isPreset);
        }

        private bool PruneStaleMoodleSelection(HeelsRuleAction action, IReadOnlyList<MoodleListItem> items)
        {
            if (MoodleSelectionExists(action.MoodleGuid, action.MoodleIsPreset, items))
                return false;

            action.MoodleGuid = "";
            action.MoodleIsPreset = false;
            return true;
        }

        private IReadOnlyList<MoodleListItem> RefreshMoodleItemsAndPruneSelection(HeelsRuleAction action)
        {
            var moodleItems = _moodlesInterop.GetItems(force: true);
            if (PruneStaleMoodleSelection(action, moodleItems))
                PersistRuleSetAfterActionMutation();

            return moodleItems;
        }

        private void DrawMoodlesActionEditor(int ruleIndex, int actionIndex, HeelsRuleAction action, string idSuffix)
        {
            if (!isMoodlesIpcReady)
            {
                ImGui.TextDisabled(Localization.MoodlesUnavailable);
                return;
            }
            
            var moodleItems = _moodlesInterop.GetItems(force: false);
            
            ImGui.Text(Localization.MoodlesStatusPresetLabel);
            
            // 当前选择
            var currentDisplay = string.IsNullOrWhiteSpace(action.MoodleGuid)
                ? Localization.NoneSelected
                : GetMoodleDisplayName(action.MoodleGuid, action.MoodleIsPreset, moodleItems);
            
            ImGui.SetNextItemWidth(-1);
            if (ImGui.BeginCombo($"##MoodleSelect{idSuffix}", currentDisplay))
            {
                if (ImGui.IsWindowAppearing())
                    moodleItems = RefreshMoodleItemsAndPruneSelection(action);

                // "无" 选项
                if (ImGui.Selectable(Localization.NoneSelected, string.IsNullOrWhiteSpace(action.MoodleGuid)))
                {
                    action.MoodleGuid = "";
                    action.MoodleIsPreset = false;
                    SaveConfig();
                }
                
                // Moodles 列表
                if (moodleItems.Count == 0)
                {
                    ImGui.TextDisabled(Localization.NoMoodlesAvailable);
                }
                else
                {
                    var presets = moodleItems.Where(m => m.IsPreset).ToList();
                    var statuses = moodleItems.Where(m => !m.IsPreset).ToList();

                    if (presets.Any())
                    {
                        ImGui.TextDisabled(Localization.MoodlesPresetsSection);
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

                    if (statuses.Any())
                    {
                        if (presets.Any())
                            ImGui.Spacing();
                        ImGui.TextDisabled(Localization.MoodlesStatusSection);
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
                }
                
                ImGui.EndCombo();
            }
            
            // 刷新按钮
            ImGui.SameLine();
            if (ImGui.SmallButton($"{Localization.Refresh}##RefreshMoodles{idSuffix}"))
                RefreshMoodleItemsAndPruneSelection(action);
        }

        private string GetMoodleDisplayName(string guid, bool isPreset, IReadOnlyList<MoodleListItem> items)
        {
            if (Guid.TryParse(guid, out var id))
            {
                var item = items.FirstOrDefault(m => m.Id == id && m.IsPreset == isPreset);
                if (item != null)
                {
                    var typeLabel = isPreset ? Localization.MoodleTypePreset : Localization.MoodleTypeStatus;
                    return $"[{typeLabel}] {item.Title}";
                }
            }
            
            var type = isPreset ? "Preset" : "Status";
            return $"[{type}] {guid.Substring(0, Math.Min(8, guid.Length))}";
        }

        private void DrawPenumbraGlamourerTakeoverControls(
            HeelsRuleAction action,
            string idSuffix,
            PenumbraSubAction? syncSub = null)
        {
            // 仅外部 Glamourer 接管才需要授权覆盖；自己写入或本插件设计触发的接管不再提示。
            if (!IsPenumbraActionUnderGlamourerTakeover(action)
                || IsPenumbraModSelfManaged(action.PenumbraModName ?? ""))
                return;

            ImGui.PushStyleColor(ImGuiCol.Text, PenumbraGlamourerTakeoverWarningColor);
            ImGui.TextWrapped($"! {Localization.PenumbraModGlamourerTakeoverWarning}");
            ImGui.PopStyleColor();

            if (Configuration.AutoOverwriteGlamourerPenumbra)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.75f, 0.75f, 0.75f, 1.0f));
                ImGui.TextWrapped(Localization.PenumbraGlamourerAutoOverwriteHint);
                ImGui.PopStyleColor();
                return;
            }

            var overwriteGlamourer = action.PenumbraOverwriteGlamourer;
            if (ImGui.Checkbox($"{Localization.PenumbraOverwriteGlamourerAction}##OverwriteGlm{idSuffix}", ref overwriteGlamourer))
            {
                action.PenumbraOverwriteGlamourer = overwriteGlamourer;
                SavePenumbraSubActionEdit(syncSub, action);
            }

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Localization.PenumbraOverwriteGlamourerActionTooltip);
        }

        private static readonly Vector4 ActionTypeAvailableTextColor = new(1f, 1f, 1f, 1f);

        private bool IsActionTypeAvailable(ActionType actionType) => actionType switch
        {
            ActionType.Glamourer => isGlamourerAvailable,
            ActionType.Penumbra => isPenumbraIpcReady,
            ActionType.Honorific => isHonorificIpcReady,
            ActionType.Moodles => isMoodlesIpcReady,
            ActionType.SoundMixer => isSoundMixerIpcReady,
            _ => false,
        };

        private Vector4 GetActionTypeTextColor(ActionType actionType) =>
            IsActionTypeAvailable(actionType)
                ? ActionTypeAvailableTextColor
                : ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled];

        private void DrawActionTypeSelectable(HeelsRuleAction action, ActionType type)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, GetActionTypeTextColor(type));
            if (ImGui.Selectable(Localization.ActionTypeLabel(type), action.Type == type))
            {
                action.Type = type;
                if (type == ActionType.Penumbra)
                    action.PenumbraGroup ??= CreateDefaultPenumbraActionGroup();
                SaveConfig();
            }
            ImGui.PopStyleColor();
        }

        private void DrawActionTypeSelector(int ruleIndex, int actionIndex, HeelsRuleAction action)
        {
            ImGui.SetNextItemWidth(150);
            var currentTypeLabel = Localization.ActionTypeLabel(action.Type);

            ImGui.PushStyleColor(ImGuiCol.Text, GetActionTypeTextColor(action.Type));
            var comboOpen = ImGui.BeginCombo($"##ActionType{ruleIndex}_{actionIndex}", currentTypeLabel);
            ImGui.PopStyleColor();

            if (comboOpen)
            {
                DrawActionTypeSelectable(action, ActionType.Glamourer);
                DrawActionTypeSelectable(action, ActionType.Penumbra);
                DrawActionTypeSelectable(action, ActionType.Honorific);
                DrawActionTypeSelectable(action, ActionType.Moodles);
                DrawActionTypeSelectable(action, ActionType.SoundMixer);
                ImGui.EndCombo();
            }

            ImGui.SameLine();
            ImGui.TextDisabled(Localization.ActionTypeText);
        }

        private void DrawCustomizePlusProfileSelector(string idSuffix, HeelsRuleAction action)
        {
            // Customize+ 支持已移除
            ImGui.TextColored(new Vector4(1.0f, 0.5f, 0.3f, 1.0f), 
                Localization.CustomizePlusSupportRemoved);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(Localization.CustomizePlusRemovedTooltip);
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
                    var penumbraGroup = EnsurePenumbraGroupOnAction(action);
                    var modLabel = GetPenumbraGroupModLabel(penumbraGroup);
                    return Localization.PenumbraActionGroupSummary(
                        penumbraGroup.PenumbraCollection,
                        modLabel,
                        penumbraGroup.SubActions.Count);
                    
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

                case ActionType.SoundMixer:
                    if (action.SoundMixerActionKind == SoundMixerActionKind.TemporaryPreset
                        && !string.IsNullOrWhiteSpace(action.SoundMixerPresetName))
                    {
                        return $"[Preset → {action.SoundMixerPresetName}]";
                    }

                    if (action.SoundMixerActionKind == SoundMixerActionKind.TemporaryGroupVolume
                        && !string.IsNullOrWhiteSpace(action.SoundMixerGroupName))
                    {
                        return $"[{action.SoundMixerGroupName} → {action.SoundMixerGroupVolume * 100f:F0}%]";
                    }
                    break;
            }
            
            return "[未配置]";
        }

        private string GetPenumbraSubActionSummary(PenumbraSubAction sub)
        {
            if (sub.PenumbraActionKind == PenumbraActionKind.EnableMod)
                return $"[{Localization.PenumbraEnableShort}]";
            if (sub.PenumbraActionKind == PenumbraActionKind.DisableMod)
                return $"[{Localization.PenumbraDisableShort}]";
            if (!string.IsNullOrWhiteSpace(sub.PenumbraOptionName))
                return $"[{sub.PenumbraOptionName}]";
            if (sub.PenumbraMultiToggleStates.Any(kv => kv.Value))
            {
                var enabled = string.Join(", ",
                    sub.PenumbraMultiToggleStates.Where(kv => kv.Value).Select(kv => kv.Key));
                if (!string.IsNullOrWhiteSpace(enabled))
                    return $"[{enabled}]";
            }
            if (!string.IsNullOrWhiteSpace(sub.PenumbraOption))
                return $"[{sub.PenumbraOption}]";
            return "[未配置]";
        }


        private void DrawPenumbraCollectionSelector(
            string idSuffix,
            PenumbraActionGroup group,
            PenumbraApplyLayerConfig? dedupLayer)
        {
            var collections = _penumbraInterop.GetCollectionNames();
            var selected = group.PenumbraCollection ?? "";
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
                            if (!string.Equals(name, selected, StringComparison.OrdinalIgnoreCase))
                            {
                                var previousCollection = group.PenumbraCollection ?? "Default";
                                var previousMod = group.PenumbraModName ?? "";
                                group.PenumbraCollection = name;
                                HandlePenumbraGroupTargetChanged(
                                    group,
                                    previousCollection,
                                    previousMod,
                                    modChanged: false,
                                    dedupLayer);
                            }
                        }
                    }
                }

                ImGui.EndCombo();
            }

            ImGui.SameLine();
            ImGui.TextDisabled(Localization.Collection);
        }

        private void DrawPenumbraModSelector(
            string idSuffix,
            PenumbraActionGroup group,
            PenumbraApplyLayerConfig? dedupLayer)
        {
            var mods = _penumbraInterop.GetMods();
            var selectedDirectory = group.PenumbraModName ?? "";
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
                            var previousCollection = group.PenumbraCollection ?? "Default";
                            var previousMod = group.PenumbraModName ?? "";
                            group.PenumbraModName = "";
                            penumbraModSearchFilter = "";
                            HandlePenumbraGroupTargetChanged(
                                group,
                                previousCollection,
                                previousMod,
                                modChanged: true,
                                dedupLayer);
                        }

                        foreach (var mod in filteredMods)
                        {
                            var label = Localization.PenumbraModLabel(mod.DisplayName, mod.Directory);
                            if (ImGui.Selectable(label, string.Equals(mod.Directory, selectedDirectory, StringComparison.OrdinalIgnoreCase)))
                            {
                                if (!string.Equals(mod.Directory, selectedDirectory, StringComparison.OrdinalIgnoreCase))
                                {
                                    var previousCollection = group.PenumbraCollection ?? "Default";
                                    var previousMod = group.PenumbraModName ?? "";
                                    group.PenumbraModName = mod.Directory;
                                    penumbraModSearchFilter = "";
                                    _penumbraInterop.InvalidateModSettingsCache();
                                    HandlePenumbraGroupTargetChanged(
                                        group,
                                        previousCollection,
                                        previousMod,
                                        modChanged: true,
                                        dedupLayer);
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

        private void DrawPenumbraOptionGroupSelector(
            string idSuffix,
            HeelsRuleAction action,
            string? penumbraModDirectoryOverride = null,
            PenumbraSubAction? syncSub = null)
        {
            var modDirectory = penumbraModDirectoryOverride ?? action.PenumbraModName ?? "";
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
                                SavePenumbraSubActionEdit(syncSub, action);
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

        private void DrawPenumbraOptionCombo(
            string idSuffix,
            HeelsRuleAction action,
            string? penumbraModDirectoryOverride = null,
            PenumbraSubAction? syncSub = null)
        {
            var modDirectory = penumbraModDirectoryOverride ?? action.PenumbraModName ?? "";
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
                        SavePenumbraSubActionEdit(syncSub, action);
                    }

                    foreach (var option in options)
                    {
                        if (ImGui.Selectable(option, string.Equals(option, optionName, StringComparison.OrdinalIgnoreCase)))
                        {
                            action.PenumbraOptionName = option;
                            SavePenumbraSubActionEdit(syncSub, action);
                        }
                    }
                }

                ImGui.EndCombo();
            }

            ImGui.SameLine();
            ImGui.TextDisabled(Localization.OptionName);
        }

        private void DrawPenumbraMultiToggleList(
            string idSuffix,
            HeelsRuleAction action,
            string? penumbraModDirectoryOverride = null,
            PenumbraSubAction? syncSub = null)
        {
            var modDirectory = penumbraModDirectoryOverride ?? action.PenumbraModName ?? "";
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
                SavePenumbraSubActionEdit(syncSub, action);

            ImGui.TextDisabled(Localization.PenumbraMultiToggleListLabel);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Localization.PenumbraMultiToggleListHint);

            ImGui.Indent();
            foreach (var option in options)
            {
                DrawPenumbraMultiToggleOnOffCombo(
                    option,
                    $"##PenumbraMultiToggle{idSuffix}{option}",
                    action.PenumbraMultiToggleStates,
                    option,
                    syncSub,
                    action);
            }

            ImGui.Unindent();
        }

        private void DrawPenumbraMultiToggleOnOffCombo(
            string optionLabel,
            string comboId,
            Dictionary<string, bool> toggleStates,
            string optionKey,
            PenumbraSubAction? syncSub = null,
            HeelsRuleAction? syncFlat = null)
        {
            var enabled = toggleStates.TryGetValue(optionKey, out var state) && state;
            var preview = enabled ? Localization.PenumbraOptionEnabled : Localization.PenumbraOptionDisabled;

            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(optionLabel);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(90);
            if (ImGui.BeginCombo(comboId, preview))
            {
                if (ImGui.Selectable(Localization.PenumbraOptionEnabled, enabled))
                {
                    toggleStates[optionKey] = true;
                    if (syncFlat != null)
                        SavePenumbraSubActionEdit(syncSub, syncFlat);
                    else
                        SaveConfig();
                }

                if (ImGui.Selectable(Localization.PenumbraOptionDisabled, !enabled))
                {
                    toggleStates[optionKey] = false;
                    if (syncFlat != null)
                        SavePenumbraSubActionEdit(syncSub, syncFlat);
                    else
                        SaveConfig();
                }

                ImGui.EndCombo();
            }

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(Localization.PenumbraOptionEnabledHint);
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
        
        private void DrawDrawObjectEquipmentStatus()
        {
            ImGui.TextWrapped(Localization.DebugDrawObjectNote);
            ImGui.Spacing();

            if (ImGui.Button(Localization.DebugRebuildModelLookup))
                ItemModelNameLookup.Rebuild(DataManager);

            ImGui.SameLine();
            ImGui.TextDisabled(Localization.DebugDrawObjectLookupCount(ItemModelNameLookup.CountEntries(DataManager)));

            ImGui.Separator();

            if (!DrawObjectEquipmentReader.TryReadLocalPlayerSlots(
                    ObjectTable?.LocalPlayer,
                    out var slots,
                    out var error))
            {
                ImGui.TextColored(new Vector4(1.0f, 0.55f, 0.25f, 1.0f), error ?? Localization.EquipmentStatusUnknown);
                return;
            }

            _penumbraInterop.EnsureModFilesystemPathCache();

            if (ImGui.BeginTable(
                    "DrawObjectEquipmentTable",
                    5,
                    ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
            {
                ImGui.TableSetupColumn(Localization.DebugDrawObjectTableSlot, ImGuiTableColumnFlags.WidthFixed, 56f);
                ImGui.TableSetupColumn(Localization.DebugDrawObjectEquipSlotCategoryId, ImGuiTableColumnFlags.WidthFixed, 44f);
                ImGui.TableSetupColumn(Localization.DebugDrawObjectModelId, ImGuiTableColumnFlags.WidthFixed, 56f);
                ImGui.TableSetupColumn(Localization.DebugDrawObjectItemName, ImGuiTableColumnFlags.WidthStretch, 3.0f);
                ImGui.TableSetupColumn(Localization.DebugDrawObjectModelPath, ImGuiTableColumnFlags.WidthStretch, 1.0f);
                ImGui.TableHeadersRow();

                foreach (var slotInfo in slots)
                {
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    ImGui.Text(DrawObjectEquipmentReader.GetSlotLabel(slotInfo.Slot));

                    ImGui.TableNextColumn();
                    ImGui.TextDisabled($"{slotInfo.EquipSlotCategoryRowId}");

                    ImGui.TableNextColumn();
                    var isSpecialEquipment = EmperorsNewItems.IsEmperorsNewByModelId(slotInfo.ModelId);
                    var idColor = isSpecialEquipment
                        ? new Vector4(1.0f, 0.5f, 0.0f, 1.0f)
                        : new Vector4(0.6f, 0.85f, 1.0f, 1.0f);
                    ImGui.TextColored(idColor, $"{slotInfo.ModelId}");

                    ImGui.TableNextColumn();
                    var itemName = ItemModelNameLookup.FormatDisplayName(
                        slotInfo.ModelId,
                        slotInfo.Variant,
                        slotInfo.Slot,
                        DataManager);
                    DrawTableSingleLineText(itemName, isSpecialEquipment ? idColor : null);

                    ImGui.TableNextColumn();
                    DrawDrawObjectModelPathCell(slotInfo.ModelPath);
                }

                ImGui.EndTable();
            }
        }

        private void DrawDrawObjectModelPathCell(string? rawPath)
        {
            var info = ModelPathDisplayResolver.Resolve(rawPath, _penumbraInterop);
            switch (info.Kind)
            {
                case ModelPathKind.Empty:
                    ImGui.TextDisabled(info.DisplayText);
                    return;
                case ModelPathKind.Vanilla:
                    ImGui.TextColored(new Vector4(0.65f, 0.85f, 0.65f, 1.0f), info.DisplayText);
                    if (ImGui.IsItemHovered() && !string.IsNullOrWhiteSpace(info.TooltipPath))
                        ImGui.SetTooltip(info.TooltipPath);
                    return;
                case ModelPathKind.PenumbraMod when info.Mod != null:
                {
                    var linkColor = new Vector4(0.45f, 0.75f, 1.0f, 1.0f);
                    var label = TruncateForTable(info.DisplayText, 64);
                    ImGui.PushStyleColor(ImGuiCol.Text, linkColor);
                    ImGui.PushStyleColor(ImGuiCol.Header, Vector4.Zero);
                    ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0.45f, 0.75f, 1.0f, 0.12f));
                    ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(0.45f, 0.75f, 1.0f, 0.22f));

                    if (ImGui.Selectable(label, false, ImGuiSelectableFlags.None)
                        && !_penumbraInterop.TryOpenModInPenumbra(info.Mod, out var openResult))
                    {
                        PluginLog.Warning(
                            $"[DrawObject] Penumbra.OpenMainWindow failed for {info.Mod.Directory} / {info.Mod.DisplayName}: {openResult}");
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                        var tooltip = Localization.DebugDrawObjectPenumbraModClick;
                        if (!string.IsNullOrWhiteSpace(info.TooltipPath))
                            tooltip += $"\n{info.TooltipPath}";
                        ImGui.SetTooltip(tooltip);
                    }

                    ImGui.PopStyleColor(4);
                    return;
                }
                default:
                {
                    var display = TruncateForTable(info.DisplayText, 64);
                    ImGui.TextUnformatted(display);
                    if (ImGui.IsItemHovered()
                        && !string.IsNullOrWhiteSpace(info.TooltipPath)
                        && info.TooltipPath.Length > 64)
                    {
                        ImGui.SetTooltip(info.TooltipPath);
                    }

                    break;
                }
            }
        }

        private static string TruncateForTable(string text, int maxLength)
        {
            if (text.Length <= maxLength)
                return text;

            return text[..(maxLength - 3)] + "...";
        }

        private static void DrawTableSingleLineText(string text, Vector4? color = null)
        {
            var cellPadding = ImGui.GetStyle().CellPadding.X;
            var maxWidth = ImGui.GetColumnWidth() - cellPadding * 2f;
            var display = TruncateTextToWidth(text, maxWidth);

            if (color != null)
                ImGui.PushStyleColor(ImGuiCol.Text, color.Value);

            ImGui.TextUnformatted(display);

            if (color != null)
                ImGui.PopStyleColor();

            if (display != text && ImGui.IsItemHovered())
                ImGui.SetTooltip(text);
        }

        private static string TruncateTextToWidth(string text, float maxWidth)
        {
            if (maxWidth <= 0f || ImGui.CalcTextSize(text).X <= maxWidth)
                return text;

            const string ellipsis = "...";
            var ellipsisWidth = ImGui.CalcTextSize(ellipsis).X;
            var maxContentWidth = maxWidth - ellipsisWidth;
            if (maxContentWidth <= 0f)
                return ellipsis;

            for (var length = text.Length; length > 0; length--)
            {
                if (ImGui.CalcTextSize(text[..length]).X <= maxContentWidth)
                    return text[..length] + ellipsis;
            }

            return ellipsis;
        }

        private void DrawPenumbraPluginStatusLine()
        {
            var statusText = string.IsNullOrEmpty(penumbraStatusDisplayText)
                ? Localization.Checking
                : penumbraStatusDisplayText;

            if (penumbraStatusDisplayError)
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.3f, 0.3f, 1.0f));
            ImGui.Text($"{Localization.PenumbraStatus}: {statusText}");
            if (penumbraStatusDisplayError)
                ImGui.PopStyleColor();
        }

        private void DrawSoundMixerPluginStatusLine()
        {
            var statusText = string.IsNullOrEmpty(soundMixerStatusDisplayText)
                ? Localization.Checking
                : soundMixerStatusDisplayText;

            if (soundMixerStatusDisplayError)
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.3f, 0.3f, 1.0f));
            ImGui.Text($"{Localization.SoundMixerStatus}: {statusText}");
            if (soundMixerStatusDisplayError)
                ImGui.PopStyleColor();

            if (isSoundMixerIpcReady)
            {
                ImGui.Indent();
                ImGui.Text(Localization.SoundMixerIpcVersion(
                    soundMixerDetectedApiVersion,
                    SoundMixerInterop.ExpectedApiVersion));
                if (!soundMixerApiVersionCompatible)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.55f, 0.25f, 1.0f));
                    ImGui.TextWrapped(Localization.SoundMixerIpcVersionMismatch);
                    ImGui.PopStyleColor();
                }

                ImGui.Text(soundMixerOverridesActive
                    ? Localization.SoundMixerIpcOverridesActive
                    : Localization.SoundMixerIpcOverridesInactive);
                ImGui.Unindent();
            }
        }

        private void DrawSoundMixerIpcDiagnostics()
        {
            RefreshSoundMixerIpcGateProbesIfNeeded();

            if (!ImGui.CollapsingHeader(Localization.SoundMixerIpcGatesHeader))
                return;

            ImGui.Indent();
            if (!isSoundMixerLoaded)
            {
                ImGui.TextDisabled(Localization.NotAvailable);
                ImGui.Unindent();
                return;
            }

            foreach (var probe in soundMixerIpcGateProbes)
            {
                var color = probe.Available
                    ? new Vector4(0.3f, 1.0f, 0.3f, 1.0f)
                    : new Vector4(1.0f, 0.3f, 0.3f, 1.0f);
                var suffix = probe.Available
                    ? "✓"
                    : $"✗ {probe.Error}";
                ImGui.TextColored(color, $"{probe.Gate}: {suffix}");
            }

            ImGui.Unindent();
        }

        private void DrawDebugTab()
        {
            ImGui.BeginChild("DebugScroll", new Vector2(0, 0), false, ImGuiWindowFlags.None);

            // 错误信息
            if (!string.IsNullOrEmpty(lastError))
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.3f, 0.3f, 1.0f));
                ImGui.TextWrapped($"{Localization.ErrorInfo}: {lastError}");
                ImGui.PopStyleColor();
                ImGui.Separator();
            }
            
            // IPC 测试
            if (ImGui.CollapsingHeader(Localization.IpcTestHeader))
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
            if (ImGui.CollapsingHeader(Localization.ConfigFileLocation))
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
                            if (ImGui.Button(Localization.MigrateConfigManually))
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
                                Localization.NoConfigFileWarning);
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
            if (ImGui.CollapsingHeader(Localization.PluginStatus, ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Indent();
                
                var simpleHeelsStatus = !isSimpleHeelsAvailable
                    ? $"✗ {Localization.NotAvailable}"
                    : isSimpleHeelsIpcReady
                        ? $"✓ {Localization.Available}"
                        : Localization.PluginLoadedIpcPending;
                ImGui.Text($"{Localization.SimpleHeelsStatus}: {simpleHeelsStatus}");

                DrawPenumbraPluginStatusLine();
                DrawSoundMixerPluginStatusLine();
                DrawSoundMixerIpcDiagnostics();
                
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

                if (ConfigurationUsesMoodles())
                {
                    var moodlesStatus = !MoodlesInterop.IsMoodlesLoaded(PluginInterface)
                        ? $"✗ {Localization.NotAvailable}"
                        : isMoodlesIpcReady
                            ? $"✓ {Localization.Available}"
                            : Localization.PluginLoadedIpcPending;
                    ImGui.Text($"{Localization.MoodlesStatus}: {moodlesStatus}");
                }

                if (ConfigurationUsesHonorific())
                {
                    var honorificStatus = !HonorificInterop.IsHonorificLoaded(PluginInterface)
                        ? $"✗ {Localization.NotAvailable}"
                        : isHonorificIpcReady
                            ? $"✓ {Localization.Available}"
                            : Localization.PluginLoadedIpcPending;
                    ImGui.Text($"{Localization.HonorificStatus}: {honorificStatus}");
                }
                
                ImGui.Unindent();
            }
            
            // 最后执行的行动信息
            if (ImGui.CollapsingHeader(Localization.LastExecutedActions, ImGuiTreeNodeFlags.DefaultOpen))
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
                var appliedMoodleKeys = lastAppliedActionKeys
                    .Where(k => k.StartsWith("M:", StringComparison.Ordinal)
                        || k.StartsWith("Baseline:M:", StringComparison.Ordinal))
                    .ToList();
                ImGui.Text($"{Localization.LastAppliedMoodle}: {(appliedMoodleKeys.Count == 0 ? "-" : string.Join(", ", appliedMoodleKeys))}");
                ImGui.Text($"{Localization.ApplyGate}: {applyGateStatus}");
                
                ImGui.Unindent();
            }
            
            // 诊断信息
            if (ImGui.CollapsingHeader(Localization.DiagnosticInfo))
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
            
            // 规则装备条件已改读 DrawObject；Glamourer 装备槽对照仅作遗留调试，默认隐藏。
#if false
            if (isGlamourerAvailable)
            {
                if (ImGui.CollapsingHeader(Localization.GlamourerEquipmentStatus))
                {
                    ImGui.Indent();
                    DrawGlamourerEquipmentStatus();
                    ImGui.Unindent();
                }
            }
#endif

            if (ImGui.CollapsingHeader(Localization.DebugDrawObjectEquipmentStatus))
            {
                ImGui.Indent();
                DrawDrawObjectEquipmentStatus();
                ImGui.Unindent();
            }

            ImGui.EndChild();
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

            if (Configuration.SfwModeActive && !rule.SfwModeEnabled)
                return false;

            // 分组首条始终是“如果”（条件分支）；仅非分组首条的“否则”才无条件兜底命中。
            if (rule.BranchKind == RuleBranchKind.Else && !IsRuleGroupStart(ruleIndex))
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
                RenderedEquipment = RenderedEquipmentSnapshot.Capture(localPlayer),
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
            PersistShutdownApplySnapshot();
            ClientState.Logout -= OnLogout;
            Framework.Update -= OnFrameworkUpdate;
            CommandManager.RemoveHandler("/hdl");
            CommandManager.RemoveHandler("/heelsdesign");
            
            _glamourerInterop.Dispose();
            
            // 清理 Penumbra 事件订阅
            _penumbraInterop.Dispose();

            ClearSoundMixerTemporaryOverrides();
            ClearPenumbraTemporaryOverrides(forceRemove: true);
            ClearSfwPenumbraTemporaryOverrides(forceRemove: true);
            RemoveDtrBarEntry();
            RemoveDtrSfwBarEntry();
        }
    }
}