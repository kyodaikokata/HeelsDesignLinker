using Dalamud.Game.ClientState.Objects.SubKinds;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace HeelsDesignLinker;

/// <summary>
/// 检测本地玩家外观 ModelId 变化：优先 DrawObject Human，不可用时回退 DrawData。
/// </summary>
internal sealed class AppearanceChangeTracker
{
    private readonly ushort[] _lastRenderedModelIds = new ushort[EquipSlotCategoryMapping.All.Count];
    private readonly ushort[] _lastDrawDataModelIds = new ushort[10];
    private bool _initialized;

    public void Reset()
    {
        _initialized = false;
        Array.Clear(_lastRenderedModelIds, 0, _lastRenderedModelIds.Length);
        Array.Clear(_lastDrawDataModelIds, 0, _lastDrawDataModelIds.Length);
    }

    /// <param name="modelIdsChanged">是否为真实 ModelId 变化（非首次初始化）。</param>
    public unsafe bool CheckChanged(IPlayerCharacter? localPlayer, out bool modelIdsChanged)
    {
        modelIdsChanged = false;

        var snapshot = RenderedEquipmentSnapshot.Capture(localPlayer);
        if (snapshot.IsAvailable)
            return CompareRendered(snapshot.GetModelIdsInSlotOrder(), out modelIdsChanged);

        return CompareDrawData(localPlayer, out modelIdsChanged);
    }

    private bool CompareRendered(IReadOnlyList<ushort> modelIds, out bool modelIdsChanged)
    {
        modelIdsChanged = false;

        var changed = false;
        var count = Math.Min(modelIds.Count, _lastRenderedModelIds.Length);
        for (var i = 0; i < count; i++)
        {
            if (!_initialized)
            {
                _lastRenderedModelIds[i] = modelIds[i];
                continue;
            }

            if (_lastRenderedModelIds[i] == modelIds[i])
                continue;

            changed = true;
            modelIdsChanged = true;
            _lastRenderedModelIds[i] = modelIds[i];
        }

        if (!_initialized)
            _initialized = true;

        return changed;
    }

    private unsafe bool CompareDrawData(IPlayerCharacter? localPlayer, out bool modelIdsChanged)
    {
        modelIdsChanged = false;

        if (localPlayer == null || !localPlayer.IsValid())
        {
            _initialized = false;
            return false;
        }

        var character = (Character*)localPlayer.Address;
        if (character == null)
        {
            _initialized = false;
            return false;
        }

        var changed = false;

        for (var i = 0; i < 10; i++)
        {
            var modelId = character->DrawData.EquipmentModelIds[i].Id;
            if (!_initialized)
            {
                _lastDrawDataModelIds[i] = modelId;
                continue;
            }

            if (_lastDrawDataModelIds[i] == modelId)
                continue;

            changed = true;
            modelIdsChanged = true;
            _lastDrawDataModelIds[i] = modelId;
        }

        if (!_initialized)
            _initialized = true;

        return changed;
    }
}
