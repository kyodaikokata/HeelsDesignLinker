namespace HeelsDesignLinker;

internal static class RuleHeightAnalysis
{
    private readonly record struct HeightInterval(
        float Min,
        float Max,
        bool MinInclusive,
        bool MaxInclusive)
    {
        public static HeightInterval Universe { get; } =
            new(float.NegativeInfinity, float.PositiveInfinity, true, true);

        public bool IsEmpty =>
            Min > Max
            || (Min.Equals(Max) && (!MinInclusive || !MaxInclusive));
    }

    /// <summary>前面存在「否则」分支时，后续规则在结构上永远无法执行到。</summary>
    public static bool IsBlockedByPriorElse(IReadOnlyList<HeelsRule> rules, int ruleIndex)
    {
        if (ruleIndex <= 0)
            return false;

        for (var j = 0; j < ruleIndex; j++)
        {
            var previous = rules[j];
            if (!previous.IsActive)
                continue;

            if (j > 0 && previous.BranchKind == RuleBranchKind.Else)
                return true;
        }

        return false;
    }

    public static bool IsUnreachable(IReadOnlyList<HeelsRule> rules, int ruleIndex, float tolerance)
    {
        if (ruleIndex < 0 || ruleIndex >= rules.Count)
            return false;

        var rule = rules[ruleIndex];
        if (!rule.IsActive)
            return false;

        if (IsBlockedByPriorElse(rules, ruleIndex))
            return true;

        // 如果规则使用新的多条件组系统且包含非高度条件（如装备条件），跳过高度区间不可达分析
        if (rule.ConditionGroups != null && rule.ConditionGroups.Count > 0
            && rule.ConditionGroups.Any(HasNonHeightConditions))
            return false;

        // 向后兼容：检查旧的单个 ConditionGroup
#pragma warning disable CS0618
        if (rule.ConditionGroup != null && HasNonHeightConditions(rule.ConditionGroup))
            return false;
#pragma warning restore CS0618

        var reachable = new List<HeightInterval> { HeightInterval.Universe };

        for (var j = 0; j < ruleIndex; j++)
        {
            var previous = rules[j];
            if (!previous.IsActive)
                continue;

            // 装备条件无法参与高度区间分析，跳过该规则但不影响后续结构性判断
            if (previous.ConditionGroups != null && previous.ConditionGroups.Count > 0
                && previous.ConditionGroups.Any(HasNonHeightConditions))
                continue;

            // 向后兼容：检查旧的单个 ConditionGroup
#pragma warning disable CS0618
            if (previous.ConditionGroup != null && HasNonHeightConditions(previous.ConditionGroup))
                continue;
#pragma warning restore CS0618

            reachable = SubtractIntervals(reachable, GetMatchIntervals(previous, j, tolerance));
            if (reachable.Count == 0)
                return true;
        }

        if (ruleIndex > 0 && rule.BranchKind == RuleBranchKind.Else)
            return false;

        return !IntersectsAny(reachable, GetMatchIntervals(rule, ruleIndex, tolerance));
    }

    /// <summary>
    /// 检查条件组是否包含非高度条件（如装备条件）
    /// </summary>
    private static bool HasNonHeightConditions(ConditionGroup group)
    {
        foreach (var condition in group.Conditions)
        {
            if (condition is not HeightCondition)
                return true;
        }
        return false;
    }

