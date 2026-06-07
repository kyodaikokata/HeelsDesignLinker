using Dalamud.Game.ClientState.Objects.SubKinds;

namespace HeelsDesignLinker;

/// <summary>本地玩家当前渲染装备快照（DrawObject Human），供规则条件评估。</summary>
internal sealed class RenderedEquipmentSnapshot
{
    public static readonly RenderedEquipmentSnapshot Unavailable = new(false, []);

    public bool IsAvailable { get; }

    public IReadOnlyList<RenderedEquipmentSlotInfo> Slots { get; }

    private RenderedEquipmentSnapshot(bool isAvailable, IReadOnlyList<RenderedEquipmentSlotInfo> slots)
    {
        IsAvailable = isAvailable;
        Slots = slots;
    }

    public static RenderedEquipmentSnapshot Capture(IPlayerCharacter? localPlayer)
    {
        if (!DrawObjectEquipmentReader.TryReadLocalPlayerSlots(localPlayer, out var slots, out _))
            return Unavailable;

        return new RenderedEquipmentSnapshot(true, slots);
    }

    public static bool SlotShowsEquipment(in RenderedEquipmentSlotInfo slotInfo) =>
        slotInfo.ModelId != 0 && !EmperorsNewItems.IsEmperorsNewByModelId(slotInfo.ModelId);

    /// <summary>
    /// 槽位是否有可见装备。主/副手走背包；其余槽位需 DrawObject 可读。
    /// 无法确认时返回 null（fail-closed）。
    /// </summary>
    public bool? TryGetHasEquipment(EquipSlot slot)
    {
        if (EquipSlotRenderedMapping.UsesInventoryForEquipmentCheck(slot))
            return InventoryEquipmentReader.TryGetHasRealEquipment(slot);

        if (!IsAvailable)
            return null;

        if (!EquipSlotRenderedMapping.TryToRendered(slot, out var renderedSlot))
            return null;

        foreach (var slotInfo in Slots)
        {
            if (slotInfo.Slot != renderedSlot)
                continue;

            return SlotShowsEquipment(slotInfo);
        }

        return null;
    }

    /// <summary>读取 DrawObject 槽位 ModelId；主/副手或快照不可用时返回 null。</summary>
    public ushort? TryGetRenderedModelId(EquipSlot slot)
    {
        if (EquipSlotRenderedMapping.UsesInventoryForEquipmentCheck(slot))
            return null;

        if (!IsAvailable || !EquipSlotRenderedMapping.TryToRendered(slot, out var renderedSlot))
            return null;

        foreach (var slotInfo in Slots)
        {
            if (slotInfo.Slot == renderedSlot)
                return slotInfo.ModelId;
        }

        return null;
    }

    public IReadOnlyList<ushort> GetModelIdsInSlotOrder()
    {
        var ids = new ushort[EquipSlotCategoryMapping.All.Count];
        for (var i = 0; i < EquipSlotCategoryMapping.All.Count; i++)
        {
            var expected = EquipSlotCategoryMapping.All[i].Slot;
            ids[i] = 0;
            foreach (var slotInfo in Slots)
            {
                if (slotInfo.Slot != expected)
                    continue;

                ids[i] = slotInfo.ModelId;
                break;
            }
        }

        return ids;
    }
}
