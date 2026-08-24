using ReadyM.Api.Multiplayer;
using ReadyM.Api.Multiplayer.RPC;
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
}
