namespace HeelsDesignLinker;

/// <summary>
/// 检测本地玩家外观 ModelId 变化：仅比较 DrawObject Human 渲染槽（与装备条件同源）。
/// </summary>
internal sealed class AppearanceChangeTracker
{
    private readonly ushort[] _lastRenderedModelIds = new ushort[EquipSlotCategoryMapping.All.Count];
    private bool _initialized;

    public void Reset()
    {
        _initialized = false;
        Array.Clear(_lastRenderedModelIds, 0, _lastRenderedModelIds.Length);
    }

    /// <param name="modelIdsChanged">是否为真实 ModelId 变化（非首次初始化）。</param>
    public bool CheckChanged(RenderedEquipmentSnapshot snapshot, out bool modelIdsChanged)
    {
        modelIdsChanged = false;

        if (!snapshot.IsAvailable)
        {
            _initialized = false;
            return false;
        }

        return CompareRendered(snapshot.GetModelIdsInSlotOrder(), out modelIdsChanged);
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
}
