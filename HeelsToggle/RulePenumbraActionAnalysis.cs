namespace HeelsDesignLinker;



internal enum PenumbraActionConflictKind

{

    ModEnableDisable,

    OptionSetting,

    MultiToggleSubOption,

    /// <summary>同一 Mod 上 DisableMod 会使临时模式跳过 SetModOption。</summary>
    DisableModBlocksOption,

}



internal static class RulePenumbraActionAnalysis

{

    public static Dictionary<int, PenumbraActionConflictKind> Analyze(

        IReadOnlyList<HeelsRuleAction> actions,

        Func<string, string, PenumbraGroupType> getGroupType)

    {

        var conflicts = new Dictionary<int, PenumbraActionConflictKind>();



        void Mark(int index, PenumbraActionConflictKind kind)

        {

            if (!conflicts.ContainsKey(index))

                conflicts[index] = kind;

        }



        void MarkAll(IEnumerable<int> indices, PenumbraActionConflictKind kind)

        {

            foreach (var index in indices)

                Mark(index, kind);

        }



        // 同一 Collection + Mod 同时启用与禁用

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



        foreach (var (_, entries) in modStateActions)

        {

            var hasEnable = entries.Any(e => e.Kind == PenumbraActionKind.EnableMod);

            var hasDisable = entries.Any(e => e.Kind == PenumbraActionKind.DisableMod);

            if (hasEnable && hasDisable)

                MarkAll(entries.Select(e => e.Index), PenumbraActionConflictKind.ModEnableDisable);

        }



        // DisableMod 与 SetModOption 同 Mod：临时模式 batch 会跳过选项

        foreach (var (modKey, entries) in modStateActions)

        {

            if (!entries.Any(e => e.Kind == PenumbraActionKind.DisableMod))

                continue;



            for (var i = 0; i < actions.Count; i++)

            {

                var action = actions[i];

                if (!IsAnalyzablePenumbraAction(action)

                    || action.PenumbraActionKind != PenumbraActionKind.SetModOption)

                    continue;



                if (string.Equals(BuildPenumbraModKey(action), modKey, StringComparison.OrdinalIgnoreCase))

                    Mark(i, PenumbraActionConflictKind.DisableModBlocksOption);

            }



            foreach (var entry in entries.Where(e => e.Kind == PenumbraActionKind.DisableMod))

                Mark(entry.Index, PenumbraActionConflictKind.DisableModBlocksOption);

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

                        MarkAll(values.Select(v => v.Index), PenumbraActionConflictKind.MultiToggleSubOption);

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

                {

                    MarkAll(effects.Values.SelectMany(indices => indices), PenumbraActionConflictKind.OptionSetting);

                }

            }

        }



        return conflicts;

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

        $"{NormalizePenumbraCollection(action.PenumbraCollection)}|{action.PenumbraModName.Trim()}";



    private static string BuildPenumbraOptionGroupKey(HeelsRuleAction action) =>

        $"{NormalizePenumbraCollection(action.PenumbraCollection)}|{action.PenumbraModName.Trim()}|{action.PenumbraOption.Trim()}";

}


