using FFXIVClientStructs.FFXIV.Client.Game;

namespace HeelsDesignLinker;

/// <summary>从装备背包读取槽位是否有实物装备（主/副手等 DrawObject Human 不覆盖的槽位）。</summary>
internal static class InventoryEquipmentReader
{
    public static unsafe bool? TryGetHasRealEquipment(EquipSlot slot)
    {
        var inventoryManager = InventoryManager.Instance();
        if (inventoryManager == null)
            return null;

        var container = inventoryManager->GetInventoryContainer(InventoryType.EquippedItems);
        if (container == null)
            return null;

        var equipSlotIndex = slot switch
        {
            EquipSlot.MainHand => 0,
            EquipSlot.OffHand => 1,
            EquipSlot.Head => 2,
            EquipSlot.Body => 3,
            EquipSlot.Hands => 4,
            EquipSlot.Legs => 6,
            EquipSlot.Feet => 7,
            EquipSlot.Ears => 8,
            EquipSlot.Neck => 9,
            EquipSlot.Wrists => 10,
            EquipSlot.RFinger => 11,
            EquipSlot.LFinger => 12,
            _ => -1,
        };

        if (equipSlotIndex < 0)
            return null;

        var item = container->GetInventorySlot(equipSlotIndex);
        if (item == null)
            return null;

        var itemId = item->ItemId;
        if (itemId == 0 || EmperorsNewItems.IsEmperorsNewByItemId(itemId))
            return false;

        return true;
    }
}
