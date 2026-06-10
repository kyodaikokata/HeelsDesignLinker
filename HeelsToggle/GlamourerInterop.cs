using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Newtonsoft.Json.Linq;

namespace HeelsDesignLinker;

/// <summary>
/// 通过 Glamourer IPC 读取设计 JSON，判断是否会应用脚部装备槽，以及检测装备状态。
/// </summary>
internal sealed class GlamourerInterop
{
    private const string GetDesignListGate = "Glamourer.GetDesignList.V2";
    private const string GetDesignJObjectGate = "Glamourer.GetDesignJObject";
    // 尝试多种 GetState 方法以兼容不同版本
    private const string GetStateGate = "Glamourer.GetState";
    private const string GetStateBase64Gate = "Glamourer.GetStateBase64";
    private const string GetAllCustomizationGate = "Glamourer.GetAllCustomization";
    private const string GetAllActorsGate = "Glamourer.GetAllActors";
    // Glamourer 状态变化事件（使用正确的版本化名称）
    private const string StateChangedGate = "Glamourer.StateChanged.V2";
    private const string StateChangedWithTypeGate = "Glamourer.StateChangedWithType";
    private const string StateFinalizedGate = "Glamourer.StateFinalized";
    private const string GPoseChangedGate = "Glamourer.GPoseChanged";

    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly IObjectTable? _objectTable;
    private readonly Dictionary<string, bool> _feetApplyCache = new(StringComparer.OrdinalIgnoreCase);
    // design GUID → 实际应用（Apply=true）的装备槽位集合缓存
    private readonly Dictionary<Guid, HashSet<EquipSlot>> _designSlotCache = new();

    /// <summary>design GUID → 该设计关联（Mod Associations）的 Penumbra Mod 目录集合。</summary>
    private readonly Dictionary<Guid, HashSet<string>> _designModCache = new();

    private static readonly EquipSlot[] AllEquipSlots =
    {
        EquipSlot.MainHand, EquipSlot.OffHand, EquipSlot.Head, EquipSlot.Body,
        EquipSlot.Hands, EquipSlot.Legs, EquipSlot.Feet, EquipSlot.Ears,
        EquipSlot.Neck, EquipSlot.Wrists, EquipSlot.RFinger, EquipSlot.LFinger,
    };
    private List<string> _designNames = [];
    private DateTime _designListFetchedUtc = DateTime.MinValue;
    private static readonly TimeSpan DesignListRefreshInterval = TimeSpan.FromSeconds(30);
    
    // 状态变化事件回调
    public event Action? OnStateChanged;
    
    // 用于调试：记录事件触发次数
    public int StateChangedEventCount { get; private set; }
    public int StateChangedWithTypeEventCount { get; private set; }
    public int StateFinalizedEventCount { get; private set; }
    public int GPoseChangedEventCount { get; private set; }
    
    // 用于调试：记录 GetState 方法尝试结果
    public List<string> GetStateAttempts { get; private set; } = new();
    
    // 用于调试：记录 Glamourer API 版本检测
    public string GlamourerApiVersion { get; private set; } = "Unknown";
    public List<string> AvailableGetStateMethods { get; private set; } = new();
    public List<string> EventSubscriptionStatus { get; private set; } = new();
    
    // Glamourer 状态缓存
    private JObject? _cachedPlayerState;
    private DateTime _stateLastFetchedUtc = DateTime.MinValue;
    private static readonly TimeSpan StateCacheTimeout = TimeSpan.FromMilliseconds(100); // 短暂缓存，避免同一帧多次查询

    public GlamourerInterop(IDalamudPluginInterface pluginInterface, IObjectTable? objectTable = null)
    {
        _pluginInterface = pluginInterface;
        _objectTable = objectTable;
        DetectGlamourerApiVersion();
        SubscribeToStateChanged();
    }
    
