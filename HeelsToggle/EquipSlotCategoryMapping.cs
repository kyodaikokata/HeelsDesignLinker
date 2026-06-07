namespace HeelsDesignLinker;

/// <summary>
/// Human DrawObject 槽位与 Lumina EquipSlotCategory 行 ID 的对应关系。
/// RowId 6（旧腰带）已废弃且无物品；腿部为 7、脚部为 8、耳饰为 9。
/// </summary>
internal static class EquipSlotCategoryMapping
{
    /// <summary>已废弃、Item 表中无条目的 EquipSlotCategory 行 ID（旧腰带）。</summary>
    public const uint ObsoleteWaistCategoryRowId = 6;

    internal readonly record struct SlotMapping(
        RenderedEquipSlot Slot,
        uint EquipSlotCategoryRowId,
        int HumanModelArrayIndex,
        string DebugLabelEn);

    private static readonly SlotMapping[] Mappings =
    [
        new(RenderedEquipSlot.Head, 3, 0, "Head"),
        new(RenderedEquipSlot.Top, 4, 1, "Top/Body"),
        new(RenderedEquipSlot.Arms, 5, 2, "Arms/Hands"),
        new(RenderedEquipSlot.Legs, 7, 3, "Legs"),
        new(RenderedEquipSlot.Feet, 8, 4, "Feet"),
        new(RenderedEquipSlot.Ear, 9, 5, "Ears"),
        new(RenderedEquipSlot.Neck, 10, 6, "Neck"),
        new(RenderedEquipSlot.Wrist, 11, 7, "Wrists"),
        new(RenderedEquipSlot.RFinger, 12, 8, "RFinger"),
        new(RenderedEquipSlot.LFinger, 12, 9, "LFinger"),
    ];

    public static IReadOnlyList<SlotMapping> All => Mappings;

    public static uint GetCategoryRowId(RenderedEquipSlot slot)
    {
        foreach (var mapping in Mappings)
        {
            if (mapping.Slot == slot)
                return mapping.EquipSlotCategoryRowId;
        }

        return 0;
    }

    public static bool TryGetMapping(RenderedEquipSlot slot, out SlotMapping mapping)
    {
        foreach (var entry in Mappings)
        {
            if (entry.Slot != slot)
                continue;

            mapping = entry;
            return true;
        }

        mapping = default;
        return false;
    }

    public static bool IsKnownCategoryRowId(uint categoryRowId) =>
        categoryRowId != 0 && categoryRowId != ObsoleteWaistCategoryRowId
        && Mappings.Any(m => m.EquipSlotCategoryRowId == categoryRowId);
}
