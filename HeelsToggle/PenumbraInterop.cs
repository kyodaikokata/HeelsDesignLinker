using System.Collections;
using System.Reflection;
using System;
using Dalamud.Plugin;
using Newtonsoft.Json.Linq;

namespace HeelsToggle;

/// <summary>
/// Penumbra IPC 返回码（与 Penumbra.Api.Enums.PenumbraApiEc 数值一致，避免引用 NuGet 包）。
/// </summary>
internal enum PenumbraIpcEc
{
    Success = 0,
    NothingChanged = 1,
    CollectionMissing = 2,
    ModMissing = 3,
    OptionGroupMissing = 4,
    OptionMissing = 5,
    UnknownError = 255,
}

internal sealed record PenumbraModEntry(string Directory, string DisplayName);

/// <summary>与 Penumbra.Api.Enums.GroupType 数值一致。</summary>
public enum PenumbraGroupType
{
    Single = 0,
    Multi = 1,
    Imc = 2,
    Combining = 3,
}

internal sealed record PenumbraOptionGroupInfo(
    IReadOnlyList<string> Options,
    PenumbraGroupType GroupType);

/// <summary>
/// 通过 Penumbra IPC 切换 Mod 选项，避免 <c>/penumbra mod setting</c> 在聊天栏留日志。
/// </summary>
internal sealed class PenumbraInterop
{
    private const string GetCollectionsGate = "Penumbra.GetCollections.V5";
    private const string GetModListGate = "Penumbra.GetModList";
    private const string GetAvailableModSettingsGate = "Penumbra.GetAvailableModSettings.V5";
    private const string GetCurrentModSettingsGate = "Penumbra.GetCurrentModSettings.V5";
    private const string TrySetModSettingGate = "Penumbra.TrySetModSetting.V5";
    private const string TrySetModSettingsGate = "Penumbra.TrySetModSettings.V5";
    private const string RemoveTemporaryModSettingsPlayerGate = "Penumbra.RemoveTemporaryModSettingsPlayer.V5";
    private const string QueryTemporaryModSettingsPlayerGate = "Penumbra.QueryTemporaryModSettingsPlayer.V5";

    /// <summary>Glamourer 自动化临时设置的 Lock Key（与 Glamourer 源码一致）。</summary>
    private const int GlamourerKeyAutomation = -1610;

    /// <summary>Glamourer 手动临时设置的 Lock Key（与 Glamourer 源码一致）。</summary>
    private const int GlamourerKeyManual = -6160;

    private readonly IDalamudPluginInterface _pluginInterface;
    private List<string> _collectionNames = [];
    private List<PenumbraModEntry> _mods = [];
    private Dictionary<string, Dictionary<string, PenumbraOptionGroupInfo>> _modSettingsByDirectory =
        new(StringComparer.OrdinalIgnoreCase);
    private DateTime _dataFetchedUtc = DateTime.MinValue;
    private static readonly TimeSpan DataRefreshInterval = TimeSpan.FromSeconds(30);

    private string _takeoverCacheMod = "";
    private int _takeoverCachePlayerIndex = -1;
    private bool _takeoverCacheResult;
    private DateTime _takeoverCacheUtc = DateTime.MinValue;
    private static readonly TimeSpan TakeoverCacheInterval = TimeSpan.FromSeconds(1);

    public PenumbraInterop(IDalamudPluginInterface pluginInterface)
    {
        _pluginInterface = pluginInterface;
    }

    public static bool IsPenumbraLoaded(IDalamudPluginInterface pluginInterface)
    {
        return pluginInterface.InstalledPlugins.Any(p =>
            p.InternalName == "Penumbra" && p.IsLoaded);
    }

