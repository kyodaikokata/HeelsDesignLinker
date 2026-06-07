namespace HeelsDesignLinker;

internal enum ModelPathKind
{
    Empty,
    Vanilla,
    PenumbraMod,
    Unresolved,
}

internal sealed record ModelPathDisplayInfo(
    ModelPathKind Kind,
    string DisplayText,
    string? TooltipPath,
    PenumbraModEntry? Mod);

/// <summary>
/// Classifies DrawObject model resource paths (vanilla game paths vs Penumbra-resolved filesystem paths).
/// </summary>
internal static class ModelPathDisplayResolver
{
    public static ModelPathDisplayInfo Resolve(string? path, PenumbraInterop penumbra)
    {
        if (string.IsNullOrWhiteSpace(path))
            return new ModelPathDisplayInfo(ModelPathKind.Empty, "-", null, null);

        if (IsVanillaGamePath(path))
        {
            return new ModelPathDisplayInfo(
                ModelPathKind.Vanilla,
                Localization.DebugDrawObjectVanillaPath,
                path,
                null);
        }

        if (IsAbsoluteFilesystemPath(path) && penumbra.TryResolveModFromFilePath(path, out var mod))
        {
            return new ModelPathDisplayInfo(
                ModelPathKind.PenumbraMod,
                mod.DisplayName,
                path,
                mod);
        }

        return new ModelPathDisplayInfo(ModelPathKind.Unresolved, path, path, null);
    }

    internal static bool IsAbsoluteFilesystemPath(string path)
    {
        if (Path.IsPathRooted(path))
            return true;

        return path.Length >= 2 && char.IsLetter(path[0]) && path[1] == ':';
    }

    internal static bool IsVanillaGamePath(string path)
    {
        if (IsAbsoluteFilesystemPath(path))
            return false;

        // 游戏虚拟路径（未解析到磁盘）均以 chara/ 开头：equipment、accessory、human 等
        var normalized = path.Replace('\\', '/').TrimStart('/');
        return normalized.StartsWith("chara/", StringComparison.OrdinalIgnoreCase);
    }
}
