namespace HeelsDesignLinker;

internal enum ConditionConflictKind
{
    HeightRange,
    EquipmentSlot,
}

internal static class RuleConditionConflictAnalysis
{
    private readonly record struct HeightInterval(
        float Min,
        float Max,
        bool MinInclusive,
        bool MaxInclusive)
    {
        public bool IsEmpty =>
            Min > Max
            || (Min.Equals(Max) && (!MinInclusive || !MaxInclusive));
    }

    public static IReadOnlyDictionary<int, ConditionConflictKind> Analyze(ConditionGroup group, float tolerance)
    {
        var conflicts = new Dictionary<int, ConditionConflictKind>();
        if (group.Operator != LogicOperator.And)
            return conflicts;

        var conditions = group.Conditions;
        for (var i = 0; i < conditions.Count; i++)
        {
            for (var j = i + 1; j < conditions.Count; j++)
            {
                if (!TryGetConflictKind(conditions[i], conditions[j], tolerance, out var kind))
                    continue;

                conflicts.TryAdd(i, kind);
                conflicts.TryAdd(j, kind);
            }
        }

        return conflicts;
    }

    private static bool TryGetConflictKind(
        RuleCondition left,
        RuleCondition right,
        float tolerance,
        out ConditionConflictKind kind)
    {
        kind = default;

        if (left is HeightCondition leftHeight && right is HeightCondition rightHeight)
        {
            if (HeightIntervalsIntersect(leftHeight, rightHeight, tolerance))
                return false;

            kind = ConditionConflictKind.HeightRange;
            return true;
        }

        if (left is EquipmentCondition leftEquip && right is EquipmentCondition rightEquip)
        {
            if (EquipmentConditionsCompatible(leftEquip, rightEquip))
                return false;

            kind = ConditionConflictKind.EquipmentSlot;
            return true;
        }

        return false;
    }

    private static bool HeightIntervalsIntersect(HeightCondition left, HeightCondition right, float tolerance)
    {
        var a = ToHeightInterval(left, tolerance);
        var b = ToHeightInterval(right, tolerance);
        if (a.IsEmpty || b.IsEmpty)
            return false;

        if (IsGreaterThan(a.Min, a.MinInclusive, b.Max, b.MaxInclusive))
            return false;

        if (IsGreaterThan(b.Min, b.MinInclusive, a.Max, a.MaxInclusive))
            return false;

        return true;
    }

    private static HeightInterval ToHeightInterval(HeightCondition condition, float tolerance) =>
        condition.Comparison switch
        {
            HeightComparison.GreaterThan => Open(condition.Value, float.PositiveInfinity),
            HeightComparison.GreaterThanOrEqual => Closed(condition.Value - tolerance, float.PositiveInfinity),
            HeightComparison.LessThan => Open(float.NegativeInfinity, condition.Value),
            HeightComparison.LessThanOrEqual => Closed(float.NegativeInfinity, condition.Value + tolerance),
            HeightComparison.Equal => Closed(condition.Value - tolerance, condition.Value + tolerance),
            _ => new HeightInterval(0, -1, true, true),
        };

    private static bool EquipmentConditionsCompatible(EquipmentCondition left, EquipmentCondition right)
    {
        if (left.Slot != right.Slot)
            return true;

        if (!left.MustBeEquipped && !right.MustBeEquipped)
            return true;

        if (left.MustBeEquipped != right.MustBeEquipped)
            return false;

        if (left.MatchMode == EquipmentMatchMode.Any || right.MatchMode == EquipmentMatchMode.Any)
            return true;

        return left.TargetModelId == right.TargetModelId;
    }

    private static HeightInterval Open(float min, float max) => new(min, max, false, false);

    private static HeightInterval Closed(float min, float max) => new(min, max, true, true);

    private static bool IsGreaterThan(float left, bool leftInclusive, float right, bool rightInclusive)
    {
        if (left > right)
            return true;

        if (left < right)
            return false;

        return !leftInclusive && rightInclusive;
    }
}
