namespace HeelsToggle;

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

    public static bool IsUnreachable(IReadOnlyList<HeelsRule> rules, int ruleIndex, float tolerance)
    {
        if (ruleIndex < 0 || ruleIndex >= rules.Count)
            return false;

        var rule = rules[ruleIndex];
        if (!rule.IsActive)
            return false;

        var reachable = new List<HeightInterval> { HeightInterval.Universe };

        for (var j = 0; j < ruleIndex; j++)
        {
            var previous = rules[j];
            if (!previous.IsActive)
                continue;

            if (j > 0 && previous.BranchKind == RuleBranchKind.Else)
                return true;

            reachable = SubtractIntervals(reachable, GetMatchIntervals(previous, j, tolerance));
            if (reachable.Count == 0)
                return true;
        }

        if (ruleIndex > 0 && rule.BranchKind == RuleBranchKind.Else)
            return false;

        return !IntersectsAny(reachable, GetMatchIntervals(rule, ruleIndex, tolerance));
    }

    private static List<HeightInterval> GetMatchIntervals(HeelsRule rule, int ruleIndex, float tolerance)
    {
        if (ruleIndex > 0 && rule.BranchKind == RuleBranchKind.Else)
            return [HeightInterval.Universe];

        var value = rule.HeightValue;
        return rule.HeightComparison switch
        {
            HeightComparison.GreaterThan => [Open(value, float.PositiveInfinity)],
            HeightComparison.GreaterThanOrEqual => [Closed(value - tolerance, float.PositiveInfinity)],
            HeightComparison.LessThan => [Open(float.NegativeInfinity, value)],
            HeightComparison.LessThanOrEqual => [Closed(float.NegativeInfinity, value + tolerance)],
            HeightComparison.Equal => [Closed(value - tolerance, value + tolerance)],
            _ => [],
        };
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
