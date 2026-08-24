using ReadyM.Api.Multiplayer;

namespace GameICU.Tools.Common;

[ServerRpcContracts]
public static partial class GameICURpcContracts
{
    /// <summary>
    /// GameICU 增强：客户端请求服务器把指定过场标记为"已开始"，
    /// 从而让所有客户端解除"等待其他玩家到达"的闸门，直接进入该过场。
    /// 服务器侧在 RpcHandlers.OnForceMovieStarted 中把该序列加入所有区域的
    /// MovieComponent.AddStartedSequences，客户端 Tick 闸门检测到
    /// isMovieStartedByOthers 后自动放行播放。
    /// </summary>
    [ClientToServer] public static partial void ForceMovieStarted(int sequenceId);

    /// <summary>
    /// GameICU 增强：客户端请求查询服务器上所有区域的过场状态（已开始/已完成）。
    /// 服务器聚合所有区域的 MovieComponent 后通过 MovieStatesResult 返回。
    /// </summary>
    [ClientToServer] public static partial void QueryMovieStates();

    /// <summary>
    /// GameICU 增强：服务器返回所有区域的过场状态。
    /// payload 格式：多个区域用 ';' 分隔，每个区域为 "areaId|s:startedIds|f:finishedIds"。
    /// </summary>
    [ServerToClient] public static partial void MovieStatesResult(string payload);
}