    /// <summary>
    /// 检测 Glamourer 的 API 版本和可用方法
    /// </summary>
    private void DetectGlamourerApiVersion()
    {
        AvailableGetStateMethods.Clear();
        
        // 尝试检测各种 GetState 方法
        var methodsToTest = new[]
        {
            "Glamourer.GetState",
            "Glamourer.GetStateBase64",
            "Glamourer.GetAllCustomization",
            "Glamourer.GetAllCustomizationFromCharacter",
            "Glamourer.ApiVersion",
            "Glamourer.ApiVersions"
        };
        
        foreach (var method in methodsToTest)
        {
            try
            {
                // 尝试获取 IPC subscriber（不实际调用）
                var testSub = _pluginInterface.GetIpcSubscriber<object>(method);
                AvailableGetStateMethods.Add($"✓ {method}");
            }
            catch
            {
                AvailableGetStateMethods.Add($"✗ {method}");
            }
        }
        
        // 尝试获取 API 版本
        try
        {
            var versionSub = _pluginInterface.GetIpcSubscriber<(int, int)>("Glamourer.ApiVersions");
            var versions = versionSub.InvokeFunc();
            GlamourerApiVersion = $"{versions.Item1}.{versions.Item2}";
        }
        catch
        {
            try
            {
                var versionSub = _pluginInterface.GetIpcSubscriber<int>("Glamourer.ApiVersion");
                var version = versionSub.InvokeFunc();
                GlamourerApiVersion = $"{version}";
            }
            catch
            {
                GlamourerApiVersion = "Cannot detect";
            }
        }
    }

    private void SubscribeToStateChanged()
    {
        EventSubscriptionStatus.Clear();
        
        // 根据 Glamourer API 源码订阅事件
        // 事件签名：Action<T>（无返回值）
        
        // 事件 1: StateChanged.V2 (nint actorAddress)
        try
        {
            // Dalamud 事件订阅：最后一个泛型参数应该是 object（不是 object?）
            // 对于事件，这表示 Action<nint>
            var subscriber = _pluginInterface.GetIpcSubscriber<nint, object>(StateChangedGate);
            subscriber.Subscribe(OnGlamourerStateChangedV2);
            EventSubscriptionStatus.Add("✓ StateChanged.V2 (nint)");
        }
        catch (Exception ex)
        {
            EventSubscriptionStatus.Add($"✗ StateChanged.V2: {ex.Message}");
        }
        
        // 事件 2: StateChangedWithType (nint actorAddress, int changeType)
        try
        {
            var subscriber = _pluginInterface.GetIpcSubscriber<nint, int, object>(StateChangedWithTypeGate);
            subscriber.Subscribe(OnGlamourerStateChangedWithType);
            EventSubscriptionStatus.Add("✓ StateChangedWithType (nint, int)");
        }
        catch (Exception ex)
        {
            EventSubscriptionStatus.Add($"✗ StateChangedWithType: {ex.Message}");
        }
        
        // 事件 3: StateFinalized (nint actorAddress)
        try
        {
            var subscriber = _pluginInterface.GetIpcSubscriber<nint, object>(StateFinalizedGate);
            subscriber.Subscribe(OnGlamourerStateFinalized);
            EventSubscriptionStatus.Add("✓ StateFinalized (nint)");
        }
        catch (Exception ex)
        {
            EventSubscriptionStatus.Add($"✗ StateFinalized: {ex.Message}");
        }
        
        // 事件 4: GPoseChanged (bool inGPose)
        try
        {
            var subscriber = _pluginInterface.GetIpcSubscriber<bool, object>(GPoseChangedGate);
            subscriber.Subscribe(OnGlamourerGPoseChanged);
            EventSubscriptionStatus.Add("✓ GPoseChanged (bool)");
        }
        catch (Exception ex)
        {
            EventSubscriptionStatus.Add($"✗ GPoseChanged: {ex.Message}");
        }
    }

    private void OnGlamourerStateChangedV2(nint actorAddress)
    {
        StateChangedEventCount++;
        _cachedPlayerState = null;
        _stateLastFetchedUtc = DateTime.MinValue;
        OnStateChanged?.Invoke();
    }
    
    private void OnGlamourerStateChangedWithType(nint actorAddress, int changeType)
    {
        StateChangedWithTypeEventCount++;
        _cachedPlayerState = null;
        _stateLastFetchedUtc = DateTime.MinValue;
        OnStateChanged?.Invoke();
    }
    
    private void OnGlamourerStateFinalized(nint actorAddress)
    {
        StateFinalizedEventCount++;
        _cachedPlayerState = null;
        _stateLastFetchedUtc = DateTime.MinValue;
        OnStateChanged?.Invoke();
    }
    
    private void OnGlamourerGPoseChanged(bool inGPose)
    {
        GPoseChangedEventCount++;
        // GPose 状态改变，可能需要重新应用状态
    }

