using System.Text;
using Dalamud.Game.ClientState.Objects.SubKinds;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;

namespace HeelsDesignLinker;

internal enum RenderedEquipSlot
{
    Head = 0,
    Top = 1,
    Arms = 2,
    Legs = 3,
    Feet = 4,
    Ear = 5,
    Neck = 6,
    Wrist = 7,
    RFinger = 8,
    LFinger = 9,
}

internal readonly struct RenderedEquipmentSlotInfo
{
    public RenderedEquipSlot Slot { get; init; }
    public ushort ModelId { get; init; }
    public byte Variant { get; init; }
    public uint EquipSlotCategoryRowId { get; init; }
    public int HumanModelArrayIndex { get; init; }
    public string? ModelPath { get; init; }
}

/// <summary>从本地玩家 DrawObject（Human）读取当前渲染装备 ModelId。</summary>
internal static class DrawObjectEquipmentReader
{
    public static bool TryReadLocalPlayerSlots(
        IPlayerCharacter? localPlayer,
        out IReadOnlyList<RenderedEquipmentSlotInfo> slots,
        out string? error,
        bool includeModelPaths = true)
    {
        slots = [];
        error = null;

        if (localPlayer == null || !localPlayer.IsValid())
        {
            error = Localization.DebugLocalPlayerUnavailable;
            return false;
        }

        unsafe
        {
            var gameObject = (GameObject*)localPlayer.Address;
            if (gameObject == null)
            {
                error = Localization.DebugCharacterDataUnavailable;
                return false;
            }

            if (gameObject->DrawObject == null)
            {
                error = Localization.DebugDrawObjectNotDrawn;
                return false;
            }

            if (gameObject->DrawObject->GetObjectType() != FFXIVClientStructs.FFXIV.Client.Graphics.Scene.ObjectType.CharacterBase)
            {
                error = Localization.DebugDrawObjectNotCharacterBase;
                return false;
            }

            var characterBase = (CharacterBase*)gameObject->DrawObject;
            if (characterBase->GetModelType() != CharacterBase.ModelType.Human)
            {
                error = Localization.DebugDrawObjectNotHuman;
                return false;
            }

            var human = (Human*)characterBase;
            var result = new List<RenderedEquipmentSlotInfo>(EquipSlotCategoryMapping.All.Count);
            foreach (var mapping in EquipSlotCategoryMapping.All)
            {
                var equipmentModelId = mapping.Slot switch
                {
                    RenderedEquipSlot.Head => human->Head,
                    RenderedEquipSlot.Top => human->Top,
                    RenderedEquipSlot.Arms => human->Arms,
                    RenderedEquipSlot.Legs => human->Legs,
                    RenderedEquipSlot.Feet => human->Feet,
                    RenderedEquipSlot.Ear => human->Ear,
                    RenderedEquipSlot.Neck => human->Neck,
                    RenderedEquipSlot.Wrist => human->Wrist,
                    RenderedEquipSlot.RFinger => human->RFinger,
                    RenderedEquipSlot.LFinger => human->LFinger,
                    _ => default,
                };

                result.Add(CreateSlotInfo(mapping, equipmentModelId, human, includeModelPaths));
            }

            slots = result;
            return true;
        }
    }

    public static string GetSlotLabel(RenderedEquipSlot slot) => slot switch
    {
        RenderedEquipSlot.Head => Localization.EquipSlotName(EquipSlot.Head),
        RenderedEquipSlot.Top => Localization.EquipSlotName(EquipSlot.Body),
        RenderedEquipSlot.Arms => Localization.EquipSlotName(EquipSlot.Hands),
        RenderedEquipSlot.Legs => Localization.EquipSlotName(EquipSlot.Legs),
        RenderedEquipSlot.Feet => Localization.EquipSlotName(EquipSlot.Feet),
        RenderedEquipSlot.Ear => Localization.EquipSlotName(EquipSlot.Ears),
        RenderedEquipSlot.Neck => Localization.EquipSlotName(EquipSlot.Neck),
        RenderedEquipSlot.Wrist => Localization.EquipSlotName(EquipSlot.Wrists),
        RenderedEquipSlot.RFinger => Localization.EquipSlotName(EquipSlot.RFinger),
        RenderedEquipSlot.LFinger => Localization.EquipSlotName(EquipSlot.LFinger),
        _ => slot.ToString(),
    };

    private static unsafe RenderedEquipmentSlotInfo CreateSlotInfo(
        EquipSlotCategoryMapping.SlotMapping mapping,
        EquipmentModelId equipmentModelId,
        Human* human,
        bool includeModelPaths)
    {
        return new RenderedEquipmentSlotInfo
        {
            Slot = mapping.Slot,
            ModelId = equipmentModelId.Id,
            Variant = equipmentModelId.Variant,
            EquipSlotCategoryRowId = mapping.EquipSlotCategoryRowId,
            HumanModelArrayIndex = mapping.HumanModelArrayIndex,
            ModelPath = includeModelPaths ? TryGetModelPath(human, mapping.HumanModelArrayIndex) : null,
        };
    }

    private static unsafe string? TryGetModelPath(Human* human, int modelArrayIndex)
    {
        try
        {
            var models = human->CharacterBase.Models;
            if (models == null || modelArrayIndex < 0 || modelArrayIndex >= human->CharacterBase.SlotCount)
                return null;

            var model = models[modelArrayIndex];
            if (model == null)
                return null;

            var resource = model->ModelResourceHandle;
            if (resource == null)
                return null;

            return Encoding.UTF8.GetString(resource->ResourceHandle.FileName.AsSpan());
        }
        catch
        {
            return null;
        }
    }
}
