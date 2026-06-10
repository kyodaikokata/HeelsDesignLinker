namespace HeelsDesignLinker;

/// <summary>Glamourer 行动按装备槽位的冲突状态。</summary>
internal enum GlamourerConflictKind
{
    /// <summary>与其他可同时应用的 Glamourer 行动存在装备槽位冲突，但优先级不同（有确定赢家）。</summary>
    Resolved,

    /// <summary>与其他可同时应用的 Glamourer 行动存在装备槽位冲突，且优先级相同（无法决定胜负）。</summary>
    Unresolved,
}

/// <summary>
/// 分析「可能被同时应用」的 Glamourer 行动之间的装备槽位冲突。
/// 可同时应用的判定：同一规则内永远算；跨规则仅当两规则可共存
/// （处于不同的 OR 互斥块；同一 OR 块内的不同规则互斥，不会同时应用）。
/// 冲突 = 两行动 design 实际应用的装备槽位集合有交集；
/// 同优先级 → Unresolved（红），不同优先级 → Resolved（蓝）。
/// </summary>
internal static class RuleGlamourerConflictAnalysis
{
    private readonly struct GlamourerActionInfo
    {
        public GlamourerActionInfo(int ruleIndex, int actionIndex, int blockId, int priority, HashSet<EquipSlot> slots)
        {
            RuleIndex = ruleIndex;
            ActionIndex = actionIndex;
            BlockId = blockId;
            Priority = priority;
            Slots = slots;
        }

        public int RuleIndex { get; }
        public int ActionIndex { get; }
        public int BlockId { get; }
        public int Priority { get; }
        public HashSet<EquipSlot> Slots { get; }
    }

    /// <summary>
    /// 返回 (规则索引, 行动索引) → 冲突状态。无冲突的行动不会出现在结果中（视为白色）。
    /// </summary>
    public static Dictionary<(int RuleIndex, int ActionIndex), GlamourerConflictKind> Analyze(
        IReadOnlyList<HeelsRule> rules,
        Func<string, HashSet<EquipSlot>?> getDesignSlots)
    {
        var result = new Dictionary<(int, int), GlamourerConflictKind>();
        if (rules == null || rules.Count == 0)
            return result;

        // 计算每条规则所属的 OR 互斥块（与匹配引擎一致）：i==0 或前一条连接符为 And 时开新块。
        var blockOfRule = new int[rules.Count];
        var blockId = 0;
        for (var i = 0; i < rules.Count; i++)
        {
            if (i == 0 || rules[i - 1].OperatorToNext == LogicOperator.And)
                blockId++;
            blockOfRule[i] = blockId;
        }

        var infos = new List<GlamourerActionInfo>();
        for (var ruleIndex = 0; ruleIndex < rules.Count; ruleIndex++)
        {
            var rule = rules[ruleIndex];
            if (rule.Actions == null)
                continue;

            for (var actionIndex = 0; actionIndex < rule.Actions.Count; actionIndex++)
            {
                var action = rule.Actions[actionIndex];
                if (action.Type != ActionType.Glamourer || string.IsNullOrWhiteSpace(action.GlamourerDesign))
                    continue;

                var slots = getDesignSlots(action.GlamourerDesign);
                if (slots == null || slots.Count == 0)
                    continue;

                infos.Add(new GlamourerActionInfo(
                    ruleIndex,
                    actionIndex,
                    blockOfRule[ruleIndex],
                    action.GlamourerPriority,
                    slots));
            }
        }

        for (var a = 0; a < infos.Count; a++)
        {
            var current = infos[a];
            var hasConflict = false;
            var hasEqualPriorityConflict = false;

            for (var b = 0; b < infos.Count; b++)
            {
                if (a == b)
                    continue;

                var other = infos[b];

                // 可同时应用：同一规则内永远算；跨规则仅当处于不同 OR 互斥块。
                var coApplicable = current.RuleIndex == other.RuleIndex || current.BlockId != other.BlockId;
                if (!coApplicable)
                    continue;

                if (!SlotsIntersect(current.Slots, other.Slots))
                    continue;

                hasConflict = true;
                if (current.Priority == other.Priority)
                {
                    hasEqualPriorityConflict = true;
                    break;
                }
            }

            if (!hasConflict)
                continue;

            result[(current.RuleIndex, current.ActionIndex)] =
                hasEqualPriorityConflict ? GlamourerConflictKind.Unresolved : GlamourerConflictKind.Resolved;
        }

        return result;
    }

    private static bool SlotsIntersect(HashSet<EquipSlot> a, HashSet<EquipSlot> b)
    {
        // 遍历较小的集合以减少比较次数。
        if (a.Count > b.Count)
            (a, b) = (b, a);

        foreach (var slot in a)
        {
            if (b.Contains(slot))
                return true;
        }

        return false;
    }
}
