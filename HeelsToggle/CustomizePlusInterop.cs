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
        {
            Plugin.PluginLog.Debug("[HeelsDesignLinker][CustomizePlus] IPC 不可用，无法获取配置列表");
            return _profileNames;
        }

        try
        {
            var profiles = GetProfileList();
            Plugin.PluginLog.Information($"[HeelsDesignLinker][CustomizePlus] 成功获取 {profiles.Count} 个配置");
            
            // 同时构建名称列表和名称->GUID映射
            foreach (var profile in profiles)
            {
                if (!string.IsNullOrWhiteSpace(profile.name))
                {
                    _profileNameToGuid[profile.name] = profile.guid;
                }
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
        {
            Plugin.PluginLog.Warning($"[HeelsDesignLinker][CustomizePlus] SetProfileEnabled: IPC 不可用");
            return false;
        }

        // 确保配置列表已加载
        if (_profileNameToGuid.Count == 0)
        {
            GetProfileNames(force: true);
        }

        // 查找配置的 GUID
        if (!_profileNameToGuid.TryGetValue(profileName, out var profileGuid))
        {
            Plugin.PluginLog.Warning($"[HeelsDesignLinker][CustomizePlus] SetProfileEnabled: 找不到配置 '{profileName}'");
            return false;
        }

        // 尝试多个可能的 IPC 方法名
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
                // 尝试 (Guid, bool) 签名
                var subscriber = _pluginInterface.GetIpcSubscriber<Guid, bool, object>(gate);
                subscriber.InvokeAction(profileGuid, enabled);
                Plugin.PluginLog.Information($"[HeelsDesignLinker][CustomizePlus] 成功使用 IPC '{gate}' 设置配置 '{profileName}' 启用状态为 {enabled}");
                return true;
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"[HeelsDesignLinker][CustomizePlus] IPC '{gate}' 失败: {ex.Message}");
            }
        }

        // 如果所有方法都失败，尝试只传 Guid 的 Enable/Disable 方法
        try
        {
            var gate = enabled ? "CustomizePlus.Profile.Enable" : "CustomizePlus.Profile.Disable";
            var subscriber = _pluginInterface.GetIpcSubscriber<Guid, object>(gate);
            subscriber.InvokeAction(profileGuid);
            Plugin.PluginLog.Information($"[HeelsDesignLinker][CustomizePlus] 成功使用 IPC '{gate}' 设置配置 '{profileName}'");
            return true;
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Error($"[HeelsDesignLinker][CustomizePlus] SetProfileEnabled 所有尝试均失败，最后错误: {ex.Message}");
            Plugin.PluginLog.Warning($"[HeelsDesignLinker][CustomizePlus] Customize+ 可能不支持通过 IPC 启用/禁用配置");
            Plugin.PluginLog.Warning($"[HeelsDesignLinker][CustomizePlus] 请检查 Customize+ 版本或手动在 Customize+ 中管理配置启用状态");
            return false;
        }
    }

    /// <summary>
    /// 应用配置（通过启用指定配置）
    /// </summary>
    public bool ApplyProfile(string profileName, string characterName)
    {
        Plugin.PluginLog.Information($"[HeelsDesignLinker][CustomizePlus] 尝试启用配置: '{profileName}'");
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
            // Customize+ Profile.GetList 返回：
            // IList<(Guid, string, string, List<object>, int, bool)>
            // 元组元素：(uniqueId, name, path, characters, priority, enabled)
            var subscriber = _pluginInterface.GetIpcSubscriber<List<(Guid, string, string, object, int, bool)>>(GetProfileListGate);
            var profiles = subscriber.InvokeFunc();
            
            if (profiles == null || profiles.Count == 0)
                return [];
            
            // 提取配置的 GUID 和名称
            return profiles
                .Where(p => !string.IsNullOrWhiteSpace(p.Item2))
                .Select(p => (guid: p.Item1, name: p.Item2)) // Item1 是 uniqueId (Guid), Item2 是 name
                .ToList();
        }
        catch (Exception ex)
        {
            Plugin.PluginLog.Warning($"[HeelsDesignLinker][CustomizePlus] GetProfileList 失败: {ex.Message}");
            return [];
        }
    }
}