    public bool IsIpcAvailable()
    {
        if (!IsPenumbraLoaded(_pluginInterface))
            return false;

        try
        {
            _ = GetCollections();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void RefreshData(bool force = false)
    {
        _ = GetCollectionNames(force);
        _ = GetMods(force);
    }

    public void InvalidateModSettingsCache(string? modDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(modDirectory))
        {
            _modSettingsByDirectory.Clear();
            return;
        }

        _modSettingsByDirectory.Remove(modDirectory.Trim());
    }

    public IReadOnlyList<string> GetCollectionNames(bool force = false)
    {
        if (!force
            && _collectionNames.Count > 0
            && (DateTime.UtcNow - _dataFetchedUtc) < DataRefreshInterval)
        {
            return _collectionNames;
        }

        _collectionNames = [];
        if (!IsIpcAvailable())
            return _collectionNames;

        try
        {
            _collectionNames = GetCollections().Values
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            _dataFetchedUtc = DateTime.UtcNow;
        }
        catch
        {
            _collectionNames = [];
        }

        return _collectionNames;
    }

    public IReadOnlyList<PenumbraModEntry> GetMods(bool force = false)
    {
        if (!force
            && _mods.Count > 0
            && (DateTime.UtcNow - _dataFetchedUtc) < DataRefreshInterval)
        {
            return _mods;
        }

        _mods = [];
        if (!IsIpcAvailable())
            return _mods;

        try
        {
            var subscriber = _pluginInterface.GetIpcSubscriber<Dictionary<string, string>>(GetModListGate);
            var raw = subscriber.InvokeFunc() ?? [];
            _mods = raw
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Key))
                .Select(kv => new PenumbraModEntry(kv.Key, string.IsNullOrWhiteSpace(kv.Value) ? kv.Key : kv.Value))
                .OrderBy(m => m.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(m => m.Directory, StringComparer.OrdinalIgnoreCase)
                .ToList();
            _dataFetchedUtc = DateTime.UtcNow;
        }
        catch
        {
            _mods = [];
        }

        return _mods;
    }

