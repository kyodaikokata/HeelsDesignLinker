using System.Collections;
using System.Reflection;
using System;
using Dalamud.Plugin;
using Newtonsoft.Json.Linq;

namespace HeelsDesignLinker;

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
    TemporarySettingDisallowed = 6,
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
    private const string GetCollectionGate = "Penumbra.GetCollection";
    private const string GetCollectionForObjectGate = "Penumbra.GetCollectionForObject";
    private const string GetModListGate = "Penumbra.GetModList";
    private const string GetAvailableModSettingsGate = "Penumbra.GetAvailableModSettings.V5";
    private const string GetCurrentModSettingsGate = "Penumbra.GetCurrentModSettings.V5";
    private const string GetCurrentModSettingsWithTempGate = "Penumbra.GetCurrentModSettingsWithTemp";
    private const string GetAllModSettingsGate = "Penumbra.GetAllModSettings";
    private const string GetSettingsInAllCollectionsGate = "Penumbra.GetSettingsInAllCollections";
    private const string TrySetModGate = "Penumbra.TrySetMod.V5";
    private const string TrySetModSettingGate = "Penumbra.TrySetModSetting.V5";
    private const string TrySetModSettingsGate = "Penumbra.TrySetModSettings.V5";
    private const string RemoveTemporaryModSettingsPlayerGate = "Penumbra.RemoveTemporaryModSettingsPlayer.V5";
    private const string RemoveAllTemporaryModSettingsPlayerGate = "Penumbra.RemoveAllTemporaryModSettingsPlayer.V5";
    private const string SetTemporaryModSettingsPlayerGate = "Penumbra.SetTemporaryModSettingsPlayer.V5";
    private const string QueryTemporaryModSettingsPlayerGate = "Penumbra.QueryTemporaryModSettingsPlayer.V5";
    private const string GetModPathGate = "Penumbra.GetModPath.V5";
    private const string OpenMainWindowGate = "Penumbra.OpenMainWindow.V5";

    /// <summary>与 Penumbra.Api.Enums.TabType 数值一致。</summary>
    private enum PenumbraMainTabType
    {
        None = -1,
        Settings = 0,
        Mods = 1,
        Collections = 2,
    }

    /// <summary>与 Penumbra.Api.Enums.ApiCollectionType 数值一致。</summary>
    private enum PenumbraApiCollectionType : byte
    {
        Default = 0xE0,
        Interface = 0xE1,
        Current = 0xE2,
    }

    /// <summary>Glamourer 自动化临时设置的 Lock Key（与 Glamourer 源码一致）。</summary>
    private const int GlamourerKeyAutomation = -1610;

    /// <summary>Glamourer 手动临时设置的 Lock Key（与 Glamourer 源码一致）。</summary>
    private const int GlamourerKeyManual = -6160;

    /// <summary>HeelsDesignLinker 临时 Penumbra 设置的 Lock Key（见 docs/Penumbra-Temporary-IPC.md）。</summary>
    internal const int HeelsDesignLinkerPenumbraLockKey = -1211;

    /// <summary>HeelsDesignLinker 临时 Penumbra 设置的 Source 显示名。</summary>
    internal const string HeelsDesignLinkerPenumbraSource = "HeelsDesignLinker";

    /// <summary>SFW 模式临时 Penumbra 设置的 Lock Key（独立于规则层 -1211）。</summary>
    internal const int SfwModePenumbraLockKey = -1210;

    /// <summary>SFW 模式临时 Penumbra 设置的 Source 显示名。</summary>
    internal const string SfwModePenumbraSource = "HeelsDesignLinker-SFW";

    internal enum PenumbraTempLayer
    {
        HeelsRule,
        SfwMode,
    }

    /// <summary>某 Mod 当前临时层覆盖的归属：无 / 本插件（自己）/ Glamourer。</summary>
    internal enum PenumbraModTempOverrideOwner
    {
        None,
        Self,
        Glamourer,
    }

    private readonly IDalamudPluginInterface _pluginInterface;

    /// <summary>为 true 时读取含临时层的生效设置（key 见 <see cref="TemporarySettingsReadKey"/>）。</summary>
    internal bool ReadIncludingTemporarySettings { get; set; }

    /// <summary>WithTemp / QueryTemporary 读取与比对使用的 lock key（规则 -1211 或 SFW -1210）。</summary>
    internal int TemporarySettingsReadKey { get; set; } = HeelsDesignLinkerPenumbraLockKey;
    private List<string> _collectionNames = [];
    private List<PenumbraModEntry> _mods = [];
    private Dictionary<string, Dictionary<string, PenumbraOptionGroupInfo>> _modSettingsByDirectory =
        new(StringComparer.OrdinalIgnoreCase);
    private DateTime _dataFetchedUtc = DateTime.MinValue;
    private static readonly TimeSpan DataRefreshInterval = TimeSpan.FromSeconds(30);

    private string _takeoverCacheMod = "";
    private int _takeoverCachePlayerIndex = -1;
    private PenumbraModTempOverrideOwner _takeoverCacheOwner = PenumbraModTempOverrideOwner.None;
    private DateTime _takeoverCacheUtc = DateTime.MinValue;
    private static readonly TimeSpan TakeoverCacheInterval = TimeSpan.FromSeconds(1);

    private Dictionary<string, string>? _modFilesystemPathCache;
    private DateTime _modPathCacheUtc = DateTime.MinValue;

    private string? _penumbraUiSelectedModDirectory;
    private Action<string>? _penumbraUiSelectedModHandler;
    private readonly List<(string Gate, Action<string> Handler)> _penumbraUiSelectedModSubscriptions = [];

    public PenumbraInterop(IDalamudPluginInterface pluginInterface)
    {
        _pluginInterface = pluginInterface;
        EnsureUiSelectedModSubscription();
    }

    public void Dispose()
    {
        foreach (var (gate, handler) in _penumbraUiSelectedModSubscriptions)
        {
            try
            {
                _pluginInterface.GetIpcSubscriber<string, object>(gate).Unsubscribe(handler);
            }
            catch
            {
                // Penumbra 可能已卸载
            }
        }

        _penumbraUiSelectedModSubscriptions.Clear();
        _penumbraUiSelectedModHandler = null;
    }

    /// <summary>订阅 Penumbra UI 事件以跟踪用户在 Penumbra 窗口中选中的 Mod 目录名。</summary>
    public void EnsureUiSelectedModSubscription()
    {
        if (_penumbraUiSelectedModHandler != null)
            return;

        _penumbraUiSelectedModHandler = RememberPenumbraUiSelectedMod;

        foreach (var gate in new[]
                 {
                     "Penumbra.PreSettingsDraw",
                     "Penumbra.PostSettingsDraw",
                     "Penumbra.PostEnabledDraw",
                 })
        {
            TrySubscribePenumbraUiModEvent(gate, _penumbraUiSelectedModHandler);
        }
    }

    private void RememberPenumbraUiSelectedMod(string modDirectory)
    {
        if (!string.IsNullOrWhiteSpace(modDirectory))
            _penumbraUiSelectedModDirectory = modDirectory.Trim();
    }

    private void TrySubscribePenumbraUiModEvent(string gate, Action<string> handler)
    {
        try
        {
            _pluginInterface.GetIpcSubscriber<string, object>(gate).Subscribe(handler);
            _penumbraUiSelectedModSubscriptions.Add((gate, handler));
        }
        catch
        {
            // 旧版 Penumbra 可能无此事件
        }
    }

    public bool TryGetPenumbraUiSelectedModDirectory(out string modDirectory)
    {
        modDirectory = _penumbraUiSelectedModDirectory ?? "";
        return !string.IsNullOrWhiteSpace(modDirectory);
    }

    /// <summary>读取 collection 内 Mod 的生效设置（不含临时层），用于从 Penumbra UI 导入。</summary>
    public bool TryGetCollectionModConfigurationForImport(
        string collectionName,
        string modDirectory,
        out bool modEnabled,
        out IReadOnlyDictionary<string, List<string>> settingsByGroup,
        out string error)
    {
        modEnabled = false;
        settingsByGroup = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        error = "";

        if (!TryResolveCollectionId(collectionName, out var collectionId, out error))
            return false;

        if (string.IsNullOrWhiteSpace(modDirectory))
        {
            error = "Mod not selected";
            return false;
        }

        var modName = ResolveModDisplayName(modDirectory);
        try
        {
            var subscriber = _pluginInterface.GetIpcSubscriber<
                Guid, string, string, bool, bool, int, object>(
                GetCurrentModSettingsWithTempGate);
            var raw = subscriber.InvokeFunc(
                collectionId,
                modDirectory.Trim(),
                modName,
                false,
                true,
                0);

            if (!TryParseCurrentModConfiguration(raw, out modEnabled, out var settings, out error))
                return false;

            settingsByGroup = settings;
            return true;
        }
        catch (Exception ex)
        {
            error = $"读取 Penumbra 当前设置失败: {ex.Message}";
            return false;
        }
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
        if (force)
            _modFilesystemPathCache = null;
    }

    public void EnsureModFilesystemPathCache(bool force = false)
    {
        if (!force
            && _modFilesystemPathCache != null
            && (DateTime.UtcNow - _modPathCacheUtc) < DataRefreshInterval)
        {
            return;
        }

        _modFilesystemPathCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _modPathCacheUtc = DateTime.UtcNow;

        if (!IsIpcAvailable())
            return;

        foreach (var mod in GetMods())
        {
            if (TryGetModFilesystemPath(mod.Directory, mod.DisplayName, out var path))
                _modFilesystemPathCache[mod.Directory] = NormalizeFilesystemPath(path);
            else if (TryGetModFilesystemPath(mod.Directory, string.Empty, out path))
                _modFilesystemPathCache[mod.Directory] = NormalizeFilesystemPath(path);
        }
    }

    public bool TryResolveModFromFilePath(string filePath, out PenumbraModEntry mod)
    {
        mod = new PenumbraModEntry("", "");
        if (string.IsNullOrWhiteSpace(filePath) || !IsIpcAvailable())
            return false;

        EnsureModFilesystemPathCache();

        var normalizedFile = NormalizeFilesystemPath(filePath);
        PenumbraModEntry? bestMod = null;
        var bestPrefixLength = 0;

        if (_modFilesystemPathCache != null)
        {
            foreach (var candidate in GetMods())
            {
                if (!_modFilesystemPathCache.TryGetValue(candidate.Directory, out var modPath))
                    continue;

                if (!IsPathUnderDirectory(normalizedFile, modPath) || modPath.Length <= bestPrefixLength)
                    continue;

                bestMod = candidate;
                bestPrefixLength = modPath.Length;
            }
        }

        if (bestMod == null && TryResolveModFromParentDirectories(normalizedFile, out bestMod))
        {
            mod = bestMod;
            return true;
        }

        if (bestMod == null)
            return false;

        mod = bestMod;
        return true;
    }

    private bool TryResolveModFromParentDirectories(string normalizedFilePath, out PenumbraModEntry mod)
    {
        mod = new PenumbraModEntry("", "");
        var mods = GetMods();
        if (mods.Count == 0)
            return false;

        var modsByDirectory = mods
            .GroupBy(m => m.Directory, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        for (var dir = Path.GetDirectoryName(normalizedFilePath);
             !string.IsNullOrWhiteSpace(dir);
             dir = Path.GetDirectoryName(dir))
        {
            var folderName = Path.GetFileName(dir);
            if (string.IsNullOrWhiteSpace(folderName))
                continue;

            if (modsByDirectory.TryGetValue(folderName, out var match))
            {
                mod = match;
                return true;
            }
        }

        return false;
    }

    private static bool IsPathUnderDirectory(string filePath, string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(directoryPath))
            return false;

        var file = NormalizeFilesystemPath(filePath);
        var dir = NormalizeFilesystemPath(directoryPath);
        if (file.Length < dir.Length)
            return false;

        if (!file.StartsWith(dir, StringComparison.OrdinalIgnoreCase))
            return false;

        if (file.Length == dir.Length)
            return true;

        var separator = file[dir.Length];
        return separator is '\\' or '/';
    }

    public bool TryOpenModInPenumbra(PenumbraModEntry mod, out PenumbraIpcEc result)
    {
        result = PenumbraIpcEc.UnknownError;
        if (!IsIpcAvailable()
            || string.IsNullOrWhiteSpace(mod.Directory))
        {
            return false;
        }

        var modDirectory = mod.Directory.Trim();
        foreach (var modName in new[] { mod.DisplayName.Trim(), string.Empty }.Distinct(StringComparer.Ordinal))
        {
            if (TryInvokeOpenMainWindow(modDirectory, modName, out result)
                && result is PenumbraIpcEc.Success or PenumbraIpcEc.NothingChanged)
            {
                return true;
            }
        }

        return false;
    }

    public bool TryOpenModInPenumbra(PenumbraModEntry mod) =>
        TryOpenModInPenumbra(mod, out _);

    private bool TryInvokeOpenMainWindow(string modDirectory, string modDisplayName, out PenumbraIpcEc result)
    {
        result = PenumbraIpcEc.UnknownError;

        try
        {
            // Penumbra.Api V5: (int tabType, string modDirectory, string modName) -> int (PenumbraApiEc)
            var subscriber = _pluginInterface.GetIpcSubscriber<int, string, string, int>(OpenMainWindowGate);
            var ec = subscriber.InvokeFunc((int)PenumbraMainTabType.Mods, modDirectory, modDisplayName);
            result = (PenumbraIpcEc)ec;
            return true;
        }
        catch
        {
            try
            {
                var subscriber = _pluginInterface.GetIpcSubscriber<int, string, string, object>(OpenMainWindowGate);
                var raw = subscriber.InvokeFunc((int)PenumbraMainTabType.Mods, modDirectory, modDisplayName);
                if (TryConvertPenumbraIpcEc(raw, out result))
                    return true;
            }
            catch
            {
                return false;
            }
        }

        return false;
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

    /// <summary>当前生效的 Penumbra Collection 名称；失败时返回 "Default"。</summary>
    public string GetDefaultCollectionName(int? playerObjectIndex = null) =>
        TryGetActiveCollectionName(playerObjectIndex, out var name) ? name : "Default";

    /// <summary>Penumbra UI 中当前正在编辑的 Collection（Collections 页选中项）。</summary>
    public bool TryGetPenumbraUiCurrentCollectionName(out string collectionName)
    {
        if (TryGetCollectionNameByType(PenumbraApiCollectionType.Current, out collectionName)
            && !string.IsNullOrWhiteSpace(collectionName))
            return true;

        return TryGetActiveCollectionName(null, out collectionName);
    }

    /// <summary>尝试读取本地玩家当前生效的 Collection 名称。</summary>
    public bool TryGetActiveCollectionName(int? playerObjectIndex, out string collectionName)
    {
        collectionName = "";
        if (!IsIpcAvailable())
            return false;

        if (playerObjectIndex is int index && TryGetCollectionNameForObject(index, out collectionName))
            return true;

        if (TryGetCollectionNameByType(PenumbraApiCollectionType.Current, out collectionName))
            return true;

        return TryGetCollectionNameByType(PenumbraApiCollectionType.Default, out collectionName);
    }

    private bool TryGetCollectionNameForObject(int gameObjectIndex, out string collectionName)
    {
        collectionName = "";
        if (!TryInvokeGetCollectionForObject(gameObjectIndex, out var raw) || raw == null)
            return false;

        return TryParseCollectionForObjectResult(raw, out collectionName);
    }

    private bool TryGetCollectionNameByType(PenumbraApiCollectionType type, out string collectionName)
    {
        collectionName = "";
        if (!TryInvokeGetCollection((int)type, out var raw))
            return false;

        return TryParseCollectionIdentity(raw, out collectionName);
    }

    private bool TryInvokeGetCollectionForObject(int gameObjectIndex, out object? raw)
    {
        raw = null;
        try
        {
            var subscriber = _pluginInterface.GetIpcSubscriber<int, object>(GetCollectionForObjectGate);
            raw = subscriber.InvokeFunc(gameObjectIndex);
            return raw != null;
        }
        catch
        {
            return false;
        }
    }

    private bool TryInvokeGetCollection(int collectionType, out object? raw)
    {
        raw = null;
        try
        {
            var subscriber = _pluginInterface.GetIpcSubscriber<int, object>(GetCollectionGate);
            raw = subscriber.InvokeFunc(collectionType);
            return raw != null;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParseCollectionForObjectResult(object raw, out string collectionName)
    {
        collectionName = "";
        if (!TryGetNamedTupleMembers(raw, out _, out _, out var collectionPayload))
            return false;

        return TryParseCollectionIdentity(collectionPayload, out collectionName);
    }

    private static bool TryParseCollectionIdentity(object? value, out string collectionName)
    {
        collectionName = "";
        if (value == null)
            return false;

        if (value is string name && !string.IsNullOrWhiteSpace(name))
        {
            collectionName = name;
            return true;
        }

        if (TryGetNamedTupleMembers(value, out _, out var nameObj, out _)
            && nameObj is string tupleName
            && !string.IsNullOrWhiteSpace(tupleName))
        {
            collectionName = tupleName;
            return true;
        }

        return false;
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

    public bool IsModUnderGlamourerTakeover(int playerObjectIndex, string modDirectory) =>
        GetModTemporaryOverrideOwner(playerObjectIndex, modDirectory) == PenumbraModTempOverrideOwner.Glamourer;

    /// <summary>
    /// 判定某 Mod 当前临时层覆盖的归属：优先检测本插件 key（-1211/-1210，视为“自己”），
    /// 再检测 Glamourer key（-1610/-6160）。结果带 1 秒缓存。
    /// </summary>
    public PenumbraModTempOverrideOwner GetModTemporaryOverrideOwner(int playerObjectIndex, string modDirectory)
    {
        if (!IsIpcAvailable() || string.IsNullOrWhiteSpace(modDirectory))
            return PenumbraModTempOverrideOwner.None;

        var key = modDirectory.Trim();
        var now = DateTime.UtcNow;
        if (_takeoverCachePlayerIndex == playerObjectIndex
            && string.Equals(_takeoverCacheMod, key, StringComparison.OrdinalIgnoreCase)
            && (now - _takeoverCacheUtc) < TakeoverCacheInterval)
        {
            return _takeoverCacheOwner;
        }

        PenumbraModTempOverrideOwner owner;
        if (QueryTempOverrideHeldByKeys(playerObjectIndex, key,
                new[] { HeelsDesignLinkerPenumbraLockKey, SfwModePenumbraLockKey }))
            owner = PenumbraModTempOverrideOwner.Self;
        else if (QueryGlamourerTakeover(playerObjectIndex, key))
            owner = PenumbraModTempOverrideOwner.Glamourer;
        else
            owner = PenumbraModTempOverrideOwner.None;

        _takeoverCacheOwner = owner;
        _takeoverCacheMod = key;
        _takeoverCachePlayerIndex = playerObjectIndex;
        _takeoverCacheUtc = now;
        return owner;
    }

    /// <summary>查询指定 lock key 集合中是否有任意 key 当前持有该 Mod 的临时设置。</summary>
    private bool QueryTempOverrideHeldByKeys(int playerObjectIndex, string modDirectory, int[] lockKeys)
    {
        try
        {
            var subscriber = _pluginInterface.GetIpcSubscriber<int, string, string, int, (int, object?, string)>(
                QueryTemporaryModSettingsPlayerGate);

            var modName = ResolveModDisplayName(modDirectory);
            foreach (var lockKey in lockKeys)
            {
                var (ec, settings, _) = subscriber.InvokeFunc(playerObjectIndex, modDirectory, modName, lockKey);
                if ((PenumbraIpcEc)ec == PenumbraIpcEc.Success && settings != null)
                    return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    public void InvalidateTakeoverCache()
    {
        _takeoverCacheMod = "";
        _takeoverCachePlayerIndex = -1;
        _takeoverCacheOwner = PenumbraModTempOverrideOwner.None;
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

            var modName = ResolveModDisplayName(modDirectory);
            var results = new List<PenumbraIpcEc>();
            foreach (var key in new[] { GlamourerKeyAutomation, GlamourerKeyManual })
            {
                var result = (PenumbraIpcEc)subscriber.InvokeFunc(
                    playerObjectIndex,
                    modDirectory.Trim(),
                    modName,
                    key);
                results.Add(result);
            }

            // 两个 GLM key 均须 Success/NothingChanged，避免只清掉一半仍 TemporarySettingDisallowed
            if (results.All(r => r is PenumbraIpcEc.Success or PenumbraIpcEc.NothingChanged))
            {
                InvalidateTakeoverCache();
                message = "OK";
                return true;
            }

            var failed = results.FirstOrDefault(r => r is not PenumbraIpcEc.Success and not PenumbraIpcEc.NothingChanged);
            message = failed == default && results.Count > 0
                ? "Glamourer temporary settings still present"
                : $"Glamourer remove failed: {failed}";
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
                else if (!TryGetCurrentEnabledOptions(
                             collectionName,
                             trimmedModDirectory,
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

    public bool TrySetModEnabled(
        string collectionName,
        string modDirectory,
        bool enabled,
        out PenumbraIpcEc result,
        out string error)
    {
        result = PenumbraIpcEc.UnknownError;
        error = "";

        if (string.IsNullOrWhiteSpace(collectionName)
            || string.IsNullOrWhiteSpace(modDirectory))
        {
            error = "Penumbra 配置不完整（Collection / Mod）";
            return false;
        }

        if (!TryResolveCollectionId(collectionName, out var collectionId, out error))
            return false;

        var modName = ResolveModDisplayName(modDirectory);
        var trimmedModDirectory = modDirectory.Trim();

        try
        {
            var subscriber = _pluginInterface.GetIpcSubscriber<Guid, string, string, bool, object>(
                TrySetModGate);
            var ipcResult = subscriber.InvokeFunc(
                collectionId,
                trimmedModDirectory,
                modName,
                enabled);

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

    public bool TryGetCurrentModConfiguration(
        string collectionName,
        string modDirectory,
        out bool modEnabled,
        out IReadOnlyDictionary<string, List<string>> settingsByGroup,
        out string error)
    {
        modEnabled = false;
        settingsByGroup = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        error = "";

        if (!TryResolveCollectionId(collectionName, out var collectionId, out error))
            return false;

        var modName = ResolveModDisplayName(modDirectory);
        try
        {
            object raw;
            if (ReadIncludingTemporarySettings)
            {
                var subscriber = _pluginInterface.GetIpcSubscriber<
                    Guid, string, string, bool, bool, int, object>(
                    GetCurrentModSettingsWithTempGate);
                raw = subscriber.InvokeFunc(
                    collectionId,
                    modDirectory.Trim(),
                    modName,
                    false,
                    false,
                    TemporarySettingsReadKey);
            }
            else
            {
                var subscriber = _pluginInterface.GetIpcSubscriber<Guid, string, string, bool, object>(
                    GetCurrentModSettingsGate);
                raw = subscriber.InvokeFunc(collectionId, modDirectory.Trim(), modName, false);
            }

            if (!TryParseCurrentModConfiguration(raw, out modEnabled, out var settings, out error))
                return false;

            settingsByGroup = settings;
            return true;
        }
        catch (Exception ex)
        {
            error = $"读取 Penumbra 当前设置失败: {ex.Message}";
            return false;
        }
    }

    public bool TryGetCurrentEnabledOptions(
        string collectionName,
        string modDirectory,
        string optionGroupName,
        out IReadOnlyList<string> enabledOptionNames,
        out string error)
    {
        enabledOptionNames = [];
        error = "";

        if (!TryGetCurrentModConfiguration(collectionName, modDirectory, out _, out var settingsByGroup, out error))
            return false;

        if (!TryGetGroupOptionNames(settingsByGroup, optionGroupName, out var optionNames))
            return false;

        enabledOptionNames = optionNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return true;
    }

    /// <summary>读取 Collection 持久设置中某选项组的已启用项（不含任何临时层）。</summary>
    public bool TryGetCollectionEnabledOptions(
        string collectionName,
        string modDirectory,
        string optionGroupName,
        out IReadOnlyList<string> enabledOptionNames,
        out string error)
    {
        enabledOptionNames = [];
        error = "";

        if (!TryGetCollectionModConfigurationForImport(
                collectionName,
                modDirectory,
                out _,
                out var settingsByGroup,
                out error))
            return false;

        if (!TryGetGroupOptionNames(settingsByGroup, optionGroupName, out var optionNames))
            return false;

        enabledOptionNames = optionNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return true;
    }

    /// <summary>读取指定临时 key 下的生效选项（用于 multi-toggle delta 与比对）。</summary>
    public bool TryGetEffectiveEnabledOptionsForLayer(
        string collectionName,
        string modDirectory,
        string optionGroupName,
        int tempReadKey,
        out IReadOnlyList<string> enabledOptionNames,
        out string error)
    {
        enabledOptionNames = [];
        error = "";

        if (!TryResolveCollectionId(collectionName, out var collectionId, out error))
            return false;

        if (string.IsNullOrWhiteSpace(modDirectory))
        {
            error = "Mod not selected";
            return false;
        }

        var modName = ResolveModDisplayName(modDirectory);
        try
        {
            var subscriber = _pluginInterface.GetIpcSubscriber<
                Guid, string, string, bool, bool, int, (int, (bool, int, IReadOnlyDictionary<string, IReadOnlyList<string>>, bool, bool)?)>(
                GetCurrentModSettingsWithTempGate);
            var (ec, settingsTuple) = subscriber.InvokeFunc(
                collectionId,
                modDirectory.Trim(),
                modName,
                false,
                false,
                tempReadKey);

            if (!IsPenumbraIpcSuccess((PenumbraIpcEc)ec) || settingsTuple is not { } tuple)
                return false;

            var settingsByGroup = tuple.Item3
                .ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value.ToList(),
                    StringComparer.OrdinalIgnoreCase);

            if (!TryGetGroupOptionNames(settingsByGroup, optionGroupName, out var optionNames))
                return false;

            enabledOptionNames = optionNames
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

    /// <summary>
    /// 比较当前 Penumbra 设置是否与目标一致。
    /// null = 无法读取当前状态（不应据此判定失败或刷警告）。
    /// </summary>
    public bool? CompareGroupSetting(
        string collectionName,
        string modDirectory,
        string optionGroupName,
        PenumbraGroupType groupType,
        string? singleSelectOptionName,
        IReadOnlyList<string>? multiToggleEnabledNames,
        int? playerObjectIndex = null)
    {
        if (!TryGetEffectiveModSettingsForCompare(
                collectionName,
                modDirectory,
                playerObjectIndex,
                out _,
                out var settingsByGroup,
                out _))
            return null;

        var trimmedGroup = optionGroupName.Trim();
        if (!TryGetGroupOptionNames(settingsByGroup, trimmedGroup, out var optionNames))
            return null;

        var current = optionNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (UsesBoolOptionValue(groupType))
        {
            var expected = new HashSet<string>(
                (multiToggleEnabledNames ?? [])
                    .Select(name => name.Trim())
                    .Where(name => !string.IsNullOrWhiteSpace(name)),
                StringComparer.OrdinalIgnoreCase);
            var actual = new HashSet<string>(current, StringComparer.OrdinalIgnoreCase);
            return expected.SetEquals(actual);
        }

        if (string.IsNullOrWhiteSpace(singleSelectOptionName))
            return current.Count == 0;

        return current.Any(name =>
            string.Equals(name, singleSelectOptionName.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryParseCurrentModConfiguration(
        object? raw,
        out bool modEnabled,
        out Dictionary<string, List<string>> settingsByGroup,
        out string error) =>
        TryParseCurrentModSettingsIpcRaw(raw, out modEnabled, out _, out settingsByGroup, out error);

    /// <summary>
    /// 解析 GetCurrentModSettings* 返回值：(PenumbraApiEc, (enabled, priority, settings, inherit)?)。
    /// </summary>
    private static bool TryParseCurrentModSettingsIpcRaw(
        object? raw,
        out bool modEnabled,
        out int modPriority,
        out Dictionary<string, List<string>> settingsByGroup,
        out string error)
    {
        modEnabled = false;
        modPriority = 0;
        settingsByGroup = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        error = "";

        if (!TryUnwrapIpcResult(raw, out var payload, out var resultCode))
        {
            error = "无法解析 Penumbra 当前设置返回值";
            return false;
        }

        if (resultCode is not null
            && TryConvertPenumbraIpcEc(resultCode, out var ec)
            && !IsPenumbraIpcSuccess(ec))
        {
            error = $"Penumbra IPC: {ec}";
            return false;
        }

        if (payload == null)
            return true;

        if (!TryGetNamedTupleMembers(payload, out var enabledItem, out var priorityItem, out var settingsItem))
        {
            TryExtractPriorityFromTuplePayload(payload, out modPriority);
            return TryParseCurrentModSettings(payload, out settingsByGroup, out error);
        }

        modEnabled = enabledItem switch
        {
            bool enabled => enabled,
            null => false,
            _ => Convert.ToBoolean(enabledItem),
        };

        TryConvertPenumbraPriority(priorityItem, out modPriority);

        if (settingsItem != null)
            ExtractSettingsDictionary(settingsItem, settingsByGroup);

        return true;
    }

    private static bool TryConvertPenumbraPriority(object? value, out int priority)
    {
        priority = 0;
        if (value == null)
            return false;

        switch (value)
        {
            case int i:
                priority = i;
                return true;
            case long l:
                priority = (int)l;
                return true;
            case byte or short or sbyte or ushort or uint:
                priority = Convert.ToInt32(value);
                return true;
        }

        var type = value.GetType();
        var valueProperty = type.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
        if (valueProperty != null)
        {
            var inner = valueProperty.GetValue(value);
            if (inner != null && TryConvertPenumbraPriority(inner, out priority))
                return true;
        }

        var valueField = type.GetField("Value", BindingFlags.Public | BindingFlags.Instance);
        if (valueField != null)
        {
            var inner = valueField.GetValue(value);
            if (inner != null && TryConvertPenumbraPriority(inner, out priority))
                return true;
        }

        return int.TryParse(value.ToString(), out priority);
    }

    private static string NormalizePenumbraCollectionName(string? collectionName) =>
        string.IsNullOrWhiteSpace(collectionName) ? "Default" : collectionName.Trim();

    private static void ExtractSettingsDictionary(
        object value,
        Dictionary<string, List<string>> settingsByGroup)
    {
        switch (value)
        {
            case IDictionary<string, List<string>> typedDict:
                foreach (var (group, names) in typedDict)
                    settingsByGroup[group] = NormalizeOptionNames(names);
                break;
            case IReadOnlyDictionary<string, List<string>> readOnlyDict:
                foreach (var (group, names) in readOnlyDict)
                    settingsByGroup[group] = NormalizeOptionNames(names);
                break;
            case IDictionary settingsDict:
                foreach (DictionaryEntry entry in settingsDict)
                {
                    if (entry.Key is string group)
                        settingsByGroup[group] = ParseOptionNameList(entry.Value);
                }
                break;
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

        if (value == null)
            return false;

        if (TryGetTupleItemAt(value, 1, out item1)
            | TryGetTupleItemAt(value, 2, out item2)
            | TryGetTupleItemAt(value, 3, out item3))
            return item1 != null || item2 != null || item3 != null;

        return false;
    }

    private static bool TryExtractPriorityFromTuplePayload(object? payload, out int priority)
    {
        priority = 0;
        if (!TryGetTupleItemAt(payload, 2, out var priorityItem))
            return false;

        return TryConvertPenumbraPriority(priorityItem, out priority);
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

    private static bool TryExtractPenumbraIpcEc(object? raw, out PenumbraIpcEc ec)
    {
        if (TryUnwrapIpcResult(raw, out _, out var resultCode)
            && TryConvertPenumbraIpcEc(resultCode, out ec))
        {
            return true;
        }

        return TryConvertPenumbraIpcEc(raw, out ec);
    }

    private static bool TryConvertPenumbraIpcEc(object? value, out PenumbraIpcEc ec)
    {
        ec = PenumbraIpcEc.UnknownError;
        if (value == null)
            return false;

        if (value is object[] arr && arr.Length > 0)
            return TryConvertPenumbraIpcEc(arr[0], out ec);

        if (value is IList list && value is not string && value is not string[] && list.Count > 0)
            return TryConvertPenumbraIpcEc(list[0], out ec);

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

    private bool TryGetModFilesystemPath(string modDirectory, string modDisplayName, out string fullPath)
    {
        fullPath = "";
        if (string.IsNullOrWhiteSpace(modDirectory))
            return false;

        var trimmedDirectory = modDirectory.Trim();
        var trimmedName = modDisplayName?.Trim() ?? "";

        foreach (var name in new[] { trimmedName, string.Empty }.Distinct(StringComparer.Ordinal))
        {
            if (!TryInvokeGetModPath(trimmedDirectory, name, out fullPath))
                continue;

            return true;
        }

        return false;
    }

    private bool TryInvokeGetModPath(string modDirectory, string modDisplayName, out string fullPath)
    {
        fullPath = "";
        try
        {
            var subscriber = _pluginInterface.GetIpcSubscriber<string, string, (int, string, bool, bool)>(GetModPathGate);
            var (ec, path, _, _) = subscriber.InvokeFunc(modDirectory, modDisplayName);
            if ((PenumbraIpcEc)ec == PenumbraIpcEc.Success && !string.IsNullOrWhiteSpace(path))
            {
                fullPath = path;
                return true;
            }
        }
        catch
        {
            try
            {
                var subscriber = _pluginInterface.GetIpcSubscriber<string, string, object>(GetModPathGate);
                var raw = subscriber.InvokeFunc(modDirectory, modDisplayName);
                if (TryUnwrapIpcResult(raw, out var payload, out var resultCode)
                    && TryConvertPenumbraIpcEc(resultCode, out var ec)
                    && ec == PenumbraIpcEc.Success
                    && payload is string path
                    && !string.IsNullOrWhiteSpace(path))
                {
                    fullPath = path;
                    return true;
                }

                if (TryGetNamedTupleMembers(raw, out var item1, out var item2, out _)
                    && TryConvertPenumbraIpcEc(item1, out ec)
                    && ec == PenumbraIpcEc.Success
                    && item2 is string tuplePath
                    && !string.IsNullOrWhiteSpace(tuplePath))
                {
                    fullPath = tuplePath;
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    private static string NormalizeFilesystemPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "";

        // 游戏资源句柄常用 E:/... 正斜杠；先统一再 GetFullPath，避免 StartsWith 因分隔符不一致失败
        var withNativeSeparators = path.Trim().Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        try
        {
            return Path.GetFullPath(withNativeSeparators)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return withNativeSeparators
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }

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

            var modName = ResolveModDisplayName(modDirectory);
            foreach (var lockKey in new[] { GlamourerKeyAutomation, GlamourerKeyManual })
            {
                var (ec, settings, source) = subscriber.InvokeFunc(
                    playerObjectIndex,
                    modDirectory,
                    modName,
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

    public bool TryRemoveAllHeelsTemporaryModSettingsPlayer(int playerObjectIndex, out string message) =>
        TryRemoveAllTemporaryModSettingsPlayer(playerObjectIndex, HeelsDesignLinkerPenumbraLockKey, out message);

    public bool TryRemoveAllSfwTemporaryModSettingsPlayer(int playerObjectIndex, out string message) =>
        TryRemoveAllTemporaryModSettingsPlayer(playerObjectIndex, SfwModePenumbraLockKey, out message);

    private bool TryRemoveAllTemporaryModSettingsPlayer(int playerObjectIndex, int lockKey, out string message)
    {
        message = "";
        if (!IsIpcAvailable())
        {
            message = "Penumbra IPC unavailable";
            return false;
        }

        try
        {
            var subscriber = _pluginInterface.GetIpcSubscriber<int, int, int>(
                RemoveAllTemporaryModSettingsPlayerGate);
            var result = (PenumbraIpcEc)subscriber.InvokeFunc(playerObjectIndex, lockKey);

            if (result is PenumbraIpcEc.Success or PenumbraIpcEc.NothingChanged)
            {
                InvalidateTakeoverCache();
                message = "OK";
                return true;
            }

            message = result.ToString();
            return false;
        }
        catch (Exception ex)
        {
            message = $"Penumbra IPC: {ex.Message}";
            return false;
        }
    }

    public bool TrySetTemporaryModEnabledPlayer(
        int playerObjectIndex,
        string collectionName,
        string modDirectory,
        bool enabled,
        out PenumbraIpcEc result,
        out string error) =>
        TrySetTemporaryModEnabledPlayer(
            playerObjectIndex,
            collectionName,
            modDirectory,
            enabled,
            PenumbraTempLayer.HeelsRule,
            out result,
            out error);

    public bool TrySetSfwTemporaryModEnabledPlayer(
        int playerObjectIndex,
        string collectionName,
        string modDirectory,
        bool enabled,
        out PenumbraIpcEc result,
        out string error) =>
        TrySetTemporaryModEnabledPlayer(
            playerObjectIndex,
            collectionName,
            modDirectory,
            enabled,
            PenumbraTempLayer.SfwMode,
            out result,
            out error);

    private bool TrySetTemporaryModEnabledPlayer(
        int playerObjectIndex,
        string collectionName,
        string modDirectory,
        bool enabled,
        PenumbraTempLayer layer,
        out PenumbraIpcEc result,
        out string error)
    {
        result = PenumbraIpcEc.UnknownError;
        error = "";

        var (source, lockKey) = GetTempLayerParams(layer);
        var priority = ResolveTemporaryApplyPriority(
            playerObjectIndex,
            collectionName,
            modDirectory,
            lockKey);
        return TrySetTemporaryModSettingsPlayer(
            playerObjectIndex,
            modDirectory,
            forceInherit: false,
            enabled,
            priority,
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase),
            source,
            lockKey,
            out result,
            out error);
    }

    /// <summary>
    /// 单次临时写入：仅提交规则目标选项组（不附带从 WithTemp 读出的全量 settings，避免继承/解析脏数据导致 OptionMissing）。
    /// </summary>
    public bool TryApplyTemporaryModConfigurationPlayer(
        int playerObjectIndex,
        string collectionName,
        string modDirectory,
        bool enabled,
        IReadOnlyDictionary<string, IReadOnlyList<string>> optionGroupOverrides,
        bool mergeExistingTemporaryGroups,
        out PenumbraIpcEc result,
        out string error) =>
        TryApplyTemporaryModConfigurationPlayer(
            playerObjectIndex,
            collectionName,
            modDirectory,
            enabled,
            optionGroupOverrides,
            mergeExistingTemporaryGroups,
            PenumbraTempLayer.HeelsRule,
            out result,
            out error);

    public bool TryApplySfwTemporaryModConfigurationPlayer(
        int playerObjectIndex,
        string collectionName,
        string modDirectory,
        bool enabled,
        IReadOnlyDictionary<string, IReadOnlyList<string>> optionGroupOverrides,
        bool mergeExistingTemporaryGroups,
        out PenumbraIpcEc result,
        out string error) =>
        TryApplyTemporaryModConfigurationPlayer(
            playerObjectIndex,
            collectionName,
            modDirectory,
            enabled,
            optionGroupOverrides,
            mergeExistingTemporaryGroups,
            PenumbraTempLayer.SfwMode,
            out result,
            out error);

    private bool TryApplyTemporaryModConfigurationPlayer(
        int playerObjectIndex,
        string collectionName,
        string modDirectory,
        bool enabled,
        IReadOnlyDictionary<string, IReadOnlyList<string>> optionGroupOverrides,
        bool mergeExistingTemporaryGroups,
        PenumbraTempLayer layer,
        out PenumbraIpcEc result,
        out string error)
    {
        result = PenumbraIpcEc.UnknownError;
        error = "";

        var (source, lockKey) = GetTempLayerParams(layer);
        var priority = ResolveTemporaryApplyPriority(
            playerObjectIndex,
            collectionName,
            modDirectory,
            lockKey);
        var overrideKeys = new HashSet<string>(
            optionGroupOverrides.Keys.Where(key => !string.IsNullOrWhiteSpace(key)),
            StringComparer.OrdinalIgnoreCase);

        if (!TryMergeTemporaryOptionOverrides(
                collectionName,
                modDirectory,
                playerObjectIndex,
                lockKey,
                mergeExistingTemporaryGroups,
                optionGroupOverrides,
                out var mergedOverrides,
                out error))
            return false;

        if (!TryBuildSanitizedTemporaryOptionOverrides(
                modDirectory,
                mergedOverrides,
                overrideKeys,
                out var settings,
                out error))
        {
            return false;
        }

        return TrySetTemporaryModSettingsPlayer(
            playerObjectIndex,
            modDirectory,
            forceInherit: false,
            enabled,
            priority,
            settings,
            source,
            lockKey,
            out result,
            out error);
    }

    /// <summary>读取本插件 -1211 临时层上的 Mod 选项（用于 batch 合并，避免覆盖时丢组）。</summary>
    public bool TryGetHeelsTemporaryModSettingsPlayer(
        int playerObjectIndex,
        string modDirectory,
        out Dictionary<string, List<string>> settingsByGroup) =>
        TryGetTemporaryModSettingsPlayer(
            playerObjectIndex,
            modDirectory,
            HeelsDesignLinkerPenumbraLockKey,
            out settingsByGroup);

    public bool TryGetSfwTemporaryModSettingsPlayer(
        int playerObjectIndex,
        string modDirectory,
        out Dictionary<string, List<string>> settingsByGroup) =>
        TryGetTemporaryModSettingsPlayer(
            playerObjectIndex,
            modDirectory,
            SfwModePenumbraLockKey,
            out settingsByGroup);

    private bool TryGetTemporaryModSettingsPlayer(
        int playerObjectIndex,
        string modDirectory,
        int lockKey,
        out Dictionary<string, List<string>> settingsByGroup)
    {
        settingsByGroup = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        if (!IsIpcAvailable() || string.IsNullOrWhiteSpace(modDirectory))
            return false;

        try
        {
            var subscriber = _pluginInterface.GetIpcSubscriber<int, string, string, int, (int, object?, string)>(
                QueryTemporaryModSettingsPlayerGate);
            var (ec, settingsPayload, _) = subscriber.InvokeFunc(
                playerObjectIndex,
                modDirectory.Trim(),
                ResolveModDisplayName(modDirectory),
                lockKey);

            if ((PenumbraIpcEc)ec != PenumbraIpcEc.Success || settingsPayload == null)
                return false;

            var optionsItem = GetTupleItem4(settingsPayload);
            if (optionsItem != null)
                ExtractSettingsDictionary(optionsItem, settingsByGroup);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static object? GetTupleItem3(object value) => TryGetTupleItemAt(value, 3, out var item) ? item : null;

    private static object? GetTupleItem4(object value) => TryGetTupleItemAt(value, 4, out var item) ? item : null;

    /// <param name="itemIndex">ValueTuple 字段序号（Item1=1, Item2=2…）。</param>
    private static bool TryGetTupleItemAt(object? value, int itemIndex, out object? item)
    {
        item = null;
        if (value == null || itemIndex <= 0)
            return false;

        var arrayIndex = itemIndex - 1;
        if (value is object[] arr && arr.Length > arrayIndex)
        {
            item = arr[arrayIndex];
            return true;
        }

        if (value is JArray jarr && jarr.Count > arrayIndex)
        {
            item = jarr[arrayIndex];
            return true;
        }

        if (value is IList list && value is not string && value is not string[] && value is not JContainer
            && list.Count > arrayIndex)
        {
            item = list[arrayIndex];
            return true;
        }

        var key = $"Item{itemIndex}";
        var lowerKey = $"item{itemIndex}";
        var memberKey = $"m_Item{itemIndex}";

        if (value is JObject obj)
        {
            item = obj[key] ?? obj[lowerKey] ?? obj[memberKey];
            return item != null;
        }

        if (value is IDictionary dict && value is not string)
        {
            item = GetDictionaryValue(dict, key, lowerKey, memberKey);
            return item != null;
        }

        var type = value.GetType();
        var field = type.GetField(key, BindingFlags.Public | BindingFlags.Instance);
        if (field != null)
        {
            item = field.GetValue(value);
            return true;
        }

        return false;
    }

    /// <summary>从指定 Collection 的 Mod 配置读取优先级（每个行动组单独传入 collection）。</summary>
    public bool TryGetCollectionModPriority(
        string collectionName,
        string modDirectory,
        out int priority)
    {
        priority = 0;
        var normalizedCollection = NormalizePenumbraCollectionName(collectionName);
        if (!TryResolveCollectionId(normalizedCollection, out var collectionId, out _))
            return false;

        if (string.IsNullOrWhiteSpace(modDirectory))
            return false;

        if (TryGetCollectionModPriorityTyped(collectionId, modDirectory, out priority))
            return true;

        if (TryGetCollectionModPriorityFromAllCollections(collectionId, modDirectory, out priority))
            return true;

        return TryGetCollectionModPriorityUntyped(collectionId, modDirectory, out priority);
    }

    private bool TryGetCollectionModPriorityTyped(Guid collectionId, string modDirectory, out int priority)
    {
        priority = 0;
        try
        {
            var modName = ResolveModDisplayName(modDirectory);
            var subscriber = _pluginInterface.GetIpcSubscriber<
                Guid, string, string, bool, (int, (bool, int, IReadOnlyDictionary<string, IReadOnlyList<string>>, bool)?)>(
                GetCurrentModSettingsGate);
            var (ec, settings) = subscriber.InvokeFunc(
                collectionId,
                modDirectory.Trim(),
                modName,
                false);

            if (!IsPenumbraIpcSuccess((PenumbraIpcEc)ec) || settings is not { } tuple)
                return false;

            priority = tuple.Item2;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool TryGetCollectionModPriorityFromAllCollections(Guid collectionId, string modDirectory, out int priority)
    {
        priority = 0;
        try
        {
            var modName = ResolveModDisplayName(modDirectory);
            var subscriber = _pluginInterface.GetIpcSubscriber<
                string, string, bool, (int, Dictionary<Guid, (bool, int, IReadOnlyDictionary<string, IReadOnlyList<string>>, bool, bool)>)>(
                GetSettingsInAllCollectionsGate);
            var (ec, allSettings) = subscriber.InvokeFunc(modDirectory.Trim(), modName, false);

            if (!IsPenumbraIpcSuccess((PenumbraIpcEc)ec)
                || !allSettings.TryGetValue(collectionId, out var modSettings))
                return false;

            priority = modSettings.Item2;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 跨 Collection 查找 Mod 优先级：优先指定 Collection，否则取任意非零 priority。
    /// 用于 SFW 行动组 Collection 配置为 Default 但 Mod 实际在其它 Collection 有优先级时。
    /// </summary>
    private bool TryGetBestModPriorityFromAllCollections(
        Guid preferredCollectionId,
        string modDirectory,
        out int priority)
    {
        priority = 0;
        try
        {
            var modName = ResolveModDisplayName(modDirectory);
            var subscriber = _pluginInterface.GetIpcSubscriber<
                string, string, bool, (int, Dictionary<Guid, (bool, int, IReadOnlyDictionary<string, IReadOnlyList<string>>, bool, bool)>)>(
                GetSettingsInAllCollectionsGate);
            var (ec, allSettings) = subscriber.InvokeFunc(modDirectory.Trim(), modName, false);

            if (!IsPenumbraIpcSuccess((PenumbraIpcEc)ec) || allSettings.Count == 0)
                return false;

            if (preferredCollectionId != Guid.Empty
                && allSettings.TryGetValue(preferredCollectionId, out var preferredSettings))
            {
                priority = preferredSettings.Item2;
                if (priority != 0)
                    return true;
            }

            var bestNonZero = 0;
            foreach (var (_, modSettings) in allSettings)
            {
                if (modSettings.Item2 != 0)
                    bestNonZero = Math.Max(bestNonZero, modSettings.Item2);
            }

            if (bestNonZero != 0)
            {
                priority = bestNonZero;
                return true;
            }

            return preferredCollectionId != Guid.Empty && allSettings.ContainsKey(preferredCollectionId);
        }
        catch
        {
            return false;
        }
    }

    private bool TryGetCollectionModPriorityUntyped(Guid collectionId, string modDirectory, out int priority)
    {
        priority = 0;
        try
        {
            var modName = ResolveModDisplayName(modDirectory);
            var subscriber = _pluginInterface.GetIpcSubscriber<
                Guid, string, string, bool, bool, int, object>(
                GetCurrentModSettingsWithTempGate);
            var raw = subscriber.InvokeFunc(
                collectionId,
                modDirectory.Trim(),
                modName,
                false,
                true,
                0);

            if (!TryUnwrapIpcResult(raw, out var payload, out var resultCode))
                return false;

            if (resultCode is not null
                && TryConvertPenumbraIpcEc(resultCode, out var ec)
                && !IsPenumbraIpcSuccess(ec))
                return false;

            if (payload == null)
                return false;

            return TryExtractPriorityFromTuplePayload(payload, out priority);
        }
        catch
        {
            return false;
        }
    }

    private bool TryGetTemporaryModPriority(
        int playerObjectIndex,
        string modDirectory,
        int lockKey,
        out int priority)
    {
        priority = 0;
        if (!IsIpcAvailable() || string.IsNullOrWhiteSpace(modDirectory))
            return false;

        try
        {
            var subscriber = _pluginInterface.GetIpcSubscriber<int, string, string, int, (int, object?, string)>(
                QueryTemporaryModSettingsPlayerGate);
            var (ec, settingsPayload, _) = subscriber.InvokeFunc(
                playerObjectIndex,
                modDirectory.Trim(),
                ResolveModDisplayName(modDirectory),
                lockKey);

            if ((PenumbraIpcEc)ec != PenumbraIpcEc.Success || settingsPayload == null)
                return false;

            var priorityItem = GetTupleItem3(settingsPayload);
            return TryConvertPenumbraPriority(priorityItem, out priority);
        }
        catch
        {
            return false;
        }
    }

    private int ResolveTemporaryApplyPriority(
        int playerObjectIndex,
        string collectionName,
        string modDirectory,
        int lockKey)
    {
        var normalizedCollection = NormalizePenumbraCollectionName(collectionName);
        int? collectionResolved = null;
        if (TryGetCollectionModPriority(normalizedCollection, modDirectory, out var collectionPriority))
            collectionResolved = collectionPriority;

        int? playerCollectionResolved = null;
        if (playerObjectIndex >= 0
            && TryGetCollectionNameForObject(playerObjectIndex, out var playerCollection)
            && !string.Equals(playerCollection, normalizedCollection, StringComparison.OrdinalIgnoreCase)
            && TryGetCollectionModPriority(playerCollection, modDirectory, out var playerCollectionPriority))
            playerCollectionResolved = playerCollectionPriority;

        int? allCollectionsResolved = null;
        if (TryResolveCollectionId(normalizedCollection, out var preferredCollectionId, out _)
            && TryGetBestModPriorityFromAllCollections(preferredCollectionId, modDirectory, out var bestCollectionPriority))
            allCollectionsResolved = bestCollectionPriority;

        // SFW（-1210）在规则（-1211）之后 apply；若行动组 Collection 为 Default 等导致读到 0，
        // 应继承规则层已写入的正确 priority，而不是被本层旧的 0 锁死。
        int? ruleLayerResolved = null;
        if (lockKey != HeelsDesignLinkerPenumbraLockKey
            && TryGetTemporaryModPriority(
                playerObjectIndex,
                modDirectory,
                HeelsDesignLinkerPenumbraLockKey,
                out var ruleLayerPriority))
            ruleLayerResolved = ruleLayerPriority;

        int? sameLayerResolved = null;
        if (TryGetTemporaryModPriority(playerObjectIndex, modDirectory, lockKey, out var sameLayerPriority))
            sameLayerResolved = sameLayerPriority;

        foreach (var candidate in new int?[]
                 {
                     collectionResolved,
                     playerCollectionResolved,
                     allCollectionsResolved,
                     ruleLayerResolved,
                     sameLayerResolved,
                 })
        {
            if (candidate is int value && value != 0)
                return value;
        }

        return collectionResolved
            ?? playerCollectionResolved
            ?? allCollectionsResolved
            ?? ruleLayerResolved
            ?? sameLayerResolved
            ?? 0;
    }

    public bool TryApplyTemporaryModSettingPlayer(
        int playerObjectIndex,
        string collectionName,
        string modDirectory,
        string optionGroupName,
        string optionName,
        bool optionEnabled,
        PenumbraGroupType groupType,
        out PenumbraIpcEc result,
        out string error) =>
        TryApplyTemporaryModSettingPlayer(
            playerObjectIndex,
            collectionName,
            modDirectory,
            optionGroupName,
            optionName,
            optionEnabled,
            groupType,
            PenumbraTempLayer.HeelsRule,
            out result,
            out error);

    public bool TryApplySfwTemporaryModSettingPlayer(
        int playerObjectIndex,
        string collectionName,
        string modDirectory,
        string optionGroupName,
        string optionName,
        bool optionEnabled,
        PenumbraGroupType groupType,
        out PenumbraIpcEc result,
        out string error) =>
        TryApplyTemporaryModSettingPlayer(
            playerObjectIndex,
            collectionName,
            modDirectory,
            optionGroupName,
            optionName,
            optionEnabled,
            groupType,
            PenumbraTempLayer.SfwMode,
            out result,
            out error);

    private bool TryApplyTemporaryModSettingPlayer(
        int playerObjectIndex,
        string collectionName,
        string modDirectory,
        string optionGroupName,
        string optionName,
        bool optionEnabled,
        PenumbraGroupType groupType,
        PenumbraTempLayer layer,
        out PenumbraIpcEc result,
        out string error)
    {
        result = PenumbraIpcEc.UnknownError;
        error = "";

        var trimmedGroup = optionGroupName.Trim();
        var trimmedOption = optionName.Trim();
        List<string> targetNames;
        if (UsesBoolOptionValue(groupType))
        {
            if (!TryGetEffectiveModSettingsForTempApply(
                    collectionName,
                    modDirectory,
                    out _,
                    out var currentSettings,
                    out error))
            {
                return false;
            }

            var currentNames = currentSettings.TryGetValue(trimmedGroup, out var existing)
                ? existing.ToList()
                : [];
            if (optionEnabled)
            {
                if (!currentNames.Any(name =>
                        string.Equals(name, trimmedOption, StringComparison.OrdinalIgnoreCase)))
                {
                    currentNames.Add(trimmedOption);
                }
            }
            else
            {
                currentNames = currentNames
                    .Where(name => !string.Equals(name, trimmedOption, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            targetNames = currentNames;
        }
        else
        {
            targetNames = !string.IsNullOrWhiteSpace(trimmedOption)
                ? [trimmedOption]
                : [];
        }

        return TryApplyTemporaryModConfigurationPlayer(
            playerObjectIndex,
            collectionName,
            modDirectory,
            enabled: true,
            new Dictionary<string, IReadOnlyList<string>>
            {
                [trimmedGroup] = targetNames,
            },
            mergeExistingTemporaryGroups: true,
            layer,
            out result,
            out error);
    }

    public bool TryApplyTemporaryMultiToggleSettingsPlayer(
        int playerObjectIndex,
        string collectionName,
        string modDirectory,
        string optionGroupName,
        IReadOnlyList<string> enabledOptionNames,
        out PenumbraIpcEc result,
        out string error) =>
        TryApplyTemporaryMultiToggleSettingsPlayer(
            playerObjectIndex,
            collectionName,
            modDirectory,
            optionGroupName,
            enabledOptionNames,
            PenumbraTempLayer.HeelsRule,
            out result,
            out error);

    public bool TryApplySfwTemporaryMultiToggleSettingsPlayer(
        int playerObjectIndex,
        string collectionName,
        string modDirectory,
        string optionGroupName,
        IReadOnlyList<string> enabledOptionNames,
        out PenumbraIpcEc result,
        out string error) =>
        TryApplyTemporaryMultiToggleSettingsPlayer(
            playerObjectIndex,
            collectionName,
            modDirectory,
            optionGroupName,
            enabledOptionNames,
            PenumbraTempLayer.SfwMode,
            out result,
            out error);

    private bool TryApplyTemporaryMultiToggleSettingsPlayer(
        int playerObjectIndex,
        string collectionName,
        string modDirectory,
        string optionGroupName,
        IReadOnlyList<string> enabledOptionNames,
        PenumbraTempLayer layer,
        out PenumbraIpcEc result,
        out string error)
    {
        result = PenumbraIpcEc.UnknownError;
        error = "";

        var trimmedGroup = optionGroupName.Trim();
        var targetNames = enabledOptionNames
            .Select(name => name.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return TryApplyTemporaryModConfigurationPlayer(
            playerObjectIndex,
            collectionName,
            modDirectory,
            enabled: true,
            new Dictionary<string, IReadOnlyList<string>>
            {
                [trimmedGroup] = targetNames,
            },
            mergeExistingTemporaryGroups: true,
            layer,
            out result,
            out error);
    }

    private static (string Source, int LockKey) GetTempLayerParams(PenumbraTempLayer layer) =>
        layer == PenumbraTempLayer.SfwMode
            ? (SfwModePenumbraSource, SfwModePenumbraLockKey)
            : (HeelsDesignLinkerPenumbraSource, HeelsDesignLinkerPenumbraLockKey);

    private bool TryMergeTemporaryOptionOverrides(
        string collectionName,
        string modDirectory,
        int playerObjectIndex,
        int lockKey,
        bool mergeExistingTemporaryGroups,
        IReadOnlyDictionary<string, IReadOnlyList<string>> optionGroupOverrides,
        out Dictionary<string, IReadOnlyList<string>> mergedOverrides,
        out string error)
    {
        mergedOverrides = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        error = "";

        if (!TryGetCollectionModOptionSnapshot(collectionName, modDirectory, out var collectionBaseline, out error))
            return false;

        Dictionary<string, List<string>>? existingSameKeyTemp = null;
        if (mergeExistingTemporaryGroups
            && TryGetTemporaryModSettingsPlayer(playerObjectIndex, modDirectory, lockKey, out var tempSettings))
            existingSameKeyTemp = tempSettings;

        var merged = PenumbraTemporaryOptionMerge.Merge(
            collectionBaseline,
            existingSameKeyTemp,
            optionGroupOverrides);

        foreach (var (group, names) in merged)
            mergedOverrides[group] = names;

        return true;
    }

    /// <summary>读取 Collection 内该 Mod 的完整选项快照（不含任何临时层）。</summary>
    private bool TryGetCollectionModOptionSnapshot(
        string collectionName,
        string modDirectory,
        out Dictionary<string, List<string>> settingsByGroup,
        out string error)
    {
        settingsByGroup = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        error = "";

        if (string.IsNullOrWhiteSpace(modDirectory))
        {
            error = "Mod not selected";
            return false;
        }

        var trimmedModDirectory = modDirectory.Trim();

        // 优先 per-mod GetCurrentModSettings（ignoreTemporary），避免 GetAllModSettings 快照缺组导致临时层其它组被关。
        if (TryGetCollectionModConfigurationForImport(
                collectionName,
                trimmedModDirectory,
                out _,
                out var importSettings,
                out error))
        {
            foreach (var (group, names) in importSettings)
                settingsByGroup[group] = names.ToList();

            return true;
        }

        if (!TryResolveCollectionId(collectionName, out var collectionId, out error))
            return false;

        try
        {
            var subscriber = _pluginInterface.GetIpcSubscriber<
                Guid, bool, bool, int, (int, Dictionary<string, (bool, int, IReadOnlyDictionary<string, IReadOnlyList<string>>, bool, bool)>?)>(
                GetAllModSettingsGate);
            var (ec, allMods) = subscriber.InvokeFunc(collectionId, false, true, 0);

            if (IsPenumbraIpcSuccess((PenumbraIpcEc)ec)
                && allMods != null
                && TryFindModSettingsInAllModsSnapshot(allMods, trimmedModDirectory, out settingsByGroup))
                return true;
        }
        catch (Exception ex)
        {
            error = $"读取 Penumbra Collection 快照失败: {ex.Message}";
        }

        return settingsByGroup.Count > 0;
    }

    private static bool TryFindModSettingsInAllModsSnapshot(
        IReadOnlyDictionary<string, (bool, int, IReadOnlyDictionary<string, IReadOnlyList<string>>, bool, bool)> allMods,
        string modDirectory,
        out Dictionary<string, List<string>> settingsByGroup)
    {
        settingsByGroup = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (modKey, modSettings) in allMods)
        {
            if (!string.Equals(modKey, modDirectory, StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var (group, names) in modSettings.Item3)
                settingsByGroup[group] = names
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Select(name => name.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

            return true;
        }

        return false;
    }

    private bool TryGetEffectiveModSettingsForTempApply(
        string collectionName,
        string modDirectory,
        out bool modEnabled,
        out Dictionary<string, List<string>> settings,
        out string error)
    {
        modEnabled = false;
        settings = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        error = "";

        if (!TryGetCurrentModConfiguration(
                collectionName,
                modDirectory,
                out modEnabled,
                out var settingsByGroup,
                out error))
        {
            return false;
        }

        foreach (var (group, names) in settingsByGroup)
            settings[group] = names.ToList();

        return true;
    }

    /// <summary>
    /// 将规则目标选项组解析为 Penumbra 可接受的 canonical 组名/选项名；无元数据时仅做 trim。
    /// </summary>
    public bool TryGetEffectiveModSettingsForCompare(
        string collectionName,
        string modDirectory,
        int? playerObjectIndex,
        out bool modEnabled,
        out IReadOnlyDictionary<string, List<string>> settingsByGroup,
        out string error)
    {
        modEnabled = false;
        settingsByGroup = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        error = "";

        if (!TryGetCurrentModConfiguration(
                collectionName,
                modDirectory,
                out modEnabled,
                out settingsByGroup,
                out error))
            return false;

        if (!ReadIncludingTemporarySettings || playerObjectIndex is not int playerIndex)
            return true;

        if (!TryGetTemporaryModSettingsPlayer(
                playerIndex,
                modDirectory,
                TemporarySettingsReadKey,
                out var playerTemp))
            return true;

        var merged = settingsByGroup.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToList(),
            StringComparer.OrdinalIgnoreCase);
        foreach (var (group, names) in playerTemp)
            merged[group] = names.ToList();

        settingsByGroup = merged;
        return true;
    }

    private static bool TryGetGroupOptionNames(
        IReadOnlyDictionary<string, List<string>> settingsByGroup,
        string optionGroupName,
        out List<string> optionNames)
    {
        optionNames = [];
        if (settingsByGroup.TryGetValue(optionGroupName, out var direct))
        {
            optionNames = direct;
            return true;
        }

        foreach (var (group, names) in settingsByGroup)
        {
            if (!string.Equals(group, optionGroupName, StringComparison.OrdinalIgnoreCase))
                continue;

            optionNames = names;
            return true;
        }

        return false;
    }

    private bool TryBuildSanitizedTemporaryOptionOverrides(
        string modDirectory,
        IReadOnlyDictionary<string, IReadOnlyList<string>> optionGroupOverrides,
        IReadOnlySet<string> requiredOverrideGroups,
        out Dictionary<string, List<string>> settings,
        out string error)
    {
        settings = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        error = "";

        if (string.IsNullOrWhiteSpace(modDirectory))
        {
            error = "Mod not selected";
            return false;
        }

        var modMap = GetModSettingsMap(modDirectory.Trim(), force: true);
        var hasMetadata = modMap.Count > 0;
        var satisfiedRequired = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (rawGroup, rawNames) in optionGroupOverrides)
        {
            if (string.IsNullOrWhiteSpace(rawGroup))
                continue;

            var groupKey = rawGroup.Trim();
            var isRequired = requiredOverrideGroups.Contains(rawGroup)
                || requiredOverrideGroups.Contains(groupKey);
            PenumbraOptionGroupInfo? groupInfo = null;
            var groupKnown = !hasMetadata
                || TryResolveCanonicalOptionGroup(modMap, groupKey, out groupKey, out groupInfo);

            if (hasMetadata && !groupKnown)
            {
                if (isRequired)
                {
                    error = $"Penumbra option group not found: {rawGroup.Trim()}";
                    return false;
                }

                continue;
            }

            var names = new List<string>();
            foreach (var rawName in rawNames)
            {
                if (string.IsNullOrWhiteSpace(rawName))
                    continue;

                var trimmed = rawName.Trim();
                if (hasMetadata && groupInfo != null)
                {
                    var canonical = groupInfo.Options.FirstOrDefault(option =>
                        string.Equals(option, trimmed, StringComparison.OrdinalIgnoreCase));
                    if (canonical == null)
                    {
                        if (isRequired)
                        {
                            error = $"Penumbra option not found: {rawGroup.Trim()} → {trimmed}";
                            return false;
                        }

                        continue;
                    }

                    names.Add(canonical);
                }
                else
                {
                    names.Add(trimmed);
                }
            }

            if (hasMetadata && groupInfo != null)
            {
                if (!UsesBoolOptionValue(groupInfo.GroupType) && names.Count == 0)
                {
                    if (isRequired)
                    {
                        error = $"Penumbra single-select group requires an option name: {rawGroup.Trim()}";
                        return false;
                    }

                    continue;
                }
            }
            else if (names.Count == 0 && isRequired)
            {
                error = $"Penumbra option group has no valid option names: {rawGroup.Trim()}";
                return false;
            }

            settings[groupKey] = NormalizeOptionNames(names);
            if (isRequired)
                satisfiedRequired.Add(rawGroup.Trim());
        }

        foreach (var requiredGroup in requiredOverrideGroups)
        {
            if (string.IsNullOrWhiteSpace(requiredGroup))
                continue;

            if (satisfiedRequired.Contains(requiredGroup.Trim()))
                continue;

            error = $"Penumbra option group was not applied: {requiredGroup.Trim()}";
            return false;
        }

        if (settings.Count == 0)
        {
            error = "No valid Penumbra option overrides (check option group / option names)";
            return false;
        }

        return true;
    }

    private static bool TryResolveCanonicalOptionGroup(
        Dictionary<string, PenumbraOptionGroupInfo> modMap,
        string groupKey,
        out string canonicalGroup,
        out PenumbraOptionGroupInfo groupInfo)
    {
        if (modMap.TryGetValue(groupKey, out groupInfo!))
        {
            canonicalGroup = groupKey;
            return true;
        }

        foreach (var (key, info) in modMap)
        {
            if (!string.Equals(key, groupKey, StringComparison.OrdinalIgnoreCase))
                continue;

            canonicalGroup = key;
            groupInfo = info;
            return true;
        }

        canonicalGroup = groupKey;
        groupInfo = new PenumbraOptionGroupInfo([], PenumbraGroupType.Single);
        return false;
    }

    private bool TrySetTemporaryModSettingsPlayer(
        int playerObjectIndex,
        string modDirectory,
        bool forceInherit,
        bool enabled,
        int priority,
        Dictionary<string, List<string>> settings,
        string source,
        int lockKey,
        out PenumbraIpcEc result,
        out string error)
    {
        result = PenumbraIpcEc.UnknownError;
        error = "";

        if (string.IsNullOrWhiteSpace(modDirectory))
        {
            error = "Mod not selected";
            return false;
        }

        try
        {
            var ipcSettings = settings.ToDictionary(
                kv => kv.Key,
                kv => (IReadOnlyList<string>)kv.Value,
                StringComparer.OrdinalIgnoreCase);

            // Penumbra.Api V5 gate: objectIndex, modDirectory, modName, (inherit, enabled, priority, options), source, key
            var subscriber = _pluginInterface.GetIpcSubscriber<
                int,
                string,
                string,
                (bool, bool, int, IReadOnlyDictionary<string, IReadOnlyList<string>>),
                string,
                int,
                int>(SetTemporaryModSettingsPlayerGate);
            var ipcResult = subscriber.InvokeFunc(
                playerObjectIndex,
                modDirectory.Trim(),
                ResolveModDisplayName(modDirectory),
                (forceInherit, enabled, priority, ipcSettings),
                source,
                lockKey);

            result = (PenumbraIpcEc)ipcResult;
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
}
