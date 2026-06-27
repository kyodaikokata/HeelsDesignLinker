using System.Text;
using Dalamud.Game.ClientState.Objects.SubKinds;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Newtonsoft.Json.Linq;

namespace HeelsDesignLinker;

/// <summary>
/// 以 DrawObject 渲染结果作为外观基准：指纹去重、Glamourer apply 前比对。
/// </summary>
internal static class DrawObjectAppearanceBaseline
{
    private static readonly EquipSlot[] AllEquipSlots =
    [
        EquipSlot.MainHand, EquipSlot.OffHand, EquipSlot.Head, EquipSlot.Body,
        EquipSlot.Hands, EquipSlot.Legs, EquipSlot.Feet, EquipSlot.Ears,
        EquipSlot.Neck, EquipSlot.Wrists, EquipSlot.RFinger, EquipSlot.LFinger,
    ];

    public static bool CanReadDrawObject(IPlayerCharacter? localPlayer) =>
        DrawObjectEquipmentReader.TryReadLocalPlayerSlots(localPlayer, out _, out _);

    /// <summary>TransformationId + DrawObject 10 槽 + 背包 13 槽。</summary>
    public static string ComputeFingerprint(
        IPlayerCharacter? localPlayer,
        RenderedEquipmentSnapshot? renderedSnapshot = null)
    {
        var sb = new StringBuilder(512);
        AppendTransformation(sb, localPlayer);
        if (renderedSnapshot?.IsAvailable == true)
            AppendRenderedFromSnapshot(sb, renderedSnapshot.Slots);
        else
            AppendRenderedDrawObject(sb, localPlayer);
        AppendInventory(sb);
        return sb.ToString();
    }

    /// <summary>
    /// 设计在 Apply=true 的槽位上是否已与 DrawObject 一致。
    /// true=无需 apply；false=不一致；null=无法判定（DrawObject 不可读或设计无法解析）。
    /// </summary>
    public static bool? DoesDesignMatchRenderedAppearance(
        string? designName,
        IPlayerCharacter? localPlayer,
        GlamourerInterop glamourerInterop)
    {
        if (string.IsNullOrWhiteSpace(designName))
            return null;

        if (!DrawObjectEquipmentReader.TryReadLocalPlayerSlots(localPlayer, out var renderedSlots, out _))
            return null;

        var guid = glamourerInterop.GetDesignGuidByName(designName.Trim());
        if (guid == null || guid.Value == Guid.Empty)
            return null;

        var designJson = glamourerInterop.GetDesignJObjectForComparison(guid.Value);
        if (designJson == null)
            return null;

        var comparedAny = false;
        foreach (var slot in AllEquipSlots)
        {
            if (EquipSlotRenderedMapping.UsesInventoryForEquipmentCheck(slot))
                continue;

            var equipToken = designJson.SelectToken($"Equipment.{slot}");
            if (equipToken == null)
                continue;

            var applyToken = equipToken.SelectToken("Apply");
            if (applyToken?.Type != JTokenType.Boolean || !applyToken.ToObject<bool>())
                continue;

            if (!EquipSlotRenderedMapping.TryToRendered(slot, out var renderedSlot))
                return null;

            if (!TryGetRenderedSlot(renderedSlots, renderedSlot, out var rendered))
                return null;

            var designModelId = equipToken.Value<ushort>("ModelId");
            var designVariant = equipToken.Value<byte>("Variant");
            comparedAny = true;

            if (!ModelIdsMatchForApply(designModelId, designVariant, rendered.ModelId, rendered.Variant))
                return false;
        }

        return comparedAny ? true : null;
    }

    private static bool TryGetRenderedSlot(
        IReadOnlyList<RenderedEquipmentSlotInfo> slots,
        RenderedEquipSlot renderedSlot,
        out RenderedEquipmentSlotInfo rendered)
    {
        foreach (var slot in slots)
        {
            if (slot.Slot != renderedSlot)
                continue;

            rendered = slot;
            return true;
        }

        rendered = default;
        return false;
    }

    private static bool ModelIdsMatchForApply(
        ushort designModelId,
        byte designVariant,
        ushort renderedModelId,
        byte renderedVariant)
    {
        if (EmperorsNewItems.IsEmperorsNewByModelId(designModelId))
            return renderedModelId == 0 || EmperorsNewItems.IsEmperorsNewByModelId(renderedModelId);

        if (designModelId == 0)
            return renderedModelId == 0 || EmperorsNewItems.IsEmperorsNewByModelId(renderedModelId);

        return designModelId == renderedModelId && designVariant == renderedVariant;
    }

    private static unsafe void AppendTransformation(StringBuilder sb, IPlayerCharacter? localPlayer)
    {
        sb.Append("T:");
        if (localPlayer == null || !localPlayer.IsValid())
        {
            sb.Append('?');
            sb.Append('|');
            return;
        }

        var character = (Character*)localPlayer.Address;
        sb.Append(character != null ? character->TransformationId : (short)-1);
        sb.Append('|');
    }

    private static void AppendRenderedFromSnapshot(StringBuilder sb, IReadOnlyList<RenderedEquipmentSlotInfo> slots)
    {
        sb.Append("R:");
        for (var i = 0; i < slots.Count; i++)
        {
            if (i > 0)
                sb.Append(',');

            var slot = slots[i];
            sb.Append((int)slot.Slot);
            sb.Append(':');
            sb.Append(slot.ModelId);
            sb.Append(':');
            sb.Append(slot.Variant);
        }

        sb.Append('|');
    }

    private static void AppendRenderedDrawObject(StringBuilder sb, IPlayerCharacter? localPlayer)
    {
        sb.Append("R:");
        if (!DrawObjectEquipmentReader.TryReadLocalPlayerSlots(localPlayer, out var slots, out _))
        {
            sb.Append('?');
            sb.Append('|');
            return;
        }

        AppendRenderedFromSnapshot(sb, slots);
    }

    private static unsafe void AppendInventory(StringBuilder sb)
    {
        sb.Append("I:");
        var inventoryManager = InventoryManager.Instance();
        if (inventoryManager == null)
        {
            sb.Append('?');
            return;
        }

        var container = inventoryManager->GetInventoryContainer(InventoryType.EquippedItems);
        if (container == null)
        {
            sb.Append('?');
            return;
        }

        for (var i = 0; i < 13; i++)
        {
            if (i > 0)
                sb.Append(',');

            var item = container->GetInventorySlot(i);
            sb.Append(item != null ? item->ItemId : 0u);
        }
    }
}