    public void Dispose()
    {
        // 取消订阅所有事件
        try
        {
            var subscriber = _pluginInterface.GetIpcSubscriber<nint, object?>(StateChangedGate);
            subscriber.Unsubscribe(OnGlamourerStateChangedV2);
        }
        catch { }
        
        try
        {
            var subscriber = _pluginInterface.GetIpcSubscriber<nint, int, object?>(StateChangedWithTypeGate);
            subscriber.Unsubscribe(OnGlamourerStateChangedWithType);
        }
        catch { }
        
        try
        {
            var subscriber = _pluginInterface.GetIpcSubscriber<nint, object?>(StateFinalizedGate);
            subscriber.Unsubscribe(OnGlamourerStateFinalized);
        }
        catch { }
        
        try
        {
            var subscriber = _pluginInterface.GetIpcSubscriber<bool, object?>(GPoseChangedGate);
            subscriber.Unsubscribe(OnGlamourerGPoseChanged);
        }
        catch { }
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
        // design 槽位/关联 Mod 缓存按 GUID 存储，无法按名精确移除，统一清空（design 编辑后重新读取）。
        _designSlotCache.Clear();
        _designModCache.Clear();

        if (string.IsNullOrWhiteSpace(designName))
        {
            _feetApplyCache.Clear();
            return;
        }

        _feetApplyCache.Remove(designName.Trim());
    }

    /// <summary>
    /// 读取某 Glamourer design（按名称）实际会应用（Apply=true）的装备槽位集合。
    /// 仅统计 Equipment.* 装备槽位，不含 Customize/Parameters。返回 null 表示无法解析。
    /// </summary>
    public HashSet<EquipSlot>? GetDesignAppliedEquipmentSlotsByName(string? designName)
    {
        if (string.IsNullOrWhiteSpace(designName))
            return null;

        var guid = GetDesignGuidByName(designName.Trim());
        if (guid == null || guid.Value == Guid.Empty)
            return null;

        return GetDesignAppliedEquipmentSlots(guid.Value);
    }

