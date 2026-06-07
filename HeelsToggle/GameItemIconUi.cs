using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Plugin.Services;
using System.Numerics;

namespace HeelsDesignLinker;

internal static class GameItemIconUi
{
    public const float IconSize = 32f;

    public static float RowHeight =>
        IconSize + ImGui.GetStyle().FramePadding.Y * 2f;

    public static bool TryDraw(ITextureProvider textures, uint iconId, float size = IconSize)
    {
        if (iconId == 0)
            return false;

        try
        {
            var shared = textures.GetFromGameIcon(new GameIconLookup(iconId));
            if (!shared.TryGetWrap(out var wrap, out _) || wrap == null)
                return false;

            ImGui.Image(wrap.Handle, new Vector2(size, size));
            return true;
        }
        catch
        {
            return false;
        }
    }
}
