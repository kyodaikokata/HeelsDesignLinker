using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace HeelsDesignLinker;

/// <summary>将装备 ModelId + EquipSlotCategory 解析为 Lumina 物品名称。</summary>
internal static class ItemModelNameLookup
{
    internal readonly record struct ModelItemEntry(
        uint ItemId,
        string Name,
        uint EquipSlotCategoryRowId,
        byte Variant,
        uint IconId);

    internal readonly record struct EquipmentNameSearchResult(
        ushort ModelId,
        byte Variant,
        string Name,
        uint IconId);

    private static readonly object CacheLock = new();
    private static Dictionary<(ushort ModelId, uint CategoryRowId), List<ModelItemEntry>>? _byModelAndCategory;
    private static Dictionary<uint, List<EquipmentNameSearchResult>>? _searchByCategory;

    public static void Rebuild(IDataManager dataManager)
    {
        lock (CacheLock)
        {
            _byModelAndCategory = null;
            _searchByCategory = null;
            EnsureBuilt(dataManager);
        }
    }

    public static IReadOnlyList<EquipmentNameSearchResult> SearchByName(
        string query,
        RenderedEquipSlot slot,
        IDataManager dataManager,
        int maxResults = 24)
    {
        EnsureBuilt(dataManager);

        var categoryRowId = EquipSlotCategoryMapping.GetCategoryRowId(slot);
        if (categoryRowId == 0 || string.IsNullOrWhiteSpace(query))
            return [];

        if (!_searchByCategory!.TryGetValue(categoryRowId, out var entries))
            return [];

        var trimmed = query.Trim();
        return entries
            .Where(e => e.Name.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.ModelId)
            .ThenBy(e => e.Variant)
            .Take(maxResults)
            .ToList();
    }

    /// <summary>按 ModelId 与装备位置分类行 ID 查询（严格匹配槽位）。</summary>
    public static IReadOnlyList<ModelItemEntry> Lookup(
        ushort modelId,
        uint equipSlotCategoryRowId,
        IDataManager dataManager)
    {
        EnsureBuilt(dataManager);
        if (_byModelAndCategory!.TryGetValue((modelId, equipSlotCategoryRowId), out var entries))
            return entries;

        return [];
    }

    public static IReadOnlyList<ModelItemEntry> LookupForRenderedSlot(
        ushort modelId,
        RenderedEquipSlot slot,
        IDataManager dataManager)
    {
        var categoryRowId = EquipSlotCategoryMapping.GetCategoryRowId(slot);
        if (categoryRowId == 0)
            return [];

        return Lookup(modelId, categoryRowId, dataManager);
    }

    public static bool TryGetIconIdForRenderedSlot(
        ushort modelId,
        RenderedEquipSlot slot,
        IDataManager dataManager,
        out uint iconId)
    {
        iconId = 0;
        var entries = LookupForRenderedSlot(modelId, slot, dataManager);
        if (entries.Count == 0)
            return false;

        iconId = entries[0].IconId;
        return iconId != 0;
    }

    public static int CountEntries(IDataManager dataManager)
    {
        EnsureBuilt(dataManager);
        return _byModelAndCategory!.Count;
    }

    public static string FormatDisplayName(
        ushort modelId,
        byte variant,
        RenderedEquipSlot slot,
        IDataManager dataManager)
    {
        if (modelId == 0)
            return Localization.DebugDrawObjectSmallclothes;

        if (EmperorsNewItems.IsEmperorsNewByModelId(modelId))
            return Localization.DebugDrawObjectEmperorsNew;

        var categoryRowId = EquipSlotCategoryMapping.GetCategoryRowId(slot);
        var entries = Lookup(modelId, categoryRowId, dataManager);
        if (entries.Count == 0)
            return FormatNoMatchText(variant);

        var variantMatched = entries.Where(e => e.Variant == variant).ToList();
        var displayEntries = variantMatched.Count > 0 ? variantMatched : entries;
        var distinctNames = displayEntries
            .Select(e => e.Name)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var text = distinctNames[0];
        var alternateNameCount = distinctNames.Count - 1;
        if (alternateNameCount > 0)
            text += $" {Localization.DebugDrawObjectAlternateNameCount(alternateNameCount)}";

        if (variantMatched.Count == 0 && variant != 0)
            text += $" [{Localization.DebugDrawObjectVariant} {variant}]";

        return text;
    }

