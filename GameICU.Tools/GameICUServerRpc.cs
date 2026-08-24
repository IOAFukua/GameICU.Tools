using ReadyM.Api.Multiplayer;
using ReadyM.Api.Multiplayer.RPC;
using WukongMp.Sdk.Api;
using GameICU.Tools.Common;
using GameICU.Tools.Commands;

namespace GameICU.Tools;

/// <summary>
/// 客户端 → 服务器 RPC（契约在 GameICU.Tools.Common.GameICURpcContracts）。
/// 生成器会根据 [ClientToServer] 契约生成 SendForceMovieStarted / SendQueryMovieStates，
/// 并根据 [ServerToClient] 契约生成 OnMovieStatesResult 接收回调。
/// </summary>
[ServerRpcFor(typeof(GameICURpcContracts))]
public partial class GameICUServerRpc : ServerRpcClient
{
    partial void OnMovieStatesResult(string payload)
    {
        RunOnGameThread(() => CgListCommands.HandleMovieStatesResult(payload));
    }

    partial void OnBotStatus(string payload)
    {
        RunOnGameThread(() =>
        {
            GameICUBotState.Set(payload);
            if (GameICUBotState.HasBot)
                WukongApi.Chat.ShowLocalMessage($"[GameICU] 挂机玩家「{GameICUBotState.Nickname}」已上线（过场等待会把它算入未到齐）", UnrealEngine.Runtime.FLinearColor.Green);
            else
                WukongApi.Chat.ShowLocalMessage("[GameICU] 挂机玩家已移除，过场等待恢复正常", UnrealEngine.Runtime.FLinearColor.Green);
        });
    }
}
