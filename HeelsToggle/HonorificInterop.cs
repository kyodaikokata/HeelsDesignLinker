using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HeelsToggle;

internal sealed class HonorificTitleOption
{
    public string Display { get; init; } = "";
    public string Json { get; init; } = "";
}

/// <summary>
/// Honorific IPC：设置本地玩家自定义称号。
/// </summary>
internal sealed class HonorificInterop
{
    private const string ApiVersionGate = "Honorific.ApiVersion";

    private readonly IDalamudPluginInterface _pluginInterface;
    private List<HonorificTitleOption> _titleOptions = [];
    private DateTime _titleListFetchedUtc = DateTime.MinValue;
    private static readonly TimeSpan TitleListRefreshInterval = TimeSpan.FromSeconds(30);

    public HonorificInterop(IDalamudPluginInterface pluginInterface)
    {
        _pluginInterface = pluginInterface;
    }

    public static bool IsHonorificLoaded(IDalamudPluginInterface pluginInterface)
    {
        return pluginInterface.InstalledPlugins.Any(p =>
            p.InternalName == "Honorific" && p.IsLoaded);
    }

    public bool IsIpcAvailable()
    {
        if (!IsHonorificLoaded(_pluginInterface))
            return false;

        try
        {
            _ = _pluginInterface.GetIpcSubscriber<(uint, uint)>(ApiVersionGate).InvokeFunc();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void RefreshTitleList(IPlayerCharacter? player, bool force = false)
    {
        _ = GetTitleOptions(player, force);
    }

    public IReadOnlyList<HonorificTitleOption> GetTitleOptions(IPlayerCharacter? player, bool force = false)
    {
        if (!force
            && _titleOptions.Count > 0
            && (DateTime.UtcNow - _titleListFetchedUtc) < TitleListRefreshInterval)
        {
            return _titleOptions;
        }

        _titleOptions = [];
        if (!IsIpcAvailable() || player == null)
            return _titleOptions;

        try
        {
            var subscriber = _pluginInterface.GetIpcSubscriber<string, uint, object>(
                "Honorific.GetCharacterTitleList");
            var raw = subscriber.InvokeFunc(player.Name.TextValue, (uint)player.HomeWorld.RowId);
            _titleOptions = ParseTitleList(raw);
            _titleListFetchedUtc = DateTime.UtcNow;
        }
        catch
        {
            _titleOptions = [];
        }

        return _titleOptions;
    }

    public bool TrySetLocalTitle(IPlayerCharacter player, string titleJson, out string error)
    {
        error = "";
        if (!IsIpcAvailable())
        {
            error = "Honorific IPC unavailable";
            return false;
        }

        if (string.IsNullOrWhiteSpace(titleJson))
        {
            error = "Honorific title empty";
            return false;
        }

        try
        {
            var subscriber = _pluginInterface.GetIpcSubscriber<uint, string, object>(
                "Honorific.SetCharacterTitle");
            subscriber.InvokeAction(player.ObjectIndex, titleJson);
            return true;
        }
        catch (Exception ex)
        {
            error = $"Honorific IPC: {ex.Message}";
            return false;
        }
    }

    public bool TryClearLocalTitle(IPlayerCharacter player, out string error)
    {
        error = "";
        if (!IsIpcAvailable())
        {
            error = "Honorific IPC unavailable";
            return false;
        }

        try
        {
            var subscriber = _pluginInterface.GetIpcSubscriber<uint, object>(
                "Honorific.ClearCharacterTitle");
            subscriber.InvokeAction(player.ObjectIndex);
            return true;
        }
        catch (Exception ex)
        {
            error = $"Honorific IPC: {ex.Message}";
            return false;
        }
    }

    private static List<HonorificTitleOption> ParseTitleList(object? raw)
    {
        var result = new List<HonorificTitleOption>();
        if (raw == null)
            return result;

        try
        {
            var token = raw is string s ? JToken.Parse(s) : JToken.FromObject(raw);
            if (token is not JArray array)
                return result;

            foreach (var item in array)
            {
                if (item is not JObject obj)
                    continue;

                var title = obj["Title"]?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(title))
                    continue;

                result.Add(new HonorificTitleOption
                {
                    Display = title,
                    Json = obj.ToString(Formatting.None),
                });
            }
        }
        catch
        {
            return [];
        }

        return result
            .GroupBy(x => x.Json, StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderBy(x => x.Display, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
