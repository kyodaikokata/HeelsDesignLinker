using Dalamud.Plugin;
using Newtonsoft.Json;

namespace HeelsDesignLinker;

internal enum SoundMixerIpcEc
{
    Success = 0,
    InvalidArgument = 1,
    NotFound = 2,
    InvalidTag = 3,
    Unknown = 4,
}

internal sealed record SoundMixerGroupItem(
    string Id,
    string Name,
    float EffectiveVolume);

/// <summary>
/// SoundMixer IPC：临时预设切换与临时单组音量。
/// </summary>
internal sealed record SoundMixerIpcGateProbe(string Gate, bool Available, string? Error);

internal sealed class SoundMixerInterop
{
    public const string IpcTag = "HeelsDesignLinker";
    public const int DefaultPriority = 0;

    /// <summary>与 SoundMixer.Api.SoundMixerIpcGates.ApiVersion 一致。</summary>
    public const int ExpectedApiVersion = 1;

    /// <summary>线性倍率上限，与 SoundMixer Configuration.EngineAudibleCap（350%）一致。</summary>
    public const float EngineAudibleCap = 3.5f;

    public const float VolumePercentMin = 0f;
    public const float VolumePercentMax = EngineAudibleCap * 100f;

    private const string GetApiVersionGate = "SoundMixer.GetApiVersion";
    private const string GetEnabledGate = "SoundMixer.GetEnabled";
    private const string GetSavedEnabledGate = "SoundMixer.GetSavedEnabled";
    private const string GetSavedActivePresetNameGate = "SoundMixer.GetSavedActivePresetName";
    private const string GetPresetNamesGate = "SoundMixer.GetPresetNames";
    private const string GetActivePresetNameGate = "SoundMixer.GetActivePresetName";
    private const string GetGroupsJsonGate = "SoundMixer.GetGroupsJson";
    private const string GetGroupVolumeGate = "SoundMixer.GetGroupVolume";
    private const string SetTemporaryPresetGate = "SoundMixer.SetTemporaryPreset";
    private const string SetTemporaryGroupVolumeGate = "SoundMixer.SetTemporaryGroupVolume";
    private const string RemoveTemporaryOverridesGate = "SoundMixer.RemoveTemporaryOverrides";

    private readonly IDalamudPluginInterface _pluginInterface;
    private string[] _presetNames = [];
    private List<SoundMixerGroupItem> _groups = [];
    private DateTime _dataFetchedUtc = DateTime.MinValue;
    private static readonly TimeSpan DataRefreshInterval = TimeSpan.FromSeconds(30);

    public SoundMixerInterop(IDalamudPluginInterface pluginInterface)
    {
        _pluginInterface = pluginInterface;
    }

    public static bool IsSoundMixerLoaded(IDalamudPluginInterface pluginInterface) =>
        pluginInterface.InstalledPlugins.Any(p =>
            p.InternalName == "SoundMixer" && p.IsLoaded);

    public bool IsIpcAvailable()
    {
        if (!IsSoundMixerLoaded(_pluginInterface))
            return false;

        return TryGetApiVersion(out _);
    }

