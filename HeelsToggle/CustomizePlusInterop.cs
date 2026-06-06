using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace HeelsDesignLinker;

/// <summary>
/// 通过 Customize+ IPC 获取配置列表并启用/禁用配置。
/// </summary>
internal sealed class CustomizePlusInterop
{
    private const string GetProfileListGate = "CustomizePlus.Profile.GetList";
    private const string GetEnabledStateGate = "CustomizePlus.GetEnabled";
    private const string SetProfileEnabledGate = "CustomizePlus.Profile.SetEnabled";

    private readonly IDalamudPluginInterface _pluginInterface;
    private List<string> _profileNames = [];
    private Dictionary<string, Guid> _profileNameToGuid = new();
    private DateTime _profileListFetchedUtc = DateTime.MinValue;
    private static readonly TimeSpan ProfileListRefreshInterval = TimeSpan.FromSeconds(30);

    public CustomizePlusInterop(IDalamudPluginInterface pluginInterface)
    {
        _pluginInterface = pluginInterface;
    }

    public static bool IsCustomizePlusLoaded(IDalamudPluginInterface pluginInterface)
    {
        return pluginInterface.InstalledPlugins.Any(p =>
            (p.InternalName == "CustomizePlus" || p.InternalName == "Customize+") && p.IsLoaded);
    }

    public bool IsIpcAvailable()
    {
        if (!IsCustomizePlusLoaded(_pluginInterface))
            return false;

        try
        {
            _ = GetProfileList();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void RefreshProfileList(bool force = false)
    {
        _ = GetProfileNames(force);
    }

    public IReadOnlyList<string> GetProfileNames(bool force = false)
    {
        if (!force
            && _profileNames.Count > 0
            && (DateTime.UtcNow - _profileListFetchedUtc) < ProfileListRefreshInterval)
        {
            return _profileNames;
        }

        _profileNames = [];
        _profileNameToGuid.Clear();
        if (!IsIpcAvailable())
            return _profileNames;

        try
        {
            var profiles = GetProfileList();

            foreach (var profile in profiles)
            {
                if (!string.IsNullOrWhiteSpace(profile.name))
                    _profileNameToGuid[profile.name] = profile.guid;
            }

            _profileNames = _profileNameToGuid.Keys
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            _profileListFetchedUtc = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Error($"[HeelsDesignLinker][CustomizePlus] GetProfileNames 失败: {ex}");
            _profileNames = [];
            _profileNameToGuid.Clear();
        }

        return _profileNames;
    }

    /// <summary>
    /// 启用或禁用指定的 Customize+ 配置
    /// </summary>
    /// <param name="profileName">配置名称</param>
    /// <param name="enabled">true = 启用, false = 禁用</param>
    /// <returns>是否成功</returns>
    public bool SetProfileEnabled(string profileName, bool enabled)
    {
        if (!IsIpcAvailable())
            return false;

        if (_profileNameToGuid.Count == 0)
            GetProfileNames(force: true);

        if (!_profileNameToGuid.TryGetValue(profileName, out var profileGuid))
            return false;

        var possibleGates = new[]
        {
            "CustomizePlus.Profile.SetEnabled",
            "CustomizePlus.SetProfileEnabled",
            "CustomizePlus.Profile.Enable",
            "CustomizePlus.Profile.SetState",
            "CustomizePlus.Profile.Toggle"
        };

        foreach (var gate in possibleGates)
        {
            try
            {
                var subscriber = _pluginInterface.GetIpcSubscriber<Guid, bool, object>(gate);
                subscriber.InvokeAction(profileGuid, enabled);
                return true;
            }
            catch
            {
            }
        }

        try
        {
            var gate = enabled ? "CustomizePlus.Profile.Enable" : "CustomizePlus.Profile.Disable";
            var subscriber = _pluginInterface.GetIpcSubscriber<Guid, object>(gate);
            subscriber.InvokeAction(profileGuid);
            return true;
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Error($"[HeelsDesignLinker][CustomizePlus] SetProfileEnabled 失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 应用配置（通过启用指定配置）
    /// </summary>
    public bool ApplyProfile(string profileName, string characterName)
    {
        return SetProfileEnabled(profileName, true);
    }

    public bool? GetEnabledState()
    {
        if (!IsIpcAvailable())
            return null;

        try
        {
            var subscriber = _pluginInterface.GetIpcSubscriber<bool>(GetEnabledStateGate);
            return subscriber.InvokeFunc();
        }
        catch
        {
            return null;
        }
    }

    private List<(Guid guid, string name)> GetProfileList()
    {
        try
        {
            var subscriber = _pluginInterface.GetIpcSubscriber<List<(Guid, string, string, object, int, bool)>>(GetProfileListGate);
            var profiles = subscriber.InvokeFunc();

            if (profiles == null || profiles.Count == 0)
                return [];

            return profiles
                .Where(p => !string.IsNullOrWhiteSpace(p.Item2))
                .Select(p => (guid: p.Item1, name: p.Item2))
                .ToList();
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Error($"[HeelsDesignLinker][CustomizePlus] GetProfileList 失败: {ex.Message}");
            return [];
        }
    }
}
