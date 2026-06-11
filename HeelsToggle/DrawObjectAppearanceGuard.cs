using Dalamud.Game.ClientState.Objects.SubKinds;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace HeelsDesignLinker;

/// <summary>
/// 检测本地玩家是否处于游戏内置变身（TransformationId），避免插件在变身期间 apply 外观行动。
/// DrawObject 模型类型改由 <see cref="DrawObjectAppearanceBaseline"/> 与冗余 apply 跳过逻辑处理。
/// </summary>
internal static class DrawObjectAppearanceGuard
{
    internal readonly struct AppearanceState
    {
        public bool IsTransformActive { get; init; }
        public short TransformationId { get; init; }
        public string? BlockReason { get; init; }
    }

    public static AppearanceState Inspect(IPlayerCharacter? localPlayer)
    {
        if (localPlayer == null || !localPlayer.IsValid())
            return new AppearanceState();

        unsafe
        {
            var character = (Character*)localPlayer.Address;
            if (character == null)
                return new AppearanceState();

            var transformationId = character->TransformationId;
            if (transformationId == 0)
                return new AppearanceState { TransformationId = transformationId };

            return new AppearanceState
            {
                IsTransformActive = true,
                TransformationId = transformationId,
                BlockReason = Localization.AppearanceTransformActive(transformationId),
            };
        }
    }

    public static bool IsAppearanceTransformActive(IPlayerCharacter? localPlayer, out string status)
    {
        var state = Inspect(localPlayer);
        status = state.BlockReason ?? "";
        return state.IsTransformActive;
    }
}