    public bool TryGetApiVersion(out int apiVersion)
    {
        apiVersion = 0;
        if (!IsSoundMixerLoaded(_pluginInterface))
            return false;

        try
        {
            apiVersion = _pluginInterface.GetIpcSubscriber<int>(GetApiVersionGate).InvokeFunc();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool IsApiVersionCompatible(int apiVersion) => apiVersion == ExpectedApiVersion;

    /// <summary>调试页用：探测 SoundMixer 只读 IPC gate 是否可调用。</summary>
    public IReadOnlyList<SoundMixerIpcGateProbe> ProbeReadOnlyIpcGates()
    {
        var probes = new List<SoundMixerIpcGateProbe>();
        if (!IsSoundMixerLoaded(_pluginInterface))
            return probes;

        ProbeGate(probes, GetApiVersionGate, () =>
        {
            _ = _pluginInterface.GetIpcSubscriber<int>(GetApiVersionGate).InvokeFunc();
        });
        ProbeGate(probes, GetEnabledGate, () =>
        {
            _ = _pluginInterface.GetIpcSubscriber<bool>(GetEnabledGate).InvokeFunc();
        });
        ProbeGate(probes, GetSavedEnabledGate, () =>
        {
            _ = _pluginInterface.GetIpcSubscriber<bool>(GetSavedEnabledGate).InvokeFunc();
        });
        ProbeGate(probes, GetPresetNamesGate, () =>
        {
            _ = _pluginInterface.GetIpcSubscriber<string[]>(GetPresetNamesGate).InvokeFunc();
        });
        ProbeGate(probes, GetActivePresetNameGate, () =>
        {
            _ = _pluginInterface.GetIpcSubscriber<string>(GetActivePresetNameGate).InvokeFunc();
        });
        ProbeGate(probes, GetSavedActivePresetNameGate, () =>
        {
            _ = _pluginInterface.GetIpcSubscriber<string>(GetSavedActivePresetNameGate).InvokeFunc();
        });
        ProbeGate(probes, GetGroupsJsonGate, () =>
        {
            _ = _pluginInterface.GetIpcSubscriber<string>(GetGroupsJsonGate).InvokeFunc();
        });

        var groups = GetGroups(force: true);
        var sampleGroup = groups.FirstOrDefault()?.Name ?? groups.FirstOrDefault()?.Id;
        if (!string.IsNullOrWhiteSpace(sampleGroup))
        {
            ProbeGate(probes, GetGroupVolumeGate, () =>
            {
                _ = _pluginInterface.GetIpcSubscriber<string, (int, float)>(GetGroupVolumeGate)
                    .InvokeFunc(sampleGroup!);
            });
        }
        else
        {
            probes.Add(new SoundMixerIpcGateProbe(GetGroupVolumeGate, false, "No group available to probe"));
        }

        return probes;
    }

    private static void ProbeGate(List<SoundMixerIpcGateProbe> probes, string gate, Action invoke)
    {
        try
        {
            invoke();
            probes.Add(new SoundMixerIpcGateProbe(gate, true, null));
        }
        catch (Exception ex)
        {
            probes.Add(new SoundMixerIpcGateProbe(gate, false, ex.Message));
        }
    }

    public void RefreshData(bool force = false)
    {
        _ = GetPresetNames(force);
        _ = GetGroups(force);
    }

    public IReadOnlyList<string> GetPresetNames(bool force = false)
    {
        if (!force
            && _presetNames.Length > 0
            && DateTime.UtcNow - _dataFetchedUtc < DataRefreshInterval)
        {
            return _presetNames;
        }

        _presetNames = [];
        if (!IsIpcAvailable())
            return _presetNames;

        try
        {
            _presetNames = _pluginInterface.GetIpcSubscriber<string[]>(GetPresetNamesGate).InvokeFunc() ?? [];
            _presetNames = _presetNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            _dataFetchedUtc = DateTime.UtcNow;
        }
        catch
        {
            _presetNames = [];
        }

        return _presetNames;
    }

    public IReadOnlyList<SoundMixerGroupItem> GetGroups(bool force = false)
    {
        if (!force
            && _groups.Count > 0
            && DateTime.UtcNow - _dataFetchedUtc < DataRefreshInterval)
        {
            return _groups;
        }

        _groups = [];
        if (!IsIpcAvailable())
            return _groups;

        try
        {
            var json = _pluginInterface.GetIpcSubscriber<string>(GetGroupsJsonGate).InvokeFunc() ?? "[]";
            var raw = JsonConvert.DeserializeObject<List<SoundMixerGroupDto>>(json) ?? [];
            _groups = raw
                .Where(group => !string.IsNullOrWhiteSpace(group.Name) || !string.IsNullOrWhiteSpace(group.Id))
                .Select(group => new SoundMixerGroupItem(
                    group.Id ?? "",
                    string.IsNullOrWhiteSpace(group.Name) ? group.Id ?? "" : group.Name,
                    group.EffectiveVolume))
                .OrderBy(group => group.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            _dataFetchedUtc = DateTime.UtcNow;
        }
        catch
        {
            _groups = [];
        }

        return _groups;
    }

    public bool TrySetTemporaryPreset(
        string presetNameOrId,
        int priority,
        out SoundMixerIpcEc result,
        out string error)
    {
        result = SoundMixerIpcEc.Unknown;
        error = "";
        if (!IsIpcAvailable())
        {
            error = "SoundMixer IPC unavailable";
            return false;
        }

        if (string.IsNullOrWhiteSpace(presetNameOrId))
        {
            error = "Preset not selected";
            result = SoundMixerIpcEc.InvalidArgument;
            return false;
        }

        try
        {
            var subscriber = _pluginInterface.GetIpcSubscriber<string, int, string, int>(
                SetTemporaryPresetGate);
            var ec = subscriber.InvokeFunc(IpcTag, priority, presetNameOrId.Trim());
            result = (SoundMixerIpcEc)ec;
            if (result == SoundMixerIpcEc.Success)
                return true;

            error = DescribeResult(result);
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool TrySetTemporaryGroupVolume(
        string groupIdOrName,
        float volume,
        int priority,
        out SoundMixerIpcEc result,
        out string error)
    {
        result = SoundMixerIpcEc.Unknown;
        error = "";
        if (!IsIpcAvailable())
        {
            error = "SoundMixer IPC unavailable";
            return false;
        }

        if (string.IsNullOrWhiteSpace(groupIdOrName))
        {
            error = "Group not selected";
            result = SoundMixerIpcEc.InvalidArgument;
            return false;
        }

        try
        {
            var subscriber = _pluginInterface.GetIpcSubscriber<string, int, string, float, int>(
                SetTemporaryGroupVolumeGate);
            var ec = subscriber.InvokeFunc(IpcTag, priority, groupIdOrName.Trim(), volume);
            result = (SoundMixerIpcEc)ec;
            if (result == SoundMixerIpcEc.Success)
                return true;

            error = DescribeResult(result);
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool TryRemoveTemporaryOverrides(out SoundMixerIpcEc result)
    {
        result = SoundMixerIpcEc.Unknown;
        if (!IsIpcAvailable())
            return false;

        try
        {
            var subscriber = _pluginInterface.GetIpcSubscriber<string, int>(RemoveTemporaryOverridesGate);
            result = (SoundMixerIpcEc)subscriber.InvokeFunc(IpcTag);
            return result == SoundMixerIpcEc.Success;
        }
        catch
        {
            return false;
        }
    }

    public bool? IsTemporaryPresetActive(string presetNameOrId)
    {
        if (!IsIpcAvailable() || string.IsNullOrWhiteSpace(presetNameOrId))
            return null;

        try
        {
            var active = _pluginInterface.GetIpcSubscriber<string>(GetActivePresetNameGate).InvokeFunc() ?? "";
            return string.Equals(active.Trim(), presetNameOrId.Trim(), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return null;
        }
    }

    public bool? IsTemporaryGroupVolumeActive(string groupIdOrName, float targetVolume)
    {
        if (!IsIpcAvailable() || string.IsNullOrWhiteSpace(groupIdOrName))
            return null;

        try
        {
            var subscriber = _pluginInterface.GetIpcSubscriber<string, (int, float)>(GetGroupVolumeGate);
            var (ec, volume) = subscriber.InvokeFunc(groupIdOrName.Trim());
            if (ec != (int)SoundMixerIpcEc.Success)
                return null;

            return Math.Abs(volume - targetVolume) < 0.001f;
        }
        catch
        {
            return null;
        }
    }

    private static string DescribeResult(SoundMixerIpcEc result) =>
        result switch
        {
            SoundMixerIpcEc.InvalidArgument => "Invalid argument",
            SoundMixerIpcEc.NotFound => "Preset or group not found",
            SoundMixerIpcEc.InvalidTag => "Invalid IPC tag",
            _ => $"SoundMixer error ({result})",
        };

    private sealed class SoundMixerGroupDto
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public float EffectiveVolume { get; set; }
    }
}
