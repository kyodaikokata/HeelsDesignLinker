namespace HeelsDesignLinker;

/// <summary>规则 <see cref="EquipSlot"/> 与 DrawObject Human 渲染槽的对应。</summary>
internal static class EquipSlotRenderedMapping
{
    public static bool TryToRendered(EquipSlot slot, out RenderedEquipSlot rendered)
    {
        switch (slot)
        {
            case EquipSlot.Head:
                rendered = RenderedEquipSlot.Head;
                return true;
            case EquipSlot.Body:
                rendered = RenderedEquipSlot.Top;
                return true;
            case EquipSlot.Hands:
                rendered = RenderedEquipSlot.Arms;
                return true;
            case EquipSlot.Legs:
                rendered = RenderedEquipSlot.Legs;
                return true;
            case EquipSlot.Feet:
                rendered = RenderedEquipSlot.Feet;
                return true;
            case EquipSlot.Ears:
                rendered = RenderedEquipSlot.Ear;
                return true;
            case EquipSlot.Neck:
                rendered = RenderedEquipSlot.Neck;
                return true;
            case EquipSlot.Wrists:
                rendered = RenderedEquipSlot.Wrist;
                return true;
            case EquipSlot.RFinger:
                rendered = RenderedEquipSlot.RFinger;
                return true;
            case EquipSlot.LFinger:
                rendered = RenderedEquipSlot.LFinger;
                return true;
            default:
                rendered = default;
                return false;
        }
    }

    public static bool UsesInventoryForEquipmentCheck(EquipSlot slot) =>
        slot is EquipSlot.MainHand or EquipSlot.OffHand;
}