    private static List<HeightInterval> GetMatchIntervals(HeelsRule rule, int ruleIndex, float tolerance)
    {
        if (ruleIndex > 0 && rule.BranchKind == RuleBranchKind.Else)
            return [HeightInterval.Universe];

        if (TryGetSingleHeightCondition(rule, out var comparison, out var value))
        {
            return comparison switch
            {
                HeightComparison.GreaterThan => [Open(value, float.PositiveInfinity)],
                HeightComparison.GreaterThanOrEqual => [Closed(value - tolerance, float.PositiveInfinity)],
                HeightComparison.LessThan => [Open(float.NegativeInfinity, value)],
                HeightComparison.LessThanOrEqual => [Closed(float.NegativeInfinity, value + tolerance)],
                HeightComparison.Equal => [Closed(value - tolerance, value + tolerance)],
                _ => [],
            };
        }

        var legacyValue = rule.HeightValue;
        return rule.HeightComparison switch
        {
            HeightComparison.GreaterThan => [Open(legacyValue, float.PositiveInfinity)],
            HeightComparison.GreaterThanOrEqual => [Closed(legacyValue - tolerance, float.PositiveInfinity)],
            HeightComparison.LessThan => [Open(float.NegativeInfinity, legacyValue)],
            HeightComparison.LessThanOrEqual => [Closed(float.NegativeInfinity, legacyValue + tolerance)],
            HeightComparison.Equal => [Closed(legacyValue - tolerance, legacyValue + tolerance)],
            _ => [],
        };
    }

    /// <summary>从条件组中提取唯一的高度条件（仅高度条件、单组、单条件时）。</summary>
    private static bool TryGetSingleHeightCondition(
        HeelsRule rule,
        out HeightComparison comparison,
        out float value)
    {
        comparison = rule.HeightComparison;
        value = rule.HeightValue;

        if (rule.ConditionGroups == null || rule.ConditionGroups.Count != 1)
            return false;

        var group = rule.ConditionGroups[0];
        if (group.Conditions == null || group.Conditions.Count != 1)
            return false;

        if (group.Conditions[0] is not HeightCondition heightCondition)
            return false;

        comparison = heightCondition.Comparison;
        value = heightCondition.Value;
        return true;
    }

    private static HeightInterval Open(float min, float max) => new(min, max, false, false);

    private static HeightInterval Closed(float min, float max) => new(min, max, true, true);

    private static bool IntersectsAny(IReadOnlyList<HeightInterval> left, IReadOnlyList<HeightInterval> right)
    {
        foreach (var a in left)
        {
            if (a.IsEmpty)
                continue;

            foreach (var b in right)
            {
                if (!b.IsEmpty && Intersects(a, b))
                    return true;
            }
        }

        return false;
    }

    private static List<HeightInterval> SubtractIntervals(
        IReadOnlyList<HeightInterval> regions,
        IReadOnlyList<HeightInterval> removeIntervals)
    {
        var result = regions.ToList();
        foreach (var remove in removeIntervals)
        {
            if (remove.IsEmpty)
                continue;

            var next = new List<HeightInterval>();
            foreach (var region in result)
            {
                if (region.IsEmpty)
                    continue;

                next.AddRange(SubtractInterval(region, remove));
            }

            result = next;
        }

        return result;
    }

    private static IEnumerable<HeightInterval> SubtractInterval(HeightInterval region, HeightInterval remove)
    {
        if (!Intersects(region, remove))
        {
            yield return region;
            yield break;
        }

        if (IsLessThan(region.Min, region.MinInclusive, remove.Min, remove.MinInclusive))
        {
            yield return new HeightInterval(
                region.Min,
                remove.Min,
                region.MinInclusive,
                !remove.MinInclusive);
        }

        if (IsLessThan(remove.Max, remove.MaxInclusive, region.Max, region.MaxInclusive))
        {
            yield return new HeightInterval(
                remove.Max,
                region.Max,
                !remove.MaxInclusive,
                region.MaxInclusive);
        }
    }

    private static bool Intersects(HeightInterval a, HeightInterval b)
    {
        if (a.IsEmpty || b.IsEmpty)
            return false;

        if (IsGreaterThan(a.Min, a.MinInclusive, b.Max, b.MaxInclusive))
            return false;

        if (IsGreaterThan(b.Min, b.MinInclusive, a.Max, a.MaxInclusive))
            return false;

        return true;
    }

    private static bool IsLessThan(float left, bool leftInclusive, float right, bool rightInclusive)
    {
        if (left < right)
            return true;

        if (left > right)
            return false;

        return leftInclusive && !rightInclusive;
    }

    private static bool IsGreaterThan(float left, bool leftInclusive, float right, bool rightInclusive)
    {
        if (left > right)
            return true;

        if (left < right)
            return false;

        return !leftInclusive && rightInclusive;
    }
}
