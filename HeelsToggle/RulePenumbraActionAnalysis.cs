namespace HeelsDesignLinker;

internal enum PenumbraActionConflictKind
{
    ModEnableDisable,
    OptionSetting,
    MultiToggleSubOption,

    /// <summary>同一 Mod 上 DisableMod 会使临时模式跳过 SetModOption。</summary>
    DisableModBlocksOption,
}

/// <summary>单个行动的 Penumbra 冲突信息：冲突类型 + 与之冲突的其它行动索引集合。</summary>
internal readonly struct PenumbraActionConflictInfo
{
    public PenumbraActionConflictInfo(PenumbraActionConflictKind kind, IReadOnlyList<int> partners)
    {
        Kind = kind;
        Partners = partners;
    }

    public PenumbraActionConflictKind Kind { get; }

    /// <summary>与该行动冲突的其它行动索引（不含自身），按升序排列。</summary>
    public IReadOnlyList<int> Partners { get; }
}

internal static class RulePenumbraActionAnalysis
{
    public static Dictionary<int, PenumbraActionConflictKind> Analyze(
        IReadOnlyList<HeelsRuleAction> actions,
        Func<string, string, PenumbraGroupType> getGroupType) =>
        AnalyzeWithPartners(actions, getGroupType)
            .ToDictionary(kv => kv.Key, kv => kv.Value.Kind);

