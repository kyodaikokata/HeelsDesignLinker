using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin;
using Newtonsoft.Json.Linq;

namespace HeelsDesignLinker;

internal sealed class MoodleListItem
{
    public Guid Id { get; init; }
    public string Title { get; init; } = "";
    public string Path { get; init; } = "";
    public bool IsPreset { get; init; }
}

/// <summary>
/// Moodles IPC：应用自定义状态或预设档案。
/// </summary>
internal sealed class MoodlesInterop
{
    private readonly IDalamudPluginInterface _pluginInterface;
    private List<MoodleListItem> _items = [];
    private DateTime _listFetchedUtc = DateTime.MinValue;
    private static readonly TimeSpan ListRefreshInterval = TimeSpan.FromSeconds(30);

    public MoodlesInterop(IDalamudPluginInterface pluginInterface)
    {
        _pluginInterface = pluginInterface;
    }

    public static bool IsMoodlesLoaded(IDalamudPluginInterface pluginInterface)
    {
        return pluginInterface.InstalledPlugins.Any(p =>
            p.InternalName == "Moodles" && p.IsLoaded);
    }

    public bool IsIpcAvailable()
    {
        if (!IsMoodlesLoaded(_pluginInterface))
            return false;

        try
        {
            _ = _pluginInterface.GetIpcSubscriber<int>("Moodles.Version").InvokeFunc();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void RefreshList(bool force = false)
    {
        _ = GetItems(force);
    }

    public IReadOnlyList<MoodleListItem> GetItems(bool force = false)
    {
        if (!force
            && _items.Count > 0
            && (DateTime.UtcNow - _listFetchedUtc) < ListRefreshInterval)
        {
            return _items;
        }

        _items = [];
        if (!IsIpcAvailable())
            return _items;

        try
        {
            foreach (var item in FetchMoodles())
                _items.Add(item);
            foreach (var item in FetchPresets())
                _items.Add(item);

            _items = _items
                .OrderBy(x => x.IsPreset)
                .ThenBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();
            _listFetchedUtc = DateTime.UtcNow;
        }
        catch
        {
            _items = [];
        }

        return _items;
    }

    public bool TryApply(IPlayerCharacter player, Guid id, bool isPreset, out string error)
    {
        error = "";
        if (!IsIpcAvailable())
        {
            error = "Moodles IPC unavailable";
            return false;
        }

        if (id == Guid.Empty)
        {
            error = "Moodle / preset not selected";
            return false;
        }

        try
        {
            if (isPreset)
            {
                var subscriber = _pluginInterface.GetIpcSubscriber<Guid, IPlayerCharacter, object>(
                    "Moodles.ApplyPresetByPlayerV2");
                subscriber.InvokeAction(id, player);
            }
            else
            {
                var subscriber = _pluginInterface.GetIpcSubscriber<Guid, IPlayerCharacter, object>(
                    "Moodles.AddOrUpdateMoodleByPlayerV2");
                subscriber.InvokeAction(id, player);
            }

            return true;
        }
        catch (Exception ex)
        {
            error = $"Moodles IPC: {ex.Message}";
            return false;
        }
    }

    private IEnumerable<MoodleListItem> FetchMoodles()
    {
        object? raw;
        try
        {
            raw = _pluginInterface.GetIpcSubscriber<object>("Moodles.GetRegisteredMoodlesV2").InvokeFunc();
        }
        catch
        {
            yield break;
        }

        foreach (var entry in ParseTupleList(raw, minLength: 4))
        {
            if (!Guid.TryParse(entry[0], out var id) || id == Guid.Empty)
                continue;

            var title = entry.Count > 3 ? entry[3] : entry[0];
            var path = entry.Count > 2 ? entry[2] : "";
            yield return new MoodleListItem
            {
                Id = id,
                Title = string.IsNullOrWhiteSpace(title) ? path : title,
                Path = path,
                IsPreset = false,
            };
        }
    }

    private IEnumerable<MoodleListItem> FetchPresets()
    {
        object? raw;
        try
        {
            raw = _pluginInterface.GetIpcSubscriber<object>("Moodles.GetRegisteredProfilesV2").InvokeFunc();
        }
        catch
        {
            yield break;
        }

        foreach (var entry in ParseTupleList(raw, minLength: 2))
        {
            if (!Guid.TryParse(entry[0], out var id) || id == Guid.Empty)
                continue;

            var path = entry.Count > 1 ? entry[1] : id.ToString();
            yield return new MoodleListItem
            {
                Id = id,
                Title = System.IO.Path.GetFileName(path.TrimEnd('\\', '/')),
                Path = path,
                IsPreset = true,
            };
        }
    }

    private static IEnumerable<IReadOnlyList<string>> ParseTupleList(object? raw, int minLength)
    {
        if (raw == null)
            yield break;

        JArray? array;
        try
        {
            array = raw is string s ? JArray.Parse(s) : JArray.FromObject(raw);
        }
        catch
        {
            yield break;
        }

        foreach (var token in array)
        {
            if (token is JArray inner)
            {
                var parts = inner.Select(t => t.ToString()).ToList();
                if (parts.Count >= minLength)
                    yield return parts;
                continue;
            }

            if (token is JObject obj)
            {
                var parts = obj.Properties().Select(p => p.Value?.ToString() ?? "").ToList();
                if (parts.Count >= minLength)
                    yield return parts;
            }
        }
    }
}