    public IReadOnlyList<string> GetOptionGroups(string modDirectory, bool force = false)
    {
        if (string.IsNullOrWhiteSpace(modDirectory))
            return [];

        var settings = GetModSettingsMap(modDirectory, force);
        return settings.Keys
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<string> GetOptionNames(string modDirectory, string optionGroup, bool force = false)
    {
        if (string.IsNullOrWhiteSpace(modDirectory) || string.IsNullOrWhiteSpace(optionGroup))
            return [];

        var settings = GetModSettingsMap(modDirectory, force);
        if (!settings.TryGetValue(optionGroup, out var groupInfo))
            return [];

        return groupInfo.Options;
    }

    public PenumbraGroupType GetOptionGroupType(string modDirectory, string optionGroup, bool force = false)
    {
        if (string.IsNullOrWhiteSpace(modDirectory) || string.IsNullOrWhiteSpace(optionGroup))
            return PenumbraGroupType.Single;

        var settings = GetModSettingsMap(modDirectory, force);
        return settings.TryGetValue(optionGroup, out var groupInfo)
            ? groupInfo.GroupType
            : PenumbraGroupType.Single;
    }

    public bool IsModUnderGlamourerTakeover(int playerObjectIndex, string modDirectory)
    {
        if (!IsIpcAvailable() || string.IsNullOrWhiteSpace(modDirectory))
            return false;

        var key = modDirectory.Trim();
        var now = DateTime.UtcNow;
        if (_takeoverCachePlayerIndex == playerObjectIndex
            && string.Equals(_takeoverCacheMod, key, StringComparison.OrdinalIgnoreCase)
            && (now - _takeoverCacheUtc) < TakeoverCacheInterval)
        {
            return _takeoverCacheResult;
        }

        _takeoverCacheResult = QueryGlamourerTakeover(playerObjectIndex, key);
        _takeoverCacheMod = key;
        _takeoverCachePlayerIndex = playerObjectIndex;
        _takeoverCacheUtc = now;
        return _takeoverCacheResult;
    }

    public void InvalidateTakeoverCache()
    {
        _takeoverCacheMod = "";
        _takeoverCachePlayerIndex = -1;
        _takeoverCacheUtc = DateTime.MinValue;
    }

    public bool TryRemoveGlamourerTemporaryModSettings(string modDirectory, int playerObjectIndex, out string message)
    {
        message = "";
        if (!IsIpcAvailable())
        {
            message = "Penumbra IPC unavailable";
            return false;
        }

        if (string.IsNullOrWhiteSpace(modDirectory))
        {
            message = "Mod not selected";
            return false;
        }

        try
        {
            var subscriber = _pluginInterface.GetIpcSubscriber<int, string, string, int, int>(
                RemoveTemporaryModSettingsPlayerGate);

            var removedAny = false;
            string? lastError = null;
            foreach (var key in new[] { GlamourerKeyAutomation, GlamourerKeyManual })
            {
                var result = (PenumbraIpcEc)subscriber.InvokeFunc(
                    playerObjectIndex,
                    modDirectory.Trim(),
                    string.Empty,
                    key);

                if (result is PenumbraIpcEc.Success or PenumbraIpcEc.NothingChanged)
                    removedAny = true;
                else
                    lastError = result.ToString();
            }

            if (removedAny)
            {
                InvalidateTakeoverCache();
                message = "OK";
                return true;
            }

            message = lastError ?? "No Glamourer temporary settings found";
            return false;
        }
        catch (Exception ex)
        {
            message = $"Penumbra IPC: {ex.Message}";
            return false;
        }
    }

    public static bool UsesBoolOptionValue(PenumbraGroupType groupType) =>
        groupType is PenumbraGroupType.Multi or PenumbraGroupType.Imc or PenumbraGroupType.Combining;

    public static string BuildApplyStateKey(string optionName, PenumbraGroupType groupType, bool optionEnabled) =>
        UsesBoolOptionValue(groupType)
            ? $"{optionName.Trim()}|{optionEnabled}"
            : optionName.Trim();

    public bool TryApplyMultiToggleSettings(
        string collectionName,
        string modDirectory,
        string optionGroupName,
        IReadOnlyList<string> enabledOptionNames,
        out PenumbraIpcEc result,
        out string error)
    {
        result = PenumbraIpcEc.UnknownError;
        error = "";

        if (string.IsNullOrWhiteSpace(collectionName)
            || string.IsNullOrWhiteSpace(modDirectory)
            || string.IsNullOrWhiteSpace(optionGroupName))
        {
            error = "Penumbra 配置不完整（Collection / Mod / Group）";
            return false;
        }

        if (!TryResolveCollectionId(collectionName, out var collectionId, out error))
            return false;

        var modName = ResolveModDisplayName(modDirectory);
        var trimmedGroupName = optionGroupName.Trim();
        var trimmedModDirectory = modDirectory.Trim();
        var optionNames = enabledOptionNames
            .Select(name => name.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        try
        {
            var subscriber = _pluginInterface.GetIpcSubscriber<Guid, string, string, string, string[], object>(
                TrySetModSettingsGate);
            var ipcResult = subscriber.InvokeFunc(
                collectionId,
                trimmedModDirectory,
                modName,
                trimmedGroupName,
                optionNames);

            if (!TryConvertPenumbraIpcEc(ipcResult, out result))
            {
                error = $"Penumbra IPC: {ipcResult}";
                return false;
            }

            if (IsPenumbraIpcSuccess(result))
                return true;

            error = $"Penumbra IPC: {result}";
            return false;
        }
        catch (Exception ex)
        {
            error = $"Penumbra IPC 调用失败: {ex.Message}";
            return false;
        }
    }

    public bool TryApplyModSetting(
        string collectionName,
        string modDirectory,
        string optionGroupName,
        string optionName,
        bool optionEnabled,
        out PenumbraIpcEc result,
        out string error)
    {
        result = PenumbraIpcEc.UnknownError;
        error = "";

        if (string.IsNullOrWhiteSpace(collectionName)
            || string.IsNullOrWhiteSpace(modDirectory)
            || string.IsNullOrWhiteSpace(optionGroupName)
            || string.IsNullOrWhiteSpace(optionName))
        {
            error = "Penumbra 配置不完整（Collection / Mod / Group / Option）";
            return false;
        }

        if (!TryResolveCollectionId(collectionName, out var collectionId, out error))
            return false;

        var modName = ResolveModDisplayName(modDirectory);
        var groupType = GetOptionGroupType(modDirectory, optionGroupName);
        var trimmedOptionName = optionName.Trim();
        var trimmedGroupName = optionGroupName.Trim();
        var trimmedModDirectory = modDirectory.Trim();

        try
        {
            if (UsesBoolOptionValue(groupType))
            {
                string[] optionNames;
                if (optionEnabled)
                {
                    optionNames = [trimmedOptionName];
                }
                else if (!TryGetCurrentGroupOptionNames(
                             collectionId,
                             trimmedModDirectory,
                             modName,
                             trimmedGroupName,
                             out var currentOptions,
                             out error))
                {
                    result = PenumbraIpcEc.UnknownError;
                    return false;
                }
                else
                {
                    var targetWasEnabled = currentOptions.Any(name =>
                        string.Equals(name, trimmedOptionName, StringComparison.OrdinalIgnoreCase));
                    if (!targetWasEnabled)
                    {
                        result = PenumbraIpcEc.NothingChanged;
                        return true;
                    }

                    optionNames = currentOptions
                        .Where(name => !string.Equals(name, trimmedOptionName, StringComparison.OrdinalIgnoreCase))
                        .ToArray();
                }

                var subscriber = _pluginInterface.GetIpcSubscriber<Guid, string, string, string, string[], object>(
                    TrySetModSettingsGate);
                var ipcResult = subscriber.InvokeFunc(
                    collectionId,
                    trimmedModDirectory,
                    modName,
                    trimmedGroupName,
                    optionNames);

                if (!TryConvertPenumbraIpcEc(ipcResult, out result))
                {
                    error = $"Penumbra IPC: {ipcResult}";
                    return false;
                }
            }
            else
            {
                var subscriber = _pluginInterface.GetIpcSubscriber<Guid, string, string, string, string, object>(
                    TrySetModSettingGate);
                var ipcResult = subscriber.InvokeFunc(
                    collectionId,
                    trimmedModDirectory,
                    modName,
                    trimmedGroupName,
                    trimmedOptionName);

                if (!TryConvertPenumbraIpcEc(ipcResult, out result))
                {
                    error = $"Penumbra IPC: {ipcResult}";
                    return false;
                }
            }

            if (IsPenumbraIpcSuccess(result))
                return true;

            error = $"Penumbra IPC: {result}";
            return false;
        }
        catch (Exception ex)
        {
            error = $"Penumbra IPC 调用失败: {ex.Message}";
            return false;
        }
    }

    private bool TryGetCurrentGroupOptionNames(
        Guid collectionId,
        string modDirectory,
        string modName,
        string optionGroupName,
        out List<string> optionNames,
        out string error)
    {
        optionNames = [];
        error = "";

        try
        {
            var subscriber = _pluginInterface.GetIpcSubscriber<Guid, string, string, bool, object>(
                GetCurrentModSettingsGate);
            var raw = subscriber.InvokeFunc(collectionId, modDirectory, modName, false);
            if (!TryParseCurrentModSettings(raw, out var settingsByGroup, out var parseError))
            {
                error = parseError;
                return false;
            }

            if (!settingsByGroup.TryGetValue(optionGroupName, out var names))
            {
                optionNames = [];
                return true;
            }

            optionNames = names
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return true;
        }
        catch (Exception ex)
        {
            error = $"读取 Penumbra 当前设置失败: {ex.Message}";
            return false;
        }
    }

    private static bool TryParseCurrentModSettings(
        object? raw,
        out Dictionary<string, List<string>> settingsByGroup,
        out string error)
    {
        settingsByGroup = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        error = "";

        if (raw == null)
            return true;

        if (!TryUnwrapIpcResult(raw, out var payload, out var resultCode))
        {
            error = "无法解析 Penumbra 当前设置返回值";
            return false;
        }

        if (resultCode is not null)
        {
            if (!TryConvertPenumbraIpcEc(resultCode, out var ec) || !IsPenumbraIpcSuccess(ec))
            {
                error = $"Penumbra IPC: {(TryConvertPenumbraIpcEc(resultCode, out var displayEc) ? displayEc : resultCode)}";
                return false;
            }
        }

        if (payload == null)
            return true;

        if (TryParseCurrentModSettingsDictionary(payload, settingsByGroup))
            return true;

        try
        {
            var token = payload is string json ? JToken.Parse(json) : JToken.FromObject(payload);
            if (token is JObject obj)
            {
                var settingsToken = obj["Item3"] ?? obj["item3"] ?? obj["Settings"] ?? obj["settings"];
                if (settingsToken is JObject settingsObj)
                {
                    foreach (var prop in settingsObj.Properties())
                        settingsByGroup[prop.Name] = ParseOptionNameList(prop.Value);
                }
            }
        }
        catch
        {
            error = "无法解析 Penumbra 当前设置字典";
            return false;
        }

        return true;
    }

    private static bool TryParseCurrentModSettingsDictionary(
        object payload,
        Dictionary<string, List<string>> settingsByGroup)
    {
        object? settingsValue = null;

        if (payload is IDictionary dict && payload is not string)
        {
            settingsValue = GetDictionaryValue(dict, "Item3", "item3", "Settings", "settings");
            if (settingsValue == null && dict is not JToken)
            {
                foreach (DictionaryEntry entry in dict)
                {
                    if (entry.Key is not string group)
                        continue;

                    settingsByGroup[group] = ParseOptionNameList(entry.Value);
                }

                return dict.Count > 0;
            }
        }

        if (TryGetNamedTupleMembers(payload, out _, out _, out var item3) && item3 != null)
            settingsValue = item3;

        switch (settingsValue)
        {
            case IDictionary<string, List<string>> typedDict:
                foreach (var (group, names) in typedDict)
                    settingsByGroup[group] = NormalizeOptionNames(names);
                return true;
            case IReadOnlyDictionary<string, List<string>> readOnlyDict:
                foreach (var (group, names) in readOnlyDict)
                    settingsByGroup[group] = NormalizeOptionNames(names);
                return true;
            case IDictionary settingsDict:
                foreach (DictionaryEntry entry in settingsDict)
                {
                    if (entry.Key is not string group)
                        continue;

                    settingsByGroup[group] = ParseOptionNameList(entry.Value);
                }

                return true;
        }

        return false;
    }

    private static bool TryGetNamedTupleMembers(
        object? value,
        out object? item1,
        out object? item2,
        out object? item3)
    {
        item1 = null;
        item2 = null;
        item3 = null;

        if (value is JObject obj)
        {
            item1 = obj["Item1"] ?? obj["item1"] ?? obj["m_Item1"];
            item2 = obj["Item2"] ?? obj["item2"] ?? obj["m_Item2"];
            item3 = obj["Item3"] ?? obj["item3"] ?? obj["m_Item3"];
            return item1 != null || item2 != null || item3 != null;
        }

        if (value is IDictionary dict && value is not string)
        {
            item1 = GetDictionaryValue(dict, "Item1", "item1", "m_Item1");
            item2 = GetDictionaryValue(dict, "Item2", "item2", "m_Item2");
            item3 = GetDictionaryValue(dict, "Item3", "item3", "m_Item3");
            if (item1 != null || item2 != null || item3 != null)
                return true;
        }

        var type = value?.GetType();
        if (type == null)
            return false;

        var item1Field = type.GetField("Item1", BindingFlags.Public | BindingFlags.Instance);
        if (item1Field == null)
            return false;

        item1 = item1Field.GetValue(value);
        item2 = type.GetField("Item2", BindingFlags.Public | BindingFlags.Instance)?.GetValue(value);
        item3 = type.GetField("Item3", BindingFlags.Public | BindingFlags.Instance)?.GetValue(value);
        return true;
    }

    private static bool TryUnwrapIpcResult(object raw, out object? payload, out object? resultCode)
    {
        payload = raw;
        resultCode = null;

        if (raw is object[] arr)
        {
            if (arr.Length == 0)
                return false;

            resultCode = NormalizeIpcResultCode(arr[0]);
            payload = arr.Length > 1 ? arr[1] : null;
            return true;
        }

        if (raw is IList list && raw is not string && raw is not string[])
        {
            if (list.Count == 0)
                return false;

            resultCode = NormalizeIpcResultCode(list[0]);
            payload = list.Count > 1 ? list[1] : null;
            return true;
        }

        if (TryGetNamedTupleMembers(raw, out var item1, out var item2, out _))
        {
            resultCode = NormalizeIpcResultCode(item1);
            payload = item2;
            return true;
        }

        return true;
    }

    private static bool IsPenumbraIpcSuccess(PenumbraIpcEc ec) =>
        ec is PenumbraIpcEc.Success or PenumbraIpcEc.NothingChanged;

    private static bool TryConvertPenumbraIpcEc(object? value, out PenumbraIpcEc ec)
    {
        ec = PenumbraIpcEc.UnknownError;
        if (value == null)
            return false;

        switch (value)
        {
            case PenumbraIpcEc typed:
                ec = typed;
                return true;
            case int i:
                ec = (PenumbraIpcEc)i;
                return true;
            case long l:
                ec = (PenumbraIpcEc)(int)l;
                return true;
            case byte b:
                ec = (PenumbraIpcEc)b;
                return true;
            case Enum e:
                ec = (PenumbraIpcEc)Convert.ToInt32(e);
                return true;
            case JValue { Type: JTokenType.Integer } jv:
                ec = (PenumbraIpcEc)jv.Value<int>();
                return true;
            case JObject obj:
            {
                var item1 = obj["Item1"] ?? obj["item1"] ?? obj["m_Item1"];
                if (item1 != null)
                    return TryConvertPenumbraIpcEc(item1, out ec);
                break;
            }
        }

        if (TryGetNamedTupleMembers(value, out var tupleItem1, out _, out _) && tupleItem1 != null)
            return TryConvertPenumbraIpcEc(tupleItem1, out ec);

        return int.TryParse(value.ToString(), out var parsed)
               && TryConvertPenumbraIpcEc(parsed, out ec);
    }

    private static object? NormalizeIpcResultCode(object? value) =>
        TryConvertPenumbraIpcEc(value, out var ec) ? ec : value;

    private string ResolveModDisplayName(string modDirectory)
    {
        if (_mods.Count == 0)
            _ = GetMods();

        return _mods.FirstOrDefault(m =>
                string.Equals(m.Directory, modDirectory, StringComparison.OrdinalIgnoreCase))
            ?.DisplayName ?? string.Empty;
    }

    private Dictionary<string, PenumbraOptionGroupInfo> GetModSettingsMap(string modDirectory, bool force)
    {
        var key = modDirectory.Trim();
        if (!force
            && _modSettingsByDirectory.TryGetValue(key, out var cached)
            && (DateTime.UtcNow - _dataFetchedUtc) < DataRefreshInterval)
        {
            return cached;
        }

        var parsed = FetchAvailableModSettings(key);
        if (parsed.Count > 0)
            _modSettingsByDirectory[key] = parsed;
        else
            _modSettingsByDirectory.Remove(key);

        return parsed;
    }

    private Dictionary<string, PenumbraOptionGroupInfo> FetchAvailableModSettings(string modDirectory)
    {
        try
        {
            var subscriber = _pluginInterface.GetIpcSubscriber<string, string, object>(
                GetAvailableModSettingsGate);
            var modName = ResolveModDisplayName(modDirectory);
            var parsed = ParseAvailableModSettings(subscriber.InvokeFunc(modDirectory, modName));
            if (parsed.Count == 0)
                parsed = ParseAvailableModSettings(subscriber.InvokeFunc(modDirectory, string.Empty));

            return parsed;
        }
        catch
        {
            return new Dictionary<string, PenumbraOptionGroupInfo>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static Dictionary<string, PenumbraOptionGroupInfo> ParseAvailableModSettings(object? raw)
    {
        var result = new Dictionary<string, PenumbraOptionGroupInfo>(StringComparer.OrdinalIgnoreCase);
        raw = UnwrapAvailableModSettingsRoot(raw);
        if (raw == null)
            return result;

        if (TryParseAvailableModSettingsDictionary(raw, result))
            return result;

        try
        {
            var token = raw is string json ? JToken.Parse(json) : JToken.FromObject(raw);
            if (token is JObject obj)
            {
                foreach (var prop in UnwrapAvailableModSettingsToken(obj).Properties())
                    result[prop.Name] = ParseOptionGroupEntry(prop.Value);
            }
        }
        catch
        {
            return result;
        }

        return result;
    }

    private static bool TryParseAvailableModSettingsDictionary(
        object raw,
        Dictionary<string, PenumbraOptionGroupInfo> result)
    {
        switch (raw)
        {
            case IReadOnlyDictionary<string, (string[], int)> tupleDict:
                foreach (var (group, tuple) in tupleDict)
                {
                    result[group] = new PenumbraOptionGroupInfo(
                        NormalizeOptionNames(tuple.Item1),
                        ParseGroupType(tuple.Item2));
                }

                return tupleDict.Count > 0;
            case IDictionary<string, (string[], int)> tupleDict2:
                foreach (var (group, tuple) in tupleDict2)
                {
                    result[group] = new PenumbraOptionGroupInfo(
                        NormalizeOptionNames(tuple.Item1),
                        ParseGroupType(tuple.Item2));
                }

                return tupleDict2.Count > 0;
        }

        if (raw is IDictionary dict && raw is not string && raw is not JToken)
        {
            foreach (DictionaryEntry entry in dict)
            {
                if (entry.Key is not string group)
                    continue;

                result[group] = ParseOptionGroupEntry(entry.Value);
            }

            return dict.Count > 0;
        }

        return false;
    }

    private static object? UnwrapAvailableModSettingsRoot(object? raw)
    {
        if (raw == null)
            return null;

        var settingsProp = raw.GetType().GetProperty(
            "Settings",
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        var settingsValue = settingsProp?.GetValue(raw);
        return settingsValue ?? raw;
    }

    private static JObject UnwrapAvailableModSettingsToken(JToken token)
    {
        if (token is not JObject obj)
            return new JObject();

        var settings = obj["Settings"] ?? obj["settings"];
        return settings is JObject settingsObj ? settingsObj : obj;
    }

    private static PenumbraOptionGroupInfo ParseOptionGroupEntry(object? value)
    {
        if (value == null)
            return new PenumbraOptionGroupInfo([], PenumbraGroupType.Single);

        if (value is string[] strArr)
        {
            return new PenumbraOptionGroupInfo(
                NormalizeOptionNames(strArr),
                PenumbraGroupType.Single);
        }

        if (value is IEnumerable<string> stringEnumerable)
        {
            return new PenumbraOptionGroupInfo(
                NormalizeOptionNames(stringEnumerable),
                PenumbraGroupType.Single);
        }

        if (TryGetNamedTupleMembers(value, out var namedItem1, out var namedItem2, out _)
            && TryParseOptionGroupTuple(namedItem1, namedItem2, out var namedTupleInfo))
        {
            return namedTupleInfo;
        }

        if (value is JToken token)
        {
            if (token is JArray array)
            {
                if (array.Count == 2 && TryParseOptionGroupTuple(array[0], array[1], out var tupleInfo))
                    return tupleInfo;

                return new PenumbraOptionGroupInfo(ParseOptionNameList(array), PenumbraGroupType.Single);
            }

            if (token is JValue jv)
                return ParseOptionGroupEntry(jv.Value);
        }

        if (value is object[] objArr)
        {
            if (TryParseOptionGroupTuple(
                    objArr.Length > 0 ? objArr[0] : null,
                    objArr.Length > 1 ? objArr[1] : null,
                    out var tupleInfo))
            {
                return tupleInfo;
            }

            return new PenumbraOptionGroupInfo(ParseOptionNameList(objArr), PenumbraGroupType.Single);
        }

        // JObject/JArray 也实现 IList，但索引的是 JProperty 而非 Item1/Item2 的值。
        if (value is IList list && value is not string && value is not string[] && value is not JContainer)
        {
            if (list.Count == 2 && TryParseOptionGroupTuple(list[0], list[1], out var tupleInfo))
                return tupleInfo;

            return new PenumbraOptionGroupInfo(ParseOptionNameList(list), PenumbraGroupType.Single);
        }

        return new PenumbraOptionGroupInfo(ParseOptionNameList(value), PenumbraGroupType.Single);
    }

    private static object? GetDictionaryValue(IDictionary dict, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (dict.Contains(key))
                return dict[key];
        }

        return null;
    }

    private static bool TryParseOptionGroupTuple(object? optionsValue, object? typeValue, out PenumbraOptionGroupInfo info)
    {
        info = new PenumbraOptionGroupInfo([], PenumbraGroupType.Single);
        if (!IsLikelyGroupTypeToken(typeValue))
            return false;

        info = new PenumbraOptionGroupInfo(
            ParseOptionNameList(optionsValue),
            ParseGroupType(typeValue));
        return true;
    }

    private static bool IsLikelyGroupTypeToken(object? value)
    {
        if (value == null)
            return false;

        return value switch
        {
            int i => Enum.IsDefined(typeof(PenumbraGroupType), i),
            long l => Enum.IsDefined(typeof(PenumbraGroupType), (int)l),
            byte b => Enum.IsDefined(typeof(PenumbraGroupType), (int)b),
            JValue { Type: JTokenType.Integer } => true,
            _ => int.TryParse(value.ToString(), out var parsed)
                && Enum.IsDefined(typeof(PenumbraGroupType), parsed),
        };
    }

    private static List<string> ParseOptionNameList(object? value)
    {
        if (value == null)
            return [];

        if (value is string single && !string.IsNullOrWhiteSpace(single))
        {
            var trimmed = single.Trim();
            if (trimmed.StartsWith('[') || trimmed.StartsWith('{'))
            {
                try
                {
                    return ParseOptionNameList(JToken.Parse(trimmed));
                }
                catch
                {
                    // fall through
                }
            }

            return [single];
        }

        if (value is string[] arr)
            return NormalizeOptionNames(arr);

        if (value is IEnumerable<string> enumerable)
            return NormalizeOptionNames(enumerable);

        if (value is JToken token)
        {
            if (token is JProperty property)
                return ParseOptionNameList(property.Value);

            if (token is JObject obj)
            {
                var item1 = obj["Item1"] ?? obj["item1"] ?? obj["m_Item1"];
                if (item1 != null)
                    return ParseOptionNameList(item1);
            }

            if (token is JArray array)
            {
                return array.Select(t => t.Type == JTokenType.String ? t.Value<string>()! : t.ToString())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();
            }

            if (token is JValue jv)
                return ParseOptionNameList(jv.Value);
        }

        if (value is IEnumerable items && value is not string && value is not JToken)
        {
            var names = new List<string>();
            foreach (var item in items)
            {
                var name = item?.ToString() ?? "";
                if (!string.IsNullOrWhiteSpace(name))
                    names.Add(name);
            }

            return NormalizeOptionNames(names);
        }

        return [];
    }

    private static PenumbraGroupType ParseGroupType(object? value)
    {
        if (value == null)
            return PenumbraGroupType.Single;

        var numeric = value switch
        {
            int i => i,
            long l => (int)l,
            byte b => b,
            JValue { Type: JTokenType.Integer } jv => jv.Value<int>(),
            Enum e => Convert.ToInt32(e),
            _ => int.TryParse(value.ToString(), out var parsed) ? parsed : 0,
        };

        return Enum.IsDefined(typeof(PenumbraGroupType), numeric)
            ? (PenumbraGroupType)numeric
            : PenumbraGroupType.Single;
    }

    private static List<string> NormalizeOptionNames(IEnumerable<string> names) =>
        names.Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private bool TryResolveCollectionId(string collectionName, out Guid collectionId, out string error)
    {
        collectionId = Guid.Empty;
        error = "";

        try
        {
            foreach (var (id, name) in GetCollections())
            {
                if (string.Equals(name, collectionName, StringComparison.OrdinalIgnoreCase))
                {
                    collectionId = id;
                    return true;
                }
            }

            error = $"找不到 Penumbra Collection: {collectionName}";
            return false;
        }
        catch (Exception ex)
        {
            error = $"读取 Penumbra Collection 失败: {ex.Message}";
            return false;
        }
    }

    private Dictionary<Guid, string> GetCollections()
    {
        var subscriber = _pluginInterface.GetIpcSubscriber<Dictionary<Guid, string>>(GetCollectionsGate);
        return subscriber.InvokeFunc() ?? [];
    }

    private bool QueryGlamourerTakeover(int playerObjectIndex, string modDirectory)
    {
        try
        {
            var subscriber = _pluginInterface.GetIpcSubscriber<int, string, string, int, (int, object?, string)>(
                QueryTemporaryModSettingsPlayerGate);

            foreach (var lockKey in new[] { GlamourerKeyAutomation, GlamourerKeyManual })
            {
                var (ec, settings, source) = subscriber.InvokeFunc(
                    playerObjectIndex,
                    modDirectory,
                    string.Empty,
                    lockKey);

                if ((PenumbraIpcEc)ec != PenumbraIpcEc.Success || settings == null)
                    continue;

                if (string.IsNullOrWhiteSpace(source)
                    || source.Contains("Glamourer", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch
        {
            return false;
        }

        return false;
    }
}
