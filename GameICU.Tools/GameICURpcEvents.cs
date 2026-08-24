using b1;
using ReadyM.Api.Idents;
using ReadyM.Api.Multiplayer.Generators;
using ReadyM.Api.Multiplayer.Protocol.Enums;
using ReadyM.Api.Multiplayer.RPC;
using UnrealEngine.Runtime;
using WukongMp.Api;
using WukongMp.Api.WukongUtils;
using WukongMp.Sdk.Api;

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
        var local = WukongApi.Sync.LocalMainCharacter;
        if (!local.HasValue)
        {
            Logging.LogDebug("[GameICU] Teleport skipped: no local main character");
            return;
        }

        // 官方公开传送 API：内部走 PlayerUtils.TeleportLocalPlayer 完整路径
        // （GetCorrectedSpawnLocation 落点修正 + TeleportFinishFrames 传送收尾 + 相机弹簧臂重置），
        // 避免手写 SetActorTransform 导致传送后移动/跳跃视角异常。
        local.Value.Teleport(
            new System.Numerics.Vector3(x, y, z),
            new System.Numerics.Vector3(0f, yaw, 0f)); // Euler (Pitch=0, Yaw, Roll=0)

        Logging.LogDebug("[GameICU] Teleported local player to ({X}, {Y}, {Z}) yaw={Yaw}", x, y, z, yaw);
    }
}