    /// <summary>
    /// 读取某 Glamourer design（按 GUID）实际会应用（Apply=true）的装备槽位集合。返回 null 表示无法解析。
    /// </summary>
    public HashSet<EquipSlot>? GetDesignAppliedEquipmentSlots(Guid designId)
    {
        if (designId == Guid.Empty)
            return null;

        if (_designSlotCache.TryGetValue(designId, out var cached))
            return cached;

        if (!IsIpcAvailable())
            return null;

        try
        {
            var designJson = GetDesignJObject(designId);
            if (designJson == null)
                return null;

            var slots = new HashSet<EquipSlot>();
            foreach (var slot in AllEquipSlots)
            {
                var applyToken = designJson.SelectToken($"Equipment.{slot}.Apply");
                if (applyToken?.Type == JTokenType.Boolean && applyToken.ToObject<bool>())
                    slots.Add(slot);
            }

            _designSlotCache[designId] = slots;
            return slots;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>读取某 Glamourer design（按名称）关联（Mod Associations）的 Penumbra Mod 目录集合。</summary>
    public HashSet<string>? GetDesignAssociatedModDirectoriesByName(string? designName)
    {
        if (string.IsNullOrWhiteSpace(designName))
            return null;

        var guid = GetDesignGuidByName(designName.Trim());
        if (guid == null || guid.Value == Guid.Empty)
            return null;

        return GetDesignAssociatedModDirectories(guid.Value);
    }

    /// <summary>
    /// 读取某 Glamourer design（按 GUID）关联的 Penumbra Mod 目录集合（design JObject 的 "Mods" 关联）。
    /// 返回空集合表示该设计无关联 Mod；返回 null 表示无法解析。
    /// </summary>
    public HashSet<string>? GetDesignAssociatedModDirectories(Guid designId)
    {
        if (designId == Guid.Empty)
            return null;

        if (_designModCache.TryGetValue(designId, out var cached))
            return cached;

        if (!IsIpcAvailable())
            return null;

        try
        {
            var designJson = GetDesignJObject(designId);
            if (designJson == null)
                return null;

            var mods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var modsToken = designJson.SelectToken("Mods");
            if (modsToken is JArray modArray)
            {
                foreach (var entry in modArray)
                {
                    var dir = entry.SelectToken("Directory")?.ToString();
                    if (!string.IsNullOrWhiteSpace(dir))
                        mods.Add(dir.Trim());
                }
            }
            else if (modsToken is JObject modObject)
            {
                // 兼容以目录为键的对象映射形式。
                foreach (var prop in modObject.Properties())
                {
                    if (!string.IsNullOrWhiteSpace(prop.Name))
                        mods.Add(prop.Name.Trim());
                }
            }

            _designModCache[designId] = mods;
            return mods;
        }
        catch
        {
            return null;
        }
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

    public bool DoesDesignApplyToFeet(Guid designId)
    {
        if (designId == Guid.Empty)
            return false;

        if (!IsIpcAvailable())
            return false;

        try
        {
            var designJson = GetDesignJObject(designId);
            if (designJson == null)
                return false;

            var applyToken = designJson.SelectToken("Equipment.Feet.Apply");
            return applyToken?.Type == JTokenType.Boolean && applyToken.ToObject<bool>();
        }
        catch
        {
            return false;
        }
    }

    private Dictionary<Guid, string> GetDesignMap()
    {
        var subscriber = _pluginInterface.GetIpcSubscriber<Dictionary<Guid, string>>(GetDesignListGate);
        return subscriber.InvokeFunc() ?? [];
    }
    
    /// <summary>
    /// 通过设计名称查找对应的 GUID
    /// </summary>
    public Guid? GetDesignGuidByName(string designName)
    {
        if (string.IsNullOrWhiteSpace(designName))
            return null;
        
        try
        {
            var designMap = GetDesignMap();
            var entry = designMap.FirstOrDefault(kvp => 
                string.Equals(kvp.Value, designName, StringComparison.OrdinalIgnoreCase));
            
            if (entry.Key != Guid.Empty)
                return entry.Key;
            
            return null;
        }
        catch
        {
            return null;
        }
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
    
    /// <summary>
    /// 获取本地玩家的 Glamourer 状态（尝试多种方法）
    /// </summary>
    public JObject? GetLocalPlayerState(bool forceRefresh = false)
    {
        // 使用缓存，避免同一帧多次查询
        if (!forceRefresh && _cachedPlayerState != null && 
            (DateTime.UtcNow - _stateLastFetchedUtc) < StateCacheTimeout)
        {
            return _cachedPlayerState;
        }
        
        // 检查 Glamourer 是否加载（但不检查特定 IPC，因为 GetState 和 GetDesign 是不同的 API）
        if (!IsGlamourerLoaded(_pluginInterface))
        {
            GetStateAttempts.Clear();
            GetStateAttempts.Add("✗ Glamourer not loaded");
            return null;
        }
        
        GetStateAttempts.Clear();
        
        // 尝试获取本地玩家对象地址
        nint playerAddress = nint.Zero;
        if (_objectTable != null)
        {
            try
            {
                var localPlayer = _objectTable.LocalPlayer;
                if (localPlayer != null)
                {
                    playerAddress = localPlayer.Address;
                }
            }
            catch
            {
                // 无法获取玩家地址
            }
        }
        
        // 方法 1: 使用 GetStateBase64 (objectIndex, key) → (errorCode, base64String)
        try
        {
            var subscriber = _pluginInterface.GetIpcSubscriber<int, uint, (int, string?)>("Glamourer.GetStateBase64");
            
            // 安全调用，捕获任何异常
            object? result = null;
            try
            {
                result = subscriber.InvokeFunc(0, 0);
            }
            catch (Exception invokeEx)
            {
                GetStateAttempts.Add($"✗ Method 1 (GetStateBase64) InvokeFunc failed: {invokeEx.Message}");
                goto Method2; // 跳到下一个方法
            }
            
            if (result == null)
            {
                GetStateAttempts.Add("✗ Method 1 (GetStateBase64): Result is null");
                goto Method2;
            }
            
            // 尝试解构 tuple
            int errorCode;
            string? base64State;
            try
            {
                var tuple = ((int, string?))result;
                errorCode = tuple.Item1;
                base64State = tuple.Item2;
            }
            catch (Exception castEx)
            {
                GetStateAttempts.Add($"✗ Method 1 (GetStateBase64) Cast failed: {castEx.Message}");
                goto Method2;
            }
            
            if (errorCode == 0 && !string.IsNullOrEmpty(base64State))
            {
                try
                {
                    var jsonBytes = Convert.FromBase64String(base64State);
                    var jsonString = System.Text.Encoding.UTF8.GetString(jsonBytes);
                    var state = JObject.Parse(jsonString);
                    if (state != null)
                    {
                        _cachedPlayerState = state;
                        _stateLastFetchedUtc = DateTime.UtcNow;
                        GetStateAttempts.Add("✓ Method 1 (GetStateBase64) SUCCESS");
                        return state;
                    }
                }
                catch (Exception parseEx)
                {
                    GetStateAttempts.Add($"✗ Method 1 (GetStateBase64) Parse failed: {parseEx.Message}");
                }
            }
            else
            {
                GetStateAttempts.Add($"✗ Method 1 (GetStateBase64): ErrorCode={errorCode}, Data={(base64State == null ? "null" : "not null")}");
            }
        }
        catch (Exception ex)
        {
            GetStateAttempts.Add($"✗ Method 1 (GetStateBase64): {ex.Message}");
        }
        
        Method2:
        
        
        // 方法 2: 使用 GetState (objectIndex, key) → (errorCode, JObject)
        try
        {
            var subscriber = _pluginInterface.GetIpcSubscriber<int, uint, (int, JObject?)>("Glamourer.GetState");
            
            object? result = null;
            try
            {
                result = subscriber.InvokeFunc(0, 0);
            }
            catch (Exception invokeEx)
            {
                GetStateAttempts.Add($"✗ Method 2 (GetState) InvokeFunc failed: {invokeEx.Message}");
                goto Method3;
            }
            
            if (result == null)
            {
                GetStateAttempts.Add("✗ Method 2 (GetState): Result is null");
                goto Method3;
            }
            
            int errorCode;
            JObject? state;
            try
            {
                var tuple = ((int, JObject?))result;
                errorCode = tuple.Item1;
                state = tuple.Item2;
            }
            catch (Exception castEx)
            {
                GetStateAttempts.Add($"✗ Method 2 (GetState) Cast failed: {castEx.Message}");
                goto Method3;
            }
            
            if (errorCode == 0 && state != null)
            {
                _cachedPlayerState = state;
                _stateLastFetchedUtc = DateTime.UtcNow;
                GetStateAttempts.Add("✓ Method 2 (GetState) SUCCESS");
                return state;
            }
            GetStateAttempts.Add($"✗ Method 2 (GetState): ErrorCode={errorCode}, Data={(state == null ? "null" : "not null")}");
        }
        catch (Exception ex)
        {
            GetStateAttempts.Add($"✗ Method 2 (GetState): {ex.Message}");
        }
        
        Method3:
        
        // 方法 3: 尝试从 GetAllCustomization 中获取第一个角色
        try
        {
            var subscriber2 = _pluginInterface.GetIpcSubscriber<JObject>(GetAllCustomizationGate);
            var allCustomization = subscriber2.InvokeFunc();
            
            if (allCustomization != null)
            {
                // GetAllCustomization 返回所有角色的自定义数据
                // 通常第一个就是本地玩家
                var firstActor = allCustomization.Properties().FirstOrDefault();
                if (firstActor != null && firstActor.Value is JObject actorState)
                {
                    _cachedPlayerState = actorState;
                    _stateLastFetchedUtc = DateTime.UtcNow;
                    GetStateAttempts.Add("✓ Method 3 (GetAllCustomization) SUCCESS");
                    return actorState;
                }
            }
            GetStateAttempts.Add("✗ Method 3 (GetAllCustomization): Empty result");
        }
        catch (Exception ex)
        {
            GetStateAttempts.Add($"✗ Method 3 (GetAllCustomization): {ex.Message}");
        }
        
        // 方法 4: 尝试从 GetAllActors 中获取第一个角色
        try
        {
            var subscriber3 = _pluginInterface.GetIpcSubscriber<JObject>(GetAllActorsGate);
            var allActors = subscriber3.InvokeFunc();
            
            if (allActors != null)
            {
                // GetAllActors 返回所有附近角色
                // 第一个通常是本地玩家
                var firstActor = allActors.Properties().FirstOrDefault();
                if (firstActor != null && firstActor.Value is JObject actorState)
                {
                    _cachedPlayerState = actorState;
                    _stateLastFetchedUtc = DateTime.UtcNow;
                    return actorState;
                }
            }
        }
        catch
        {
            // 方法 3 失败，继续尝试
        }
        
        // 方法 4: 尝试使用空字符串参数
        try
        {
            var subscriber4 = _pluginInterface.GetIpcSubscriber<string, JObject?>(GetStateGate);
            var state = subscriber4.InvokeFunc("");
            if (state != null)
            {
                _cachedPlayerState = state;
                _stateLastFetchedUtc = DateTime.UtcNow;
                return state;
            }
        }
        catch
        {
            // 方法 4 失败，继续尝试
        }
        
        // 方法 5: 尝试无参数版本
        try
        {
            var subscriber5 = _pluginInterface.GetIpcSubscriber<JObject?>(GetStateGate);
            var state = subscriber5.InvokeFunc();
            if (state != null)
            {
                _cachedPlayerState = state;
                _stateLastFetchedUtc = DateTime.UtcNow;
                return state;
            }
        }
        catch
        {
            // 所有方法都失败了
        }
            
        return null;
    }
    
    /// <summary>
    /// 获取角色当前状态（包含所有装备槽位信息）
    /// </summary>
    public JObject? GetCurrentState(string characterName)
    {
        if (!IsIpcAvailable())
            return null;
            
        try
        {
            var subscriber = _pluginInterface.GetIpcSubscriber<string, JObject?>(GetStateGate);
            return subscriber.InvokeFunc(characterName);
        }
        catch
        {
            // 如果基于角色名失败，尝试使用本地玩家方法
            return GetLocalPlayerState();
        }
    }
    
    /// <summary>
    /// 检测本地玩家指定装备槽位是否有装备（使用 Glamourer 投影状态）
    /// 优先使用 Glamourer State（包含 Glamourer 复写和实际投影）
    /// 如果 Glamourer 不可用，使用 DrawData 获取渲染外观（包括游戏内投影）
    /// 皇帝套（隐形装备）将被视为未装备
    /// </summary>
    private static bool IsRealInventoryEquipment(uint itemId) =>
        itemId != 0 && !EmperorsNewItems.IsEmperorsNewByItemId(itemId);

    private unsafe bool? TryGetInventoryHasRealEquipment(EquipSlot slot)
    {
        var inventoryManager = FFXIVClientStructs.FFXIV.Client.Game.InventoryManager.Instance();
        if (inventoryManager == null)
            return null;

        var container = inventoryManager->GetInventoryContainer(
            FFXIVClientStructs.FFXIV.Client.Game.InventoryType.EquippedItems);
        if (container == null)
            return null;

        var equipSlotIndex = slot switch
        {
            EquipSlot.MainHand => 0,
            EquipSlot.OffHand => 1,
            EquipSlot.Head => 2,
            EquipSlot.Body => 3,
            EquipSlot.Hands => 4,
            EquipSlot.Legs => 6,
            EquipSlot.Feet => 7,
            EquipSlot.Ears => 8,
            EquipSlot.Neck => 9,
            EquipSlot.Wrists => 10,
            EquipSlot.RFinger => 11,
            EquipSlot.LFinger => 12,
            _ => -1,
        };

        if (equipSlotIndex < 0)
            return null;

        var item = container->GetInventorySlot(equipSlotIndex);
        if (item == null)
            return null;

        return IsRealInventoryEquipment(item->ItemId);
    }

    public unsafe bool? HasEquipmentInSlotForLocalPlayer(EquipSlot slot, bool allowStateQuery = true)
    {
        try
        {
            if (!allowStateQuery)
                return null;

            var inventoryEquipped = TryGetInventoryHasRealEquipment(slot);

            // 方法 1: 优先使用 Glamourer 状态（包含 Glamourer 复写和实际投影）
            var state = GetLocalPlayerState();
            if (state != null)
            {
                var hasEquip = CheckEquipmentInState(state, slot);
                if (hasEquip.HasValue)
                {
                    // 登录投影期间 Glamourer 常报「未装备」，背包已有鞋/衣 → 视为未同步，避免误匹配「全裸」
                    if (inventoryEquipped.HasValue && hasEquip.Value != inventoryEquipped.Value)
                        return null;

                    return hasEquip;
                }
            }
            
            // 方法 2: 如果 Glamourer 不可用，使用 DrawData + InventoryManager 组合检查
            // DrawData 用于检查 Model ID（判断是否为皇帝套）
            // InventoryManager 用于检查是否有装备
            var inventoryManager = FFXIVClientStructs.FFXIV.Client.Game.InventoryManager.Instance();
            if (inventoryManager == null)
                return null;
                
            var container = inventoryManager->GetInventoryContainer(FFXIVClientStructs.FFXIV.Client.Game.InventoryType.EquippedItems);
            if (container == null)
                return null;
            
            var equipSlotIndex = slot switch
            {
                EquipSlot.MainHand => 0,
                EquipSlot.OffHand => 1,
                EquipSlot.Head => 2,
                EquipSlot.Body => 3,
                EquipSlot.Hands => 4,
                EquipSlot.Legs => 6,
                EquipSlot.Feet => 7,
                EquipSlot.Ears => 8,
                EquipSlot.Neck => 9,
                EquipSlot.Wrists => 10,
                EquipSlot.RFinger => 11,
                EquipSlot.LFinger => 12,
                _ => -1
            };
            
            if (equipSlotIndex < 0)
                return null;
            
            var item = container->GetInventorySlot(equipSlotIndex);
            if (item == null)
                return null;
            
            var itemId = item->ItemId;
            
            // 检查 Item ID 是否为皇帝套
            if (EmperorsNewItems.IsEmperorsNewByItemId(itemId))
                return false;
            
            // 如果有装备，进一步检查 DrawData 的 Model ID
            if (itemId > 0)
            {
                // 尝试从 DrawData 获取 Model ID 来检查是否为皇帝套
                var drawSlotIndex = slot switch
                {
                    EquipSlot.Head => 0,
                    EquipSlot.Body => 1,
                    EquipSlot.Hands => 2,
                    EquipSlot.Legs => 3,
                    EquipSlot.Feet => 4,
                    EquipSlot.Ears => 5,
                    EquipSlot.Neck => 6,
                    EquipSlot.Wrists => 7,
                    EquipSlot.RFinger => 8,
                    EquipSlot.LFinger => 9,
                    _ => -1
                };
                
                if (drawSlotIndex >= 0)
                {
                    // 需要获取本地玩家的 Character 指针
                    // 这里暂时无法获取，返回基于 ItemId 的判断
                    // TODO: 传入 localPlayer 参数以访问 DrawData
                }
            }
            
            return itemId > 0;
        }
        catch
        {
            return null;
        }
    }
    
    /// <summary>
    /// 检测指定装备槽位是否有装备
    /// </summary>
    public bool? HasEquipmentInSlot(string characterName, EquipSlot slot)
    {
        var state = GetCurrentState(characterName);
        if (state == null)
            return null;
            
        return CheckEquipmentInState(state, slot);
    }
    
    /// <summary>
    /// 从 Glamourer 状态检查指定装备槽位是否有装备
    /// Glamourer State 包含完整的角色外观，包括：
    /// - Glamourer 复写的部分（Apply = true）
    /// - 实际装备及其投影（Apply = false 时）
    /// 皇帝套（隐形装备）将被视为未装备
    /// </summary>
    private bool? CheckEquipmentInState(JObject state, EquipSlot slot)
    {
        try
        {
            var slotName = slot.ToString();
            var equipToken = state.SelectToken($"Equipment.{slotName}");
            if (equipToken == null)
                return null;
                
            // 检查 Apply 字段 - 表示 Glamourer 是否覆写了该槽位
            var applyToken = equipToken.SelectToken("Apply");
            var apply = applyToken?.ToObject<bool>() ?? false;
            
            // 检查 ItemId
            var itemIdToken = equipToken.SelectToken("ItemId");
            if (itemIdToken == null)
                return null;
                
            var itemId = itemIdToken.ToObject<uint>();
            
            // 检查是否为皇帝套（通过 Item ID）
            if (EmperorsNewItems.IsEmperorsNewByItemId(itemId))
                return false;  // 皇帝套视为未装备
            
            // 尝试检查 Model ID（如果 Glamourer State 包含的话）
            var modelIdToken = equipToken.SelectToken("ModelId");
            if (modelIdToken != null && modelIdToken.Type == JTokenType.Integer)
            {
                var modelId = modelIdToken.ToObject<ushort>();
                if (EmperorsNewItems.IsEmperorsNewByModelId(modelId))
                    return false;  // 皇帝套视为未装备
            }
            
            // 关键修复：
            // 如果 Apply=true，说明 Glamourer 覆写了该槽位
            // - ItemId=0 → Smallclothes（无装备），返回 false
            // - ItemId>0 → 有装备，返回 true
            // 如果 Apply=false，使用实际装备状态（itemId != 0）
            
            // 无论 Apply 状态如何，ItemId=0 都表示无装备
            // Smallclothes 就是 itemId=0 的 Apply=true 状态
            return itemId != 0;
        }
        catch
        {
            return null;
        }
    }
}
