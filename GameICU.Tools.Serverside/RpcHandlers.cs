using System.Text;
using ReadyM.Api.Multiplayer;
using ReadyM.Api.Multiplayer.ECS.Components;
using ReadyM.Relay.Server.Sdk.Ecs;
using ReadyM.Relay.Server.Sdk.Rpc;
using ReadyM.Wukong.Common.ECS.Components;
using Yooni.Native.Container;
using GameICU.Tools.Common;

namespace GameICU.Tools.Serverside;

[ServerRpcFor(typeof(GameICURpcContracts))]
public partial class RpcHandlers(EcsApi ecs) : ServerRpcHandlersBase
{
    /// <summary>
    /// GameICU 增强：force_cg 命令的服务器侧处理。
    /// 把指定过场在所有区域标记为"已开始"，客户端 Tick 闸门检测到
    /// isMovieStartedByOthers 后会直接播放，从而跳过"等待所有玩家到达"。
    /// </summary>
    partial void OnForceMovieStarted(RpcContext context, int sequenceId)
    {
        ecs.Query<MovieComponent, AreaScopeComponent>((ref movie, ref area) =>
        {
            movie.AddStartedSequences(sequenceId);
        });
    }

    /// <summary>
    /// GameICU 增强：cglist 命令的服务器侧处理。
    /// 聚合所有区域的 MovieComponent（已开始/已完成序列）编码后返回给请求者。
    /// </summary>
    partial void OnQueryMovieStates(RpcContext context)
    {
        var areas = new List<string>();
        ecs.Query<MovieComponent, AreaScopeComponent>((ref MovieComponent movie, ref AreaScopeComponent area) =>
        {
            var sb = new StringBuilder();
            sb.Append(area.AreaId).Append("|s:");
            AppendIds(sb, movie.GetStartedSequences());
            sb.Append("|f:");
            AppendIds(sb, movie.GetFinishedSequences());
            areas.Add(sb.ToString());
        });
        SendMovieStatesResult(context.Sender, string.Join(";", areas));
    }

    private static void AppendIds(StringBuilder sb, NativeList<int>.ReadOnly list)
    {
        var first = true;
        for (var i = 0; i < list.Count; i++)
        {
            var id = list[i];
            if (!first) sb.Append(',');
            sb.Append(id);
            first = false;
        }
    }
}
