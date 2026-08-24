using b1;
using ReadyM.Api.Idents;
using ReadyM.Api.Multiplayer.Generators;
using ReadyM.Api.Multiplayer.Protocol.Enums;
using ReadyM.Api.Multiplayer.RPC;
using UnrealEngine.Runtime;
using WukongMp.Api;
using WukongMp.Api.WukongUtils;

namespace GameICU.Tools;

/// <summary>
/// GameICU 增强：玩家间（client-to-client）事件。
/// 目前仅包含 force_cg / tpall 的全员传送事件（GlobalAll 保证跨区域的所有玩家
/// 含发送者都能收到）。
/// </summary>
public partial class GameICURpcEvents : ClientRpcHandler
{
    /// <summary>
    /// force_cg / tpall 的传送事件：把本地玩家传送到指定位置（房主位置）。
    /// </summary>
    [RpcEvent(RelayMode.GlobalAll)]
    private void OnForceTeleportToHost(PlayerId __sender, float x, float y, float z, float yaw)
    {
        RunOnGameThread(() => TeleportLocalPlayerTo(x, y, z, yaw));
    }

    private static void TeleportLocalPlayerTo(float x, float y, float z, float yaw)
    {
        var pawn = GameUtils.GetControlledPawn();
        if (pawn == null)
            return;

        var location = new FVector(x, y, z);
        var rotation = pawn.GetActorRotation();
        rotation.Yaw = yaw;

        // 与 SDK PlayerUtils.TeleportLocalPlayer 相同的传送路径（公开 API 复刻）
        BUS_EventCollectionCS.Get(pawn)?.Evt_UnitStateTrigger.Invoke(EBUStateTrigger.TeleportBegin, -1f);
        pawn.SetActorTransform(new FTransform(rotation, location), false, out _, true);
        BUS_EventCollectionCS.Get(pawn)?.Evt_ResetCameraSpringArmRot.Invoke();

        Logging.LogDebug("[GameICU] Force-teleported local player to ({X}, {Y}, {Z})", x, y, z);
    }
}
