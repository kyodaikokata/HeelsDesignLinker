using System.Collections.Generic;

namespace HeelsDesignLinker;

/// <summary>
/// Penumbra 临时 apply 选项组合并：Collection 快照 → 同 key 临时层 → 本次 overrides。
/// </summary>
internal static class PenumbraTemporaryOptionMerge
{
    internal static Dictionary<string, List<string>> Merge(
        IReadOnlyDictionary<string, List<string>> collectionBaseline,
        IReadOnlyDictionary<string, List<string>>? existingSameKeyTemp,
        IReadOnlyDictionary<string, IReadOnlyList<string>> overrides)
    {
        var merged = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (group, names) in collectionBaseline)
            merged[group] = CopyNames(names);

        if (existingSameKeyTemp != null)
        {
            foreach (var (group, names) in existingSameKeyTemp)
                merged[group] = CopyNames(names);
        }

        foreach (var (group, names) in overrides)
        {
            if (string.IsNullOrWhiteSpace(group))
                continue;

            merged[group.Trim()] = CopyNames(names);
        }

        return merged;
    }

    private static List<string> CopyNames(IReadOnlyList<string> names) =>
        names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