    private static void EnsureBuilt(IDataManager dataManager)
    {
        if (_byModelAndCategory != null && _searchByCategory != null)
            return;

        lock (CacheLock)
        {
            if (_byModelAndCategory != null && _searchByCategory != null)
                return;

            var map = new Dictionary<(ushort, uint), List<ModelItemEntry>>();
            var searchByCategory = new Dictionary<uint, List<EquipmentNameSearchResult>>();
            var itemSheet = dataManager.GetExcelSheet<Item>();
            foreach (var item in itemSheet)
            {
                var categoryRowId = (uint)item.EquipSlotCategory.RowId;
                if (categoryRowId == 0 || categoryRowId == EquipSlotCategoryMapping.ObsoleteWaistCategoryRowId)
                    continue;

                if (!EquipSlotCategoryMapping.IsKnownCategoryRowId(categoryRowId))
                    continue;

                var name = item.Name.ExtractText();
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var iconId = (uint)item.Icon;
                foreach (var (modelId, itemVariant) in ExtractEquipmentSetKeys(item.ModelMain, item.ModelSub))
                {
                    var entry = new ModelItemEntry((uint)item.RowId, name, categoryRowId, itemVariant, iconId);
                    var key = (modelId, categoryRowId);
                    if (!map.TryGetValue(key, out var list))
                    {
                        list = [];
                        map[key] = list;
                    }

                    if (!list.Any(e => e.ItemId == entry.ItemId && e.Variant == entry.Variant))
                        list.Add(entry);

                    var searchEntry = new EquipmentNameSearchResult(modelId, itemVariant, name, iconId);
                    if (!searchByCategory.TryGetValue(categoryRowId, out var searchList))
                    {
                        searchList = [];
                        searchByCategory[categoryRowId] = searchList;
                    }

                    if (!searchList.Any(e =>
                            e.ModelId == searchEntry.ModelId
                            && e.Variant == searchEntry.Variant
                            && e.Name == searchEntry.Name))
                    {
                        searchList.Add(searchEntry);
                    }
                }
            }

            _byModelAndCategory = map;
            _searchByCategory = searchByCategory;
        }
    }

    private static string FormatNoMatchText(byte variant)
    {
        if (variant == 0)
            return Localization.DebugDrawObjectNoSlotMatch;

        return $"{Localization.DebugDrawObjectNoSlotMatch} [{Localization.DebugDrawObjectVariant} {variant}]";
    }

    /// <summary>
    /// 从 Item.ModelMain / ModelSub 解出 (modelId, variant)，与 DrawObject <see cref="FFXIVClientStructs.FFXIV.Client.Game.Character.EquipmentModelId"/> 一致。
    /// 低 16 位 = modelId，+16 的 byte = variant；uint64 高 32 位可含副模型。
    /// </summary>
    private static IEnumerable<(ushort ModelId, byte Variant)> ExtractEquipmentSetKeys(ulong modelMain, ulong modelSub)
    {
        var seen = new HashSet<(ushort, byte)>();
        foreach (var packed in new[] { modelMain, modelSub })
        {
            foreach (var key in ExtractEquipmentSetKeys(packed))
            {
                if (seen.Add(key))
                    yield return key;
            }
        }
    }

    private static IEnumerable<(ushort ModelId, byte Variant)> ExtractEquipmentSetKeys(ulong packed)
    {
        if (packed == 0)
            yield break;

        var modelId = (ushort)(packed & 0xFFFF);
        if (modelId != 0)
            yield return (modelId, (byte)((packed >> 16) & 0xFF));

        if (packed <= uint.MaxValue)
            yield break;

        var high = (uint)(packed >> 32);
        var highModelId = (ushort)(high & 0xFFFF);
        if (highModelId != 0 && highModelId != modelId)
            yield return (highModelId, (byte)((high >> 16) & 0xFF));
    }
}
