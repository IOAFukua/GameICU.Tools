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

    /// <summary>
    /// GameICU 增强：请求服务器登记一个"虚拟挂机玩家"（addbot 命令）。
    /// 登记后所有客户端的过场等待判定都会把该挂机玩家计入（永远未到齐），
    /// 从而真实模拟"有人挂机导致等待卡住"，可用 force_cg 解救。
    /// </summary>
    [ClientToServer] public static partial void AddBot(string nickname);

    /// <summary>
    /// GameICU 增强：移除虚拟挂机玩家（removebot 命令）。
    /// </summary>
    [ClientToServer] public static partial void RemoveBot();

    /// <summary>
    /// GameICU 增强：服务器广播挂机玩家状态。
    /// payload 格式："1|昵称"（有挂机玩家）或空字符串（无）。
    /// </summary>
    [ServerToClient] public static partial void BotStatus(string payload);
}