    /// <summary>
    /// 分析 Penumbra 行动冲突，返回 行动索引 → (冲突类型, 冲突对手索引集合)。
    /// 同一冲突簇内的行动互为对手。
    /// </summary>
    public static Dictionary<int, PenumbraActionConflictInfo> AnalyzeWithPartners(
        IReadOnlyList<HeelsRuleAction> actions,
        Func<string, string, PenumbraGroupType> getGroupType)
    {
        var kinds = new Dictionary<int, PenumbraActionConflictKind>();
        var partners = new Dictionary<int, HashSet<int>>();

        // 将一个冲突簇内的所有行动互相标记为对手，并记录冲突类型（已存在则不覆盖）。
        void MarkCluster(IReadOnlyCollection<int> cluster, PenumbraActionConflictKind kind)
        {
            foreach (var index in cluster)
            {
                kinds.TryAdd(index, kind);
                if (!partners.TryGetValue(index, out var set))
                {
                    set = [];
                    partners[index] = set;
                }

                foreach (var other in cluster)
                {
                    if (other != index)
                        set.Add(other);
                }
            }
        }

        // 同一 Collection + Mod 的启用/禁用行动收集
        var modStateActions = new Dictionary<string, List<(int Index, PenumbraActionKind Kind)>>(
            StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < actions.Count; i++)
        {
            var action = actions[i];
            if (!IsAnalyzablePenumbraAction(action))
                continue;

            if (action.PenumbraActionKind is PenumbraActionKind.EnableMod or PenumbraActionKind.DisableMod)
            {
                var key = BuildPenumbraModKey(action);
                if (!modStateActions.TryGetValue(key, out var list))
                {
                    list = [];
                    modStateActions[key] = list;
                }

                list.Add((i, action.PenumbraActionKind));
            }
        }

        // 同一 Mod 同时启用与禁用
        foreach (var (_, entries) in modStateActions)
        {
            var hasEnable = entries.Any(e => e.Kind == PenumbraActionKind.EnableMod);
            var hasDisable = entries.Any(e => e.Kind == PenumbraActionKind.DisableMod);
            if (hasEnable && hasDisable)
                MarkCluster(entries.Select(e => e.Index).ToList(), PenumbraActionConflictKind.ModEnableDisable);
        }

        // DisableMod 与 SetModOption 同 Mod：临时模式 batch 会跳过选项
        foreach (var (modKey, entries) in modStateActions)
        {
            if (!entries.Any(e => e.Kind == PenumbraActionKind.DisableMod))
                continue;

            var cluster = new List<int>();
            cluster.AddRange(entries.Where(e => e.Kind == PenumbraActionKind.DisableMod).Select(e => e.Index));

            for (var i = 0; i < actions.Count; i++)
            {
                var action = actions[i];
                if (!IsAnalyzablePenumbraAction(action)
                    || action.PenumbraActionKind != PenumbraActionKind.SetModOption)
                    continue;

                if (string.Equals(BuildPenumbraModKey(action), modKey, StringComparison.OrdinalIgnoreCase))
                    cluster.Add(i);
            }

            if (cluster.Count > 1)
                MarkCluster(cluster, PenumbraActionConflictKind.DisableModBlocksOption);
        }

        // 同一 Collection + Mod + Option 组：设置效果冲突
        var optionActions = new Dictionary<string, List<(int Index, HeelsRuleAction Action)>>(
            StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < actions.Count; i++)
        {
            var action = actions[i];
            if (!IsAnalyzablePenumbraAction(action) || action.PenumbraActionKind != PenumbraActionKind.SetModOption)
                continue;

            var key = BuildPenumbraOptionGroupKey(action);
            if (!optionActions.TryGetValue(key, out var list))
            {
                list = [];
                optionActions[key] = list;
            }

            list.Add((i, action));
        }

        foreach (var (_, entries) in optionActions)
        {
            if (entries.Count < 2)
                continue;

            var sample = entries[0].Action;
            var groupType = getGroupType(
                sample.PenumbraModName ?? "",
                sample.PenumbraOption ?? "");

            if (PenumbraInterop.UsesBoolOptionValue(groupType))
            {
                var subOptionValues = new Dictionary<string, List<(int Index, bool Value)>>(
                    StringComparer.OrdinalIgnoreCase);

                foreach (var (index, action) in entries)
                {
                    if (action.PenumbraMultiToggleStates == null)
                        continue;

                    foreach (var (subOption, enabled) in action.PenumbraMultiToggleStates)
                    {
                        if (string.IsNullOrWhiteSpace(subOption))
                            continue;

                        if (!subOptionValues.TryGetValue(subOption, out var values))
                        {
                            values = [];
                            subOptionValues[subOption] = values;
                        }

                        values.Add((index, enabled));
                    }
                }

                foreach (var (_, values) in subOptionValues)
                {
                    if (values.Count < 2)
                        continue;

                    var hasTrue = values.Any(v => v.Value);
                    var hasFalse = values.Any(v => !v.Value);
                    if (hasTrue && hasFalse)
                        MarkCluster(values.Select(v => v.Index).ToList(), PenumbraActionConflictKind.MultiToggleSubOption);
                }
            }
            else
            {
                var effects = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
                foreach (var (index, action) in entries)
                {
                    if (string.IsNullOrWhiteSpace(action.PenumbraOptionName))
                        continue;

                    var effectKey = PenumbraInterop.BuildApplyStateKey(
                        action.PenumbraOptionName,
                        groupType,
                        action.PenumbraOptionEnabled);

                    if (!effects.TryGetValue(effectKey, out var indices))
                    {
                        indices = [];
                        effects[effectKey] = indices;
                    }

                    indices.Add(index);
                }

                if (effects.Count > 1)
                    MarkCluster(effects.Values.SelectMany(indices => indices).ToList(), PenumbraActionConflictKind.OptionSetting);
            }
        }

        var result = new Dictionary<int, PenumbraActionConflictInfo>();
        foreach (var (index, kind) in kinds)
        {
            var partnerList = partners.TryGetValue(index, out var set)
                ? set.OrderBy(v => v).ToList()
                : new List<int>();
            result[index] = new PenumbraActionConflictInfo(kind, partnerList);
        }

        return result;
    }

    private static bool IsAnalyzablePenumbraAction(HeelsRuleAction action)
    {
        if (action.Type != ActionType.Penumbra)
            return false;

        if (string.IsNullOrWhiteSpace(action.PenumbraModName))
            return false;

        return action.PenumbraActionKind switch
        {
            PenumbraActionKind.EnableMod or PenumbraActionKind.DisableMod => true,
            PenumbraActionKind.SetModOption => !string.IsNullOrWhiteSpace(action.PenumbraOption),
            _ => false,
        };
    }

    private static string NormalizePenumbraCollection(string? collection) =>
        string.IsNullOrWhiteSpace(collection) ? "Default" : collection.Trim();

    private static string BuildPenumbraModKey(HeelsRuleAction action) =>
        $"{NormalizePenumbraCollection(action.PenumbraCollection)}|{action.PenumbraModName!.Trim()}";

    private static string BuildPenumbraOptionGroupKey(HeelsRuleAction action) =>
        $"{NormalizePenumbraCollection(action.PenumbraCollection)}|{action.PenumbraModName!.Trim()}|{action.PenumbraOption!.Trim()}";
}
