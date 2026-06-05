using Dalamud.Plugin;
using Newtonsoft.Json.Linq;

namespace HeelsToggle;

/// <summary>
/// 通过 Glamourer IPC 读取设计 JSON，判断是否会应用脚部装备槽。
/// </summary>
internal sealed class GlamourerInterop
{
    private const string GetDesignListGate = "Glamourer.GetDesignList.V2";
    private const string GetDesignJObjectGate = "Glamourer.GetDesignJObject";

    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly Dictionary<string, bool> _feetApplyCache = new(StringComparer.OrdinalIgnoreCase);
    private List<string> _designNames = [];
    private DateTime _designListFetchedUtc = DateTime.MinValue;
    private static readonly TimeSpan DesignListRefreshInterval = TimeSpan.FromSeconds(30);

    public GlamourerInterop(IDalamudPluginInterface pluginInterface)
    {
        _pluginInterface = pluginInterface;
    }

    public static bool IsGlamourerLoaded(IDalamudPluginInterface pluginInterface)
    {
        return pluginInterface.InstalledPlugins.Any(p =>
            p.InternalName == "Glamourer" && p.IsLoaded);
    }

    public bool IsIpcAvailable()
    {
        if (!IsGlamourerLoaded(_pluginInterface))
            return false;

        try
        {
            _ = GetDesignMap();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void InvalidateCache(string? designName = null)
    {
        if (string.IsNullOrWhiteSpace(designName))
        {
            _feetApplyCache.Clear();
            return;
        }

        _feetApplyCache.Remove(designName.Trim());
    }

    public void RefreshDesignList(bool force = false)
    {
        _ = GetDesignNames(force);
    }

    public IReadOnlyList<string> GetDesignNames(bool force = false)
    {
        if (!force
            && _designNames.Count > 0
            && (DateTime.UtcNow - _designListFetchedUtc) < DesignListRefreshInterval)
        {
            return _designNames;
        }

        _designNames = [];
        if (!IsIpcAvailable())
            return _designNames;

        try
        {
            var designs = GetDesignMap();
            _designNames = designs.Values
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            _designListFetchedUtc = DateTime.UtcNow;
        }
        catch
        {
            _designNames = [];
        }

        return _designNames;
    }

    public bool? DesignAppliesFeet(string? designName)
    {
        if (string.IsNullOrWhiteSpace(designName))
            return null;

        var key = designName.Trim();
        if (_feetApplyCache.TryGetValue(key, out var cached))
            return cached;

        if (!IsIpcAvailable())
            return null;

        try
        {
            var designs = GetDesignMap();
            var designId = designs
                .FirstOrDefault(kv => string.Equals(kv.Value, key, StringComparison.OrdinalIgnoreCase))
                .Key;

            if (designId == Guid.Empty)
                return null;

            var designJson = GetDesignJObject(designId);
            if (designJson == null)
                return null;

            var applyToken = designJson.SelectToken("Equipment.Feet.Apply");
            var appliesFeet = applyToken?.Type == JTokenType.Boolean && applyToken.ToObject<bool>();
            _feetApplyCache[key] = appliesFeet;
            return appliesFeet;
        }
        catch
        {
            return null;
        }
    }

    private Dictionary<Guid, string> GetDesignMap()
    {
        var subscriber = _pluginInterface.GetIpcSubscriber<Dictionary<Guid, string>>(GetDesignListGate);
        return subscriber.InvokeFunc() ?? [];
    }

    private static JObject? ParseDesignJObject(object? raw)
    {
        if (raw == null)
            return null;

        if (raw is JObject jobject)
            return jobject;

        if (raw is string json)
            return JObject.Parse(json);

        return JObject.FromObject(raw);
    }

    private JObject? GetDesignJObject(Guid designId)
    {
        var subscriber = _pluginInterface.GetIpcSubscriber<Guid, object>(GetDesignJObjectGate);
        return ParseDesignJObject(subscriber.InvokeFunc(designId));
    }
}
