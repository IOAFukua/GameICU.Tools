using System.Collections;
using b1;
using ReadyM.Api.Command;
using UnrealEngine.Runtime;
using WukongMp.Api;
using WukongMp.Api.WukongUtils;
using GameICU.Tools.Patches;
using WukongMp.Sdk.Api;

namespace GameICU.Tools.Commands;

public static class GameICUCommands
{
    public static void RegisterCommands(IWukongConsoleApi consoleApi)
    {
        // 注意：ConsoleCommand.Create 第二个参数是 isDebugOnly，正式版（非 DEBUG 构建）中
        // debug 命令会被静默拒绝执行（WukongCommandConsole.TryExecuteCommand 直接 return false），
        // 所以所有命令必须传 false 才能生效。
        consoleApi.AddCommand("force_cg", ConsoleCommand.Create(ForceCutscene, false));
        consoleApi.AddCommand("test_wait", ConsoleCommand.Create(TestWait, false));
        consoleApi.AddCommand("tpall", ConsoleCommand.Create(TeleportAllToHost, false));
        consoleApi.AddCommand("cglist", ConsoleCommand.Create(CgList, false));
        consoleApi.AddCommand("addbot", ConsoleCommand.Create(AddBot, false));
        consoleApi.AddCommand("removebot", ConsoleCommand.Create(RemoveBot, false));
    }

    /// <summary>
    /// GameICU 增强：强制跳过"等待其他玩家到达"阶段，直接播放当前待播放的过场，
    /// 并把所有玩家传送到执行者（房主）位置。
    /// 用法：
    ///   force_cg            → 自动取本地待播放的过场 ID
    ///   force_cg 80005020   → 指定过场 ID
    /// </summary>
    private static void ForceCutscene(int sequenceId = 0)
    {
        var pawn = GameUtils.GetControlledPawn();
        Logging.LogDebug("[GameICU] force_cg invoked, arg sequenceId={Id}", sequenceId);

        // 没有显式指定 ID 时，取本地待播放队列顶部的过场
        if (sequenceId <= 0)
        {
            var movieData = BGWGameInstanceCS.GetObject<BGW_GameDataMgr>(pawn)?.GetGameInstanceWritableData<BIC_MovieData>();
            var queue = movieData?.PlayMovieRequestQueue;
            if (queue != null && queue.Count > 0)
            {
                sequenceId = queue.Peek().SequenceID;
                Logging.LogDebug("[GameICU] force_cg picked queued sequence {Id}", sequenceId);
            }
            else
            {
                Logging.LogDebug("[GameICU] force_cg: play queue is empty");
            }
        }

        if (sequenceId <= 0)
        {
            Logging.LogDebug("[GameICU] force_cg: no sequence available, showing hint");
            var hint = "[force_cg] 没有待播放的过场动画，请先 test_wait <ID> 或指定 force_cg <ID>";
            WukongApi.Chat.ShowLocalMessage(hint, FLinearColor.OrangeRed);
            WukongApi.Local.ShowInfoMessage(hint, 5f);
            return;
        }

        // 1. 通知服务器把该过场标记为"已开始" → 所有客户端的等待闸门自动打开并播放
        Logging.LogDebug("[GameICU] force_cg: sending ForceMovieStarted for {Id}", sequenceId);
        WukongApi.Services.Resolve<GameICUServerRpc>()?.SendForceMovieStarted(sequenceId);

        // 2. 把所有玩家传送到执行者（房主）位置
        if (pawn != null)
        {
            var pos = pawn.GetActorLocation();
            var rot = pawn.GetActorRotation();
            Logging.LogDebug("[GameICU] force_cg: sending ForceTeleportToHost to ({X}, {Y}, {Z})", pos.X, pos.Y, pos.Z);
            WukongApi.Services.Resolve<GameICURpcEvents>()?.SendForceTeleportToHost(pos.X, pos.Y, pos.Z, rot.Yaw);
        }

        WukongApi.Local.HideInfoMessage();
        var done = $"[force_cg] 已强制播放过场 {sequenceId}，并把所有玩家传送到房主位置";
        WukongApi.Chat.ShowLocalMessage(done, FLinearColor.Green);
        WukongApi.Local.ShowInfoMessage(done, 5f);
        Logging.LogDebug("[GameICU] force_cg completed for {Id}", sequenceId);
    }

    /// <summary>
    /// GameICU 测试辅助：开启/关闭单人模拟多人等待。
    /// 用法：
    ///   test_wait <过场ID>  → 把指定过场塞入播放队列并开启模拟（单人也会卡在等待界面）
    ///   test_wait           → 仅开启模拟（已走到真实过场触发点时用）
    ///   test_wait -1        → 关闭模拟
    /// 开启后执行 force_cg 即可放行，与真实多人行为一致。
    /// </summary>
    private static void TestWait(int id = 0)
    {
        if (id == -1)
        {
            PatchSimulateWaitForPlayers.SimulateEnabled = false;
            WukongApi.Chat.ShowLocalMessage("[test_wait] 已关闭多人等待模拟", FLinearColor.Green);
            return;
        }

        var pawn = GameUtils.GetControlledPawn();

        if (id > 0)
        {
            var movieData = BGWGameInstanceCS.GetObject<BGW_GameDataMgr>(pawn)?.GetGameInstanceWritableData<BIC_MovieData>();
            if (movieData == null)
            {
                WukongApi.Chat.ShowLocalMessage("[test_wait] 获取电影数据失败", FLinearColor.OrangeRed);
                return;
            }

            movieData.PlayMovieRequestQueue.Clear();
            movieData.PlayMovieRequestQueue.Enqueue(new FPlayMovieRequest
            {
                SequenceID = id,
                bDisablePlayerControl = true,
            });
            Logging.LogDebug("[GameICU] TestWait enqueued sequence {Id} for waiting simulation", id);
        }
        else
        {
            var queue = BGWGameInstanceCS.GetObject<BGW_GameDataMgr>(pawn)?.GetGameInstanceWritableData<BIC_MovieData>()?.PlayMovieRequestQueue;
            if (queue == null || queue.Count == 0)
            {
                WukongApi.Chat.ShowLocalMessage("[test_wait] 当前没有排队中的过场，请先走到过场触发点，或直接指定：test_wait <过场ID>", FLinearColor.OrangeRed);
                return;
            }
        }

        PatchSimulateWaitForPlayers.SimulateEnabled = true;
        WukongApi.Chat.ShowLocalMessage("[test_wait] 多人等待模拟已开启：单人也会卡在等待界面，执行 force_cg 放行", FLinearColor.Green);
    }

    /// <summary>
    /// GameICU 增强：全员传送到执行者（房主）位置。
    /// 与 force_cg 内部的传送逻辑相同（GlobalAll C2C 事件）。
    /// 用法：tpall
    /// </summary>
    private static void TeleportAllToHost()
    {
        var pawn = GameUtils.GetControlledPawn();
        if (pawn == null)
        {
            var hint = "[tpall] 找不到本地角色";
            WukongApi.Chat.ShowLocalMessage(hint, FLinearColor.OrangeRed);
            WukongApi.Local.ShowInfoMessage(hint, 5f);
            return;
        }

        var pos = pawn.GetActorLocation();
        var rot = pawn.GetActorRotation();
        Logging.LogDebug("[GameICU] tpall: sending ForceTeleportToHost to ({X}, {Y}, {Z})", pos.X, pos.Y, pos.Z);
        WukongApi.Services.Resolve<GameICURpcEvents>()?.SendForceTeleportToHost(pos.X, pos.Y, pos.Z, rot.Yaw);

        var done = "[tpall] 已把所有玩家传送到房主位置";
        WukongApi.Chat.ShowLocalMessage(done, FLinearColor.Green);
        WukongApi.Local.ShowInfoMessage(done, 5f);
        Logging.LogDebug("[GameICU] tpall completed");
    }

    /// <summary>
    /// GameICU 增强：查询全员等待过场 ID。
    /// 用法：
    ///   cglist               → 所有已知的全员等待过场 ID
    ///   cglist area          → 当前区域所有
    ///   cglist done          → 所有已经历（本会话已开始/已完成）
    ///   cglist undone        → 所有未经历
    ///   cglist area done     → 当前区域已经历
    ///   cglist area undone   → 当前区域未经历
    /// 数据来源：服务器聚合所有区域的 MovieComponent + 本地播放队列/历史（运行时反射探测）。
    /// </summary>
    private static void CgList(string scope = "", string filter = "")
    {
        var isArea = false;
        var isDone = false;
        var isUndone = false;
        foreach (var arg in new[] { scope, filter })
        {
            switch (arg)
            {
                case "area": isArea = true; break;
                case "done": isDone = true; break;
                case "undone": isUndone = true; break;
                case "" when isArea: break;
                case "": break;
                default:
                    WukongApi.Chat.ShowLocalMessage($"[cglist] 未知参数 '{arg}'，用法：cglist [area] [done|undone]", FLinearColor.OrangeRed);
                    return;
            }
        }

        if (isDone && isUndone)
        {
            WukongApi.Chat.ShowLocalMessage("[cglist] done 与 undone 不能同时指定", FLinearColor.OrangeRed);
            return;
        }

        Logging.LogDebug("[GameICU] cglist requested: area={Area}, done={Done}, undone={Undone}", isArea, isDone, isUndone);
        CgListCommands.PendingQuery = new CgListQuery(isArea, isDone, isUndone);
        WukongApi.Services.Resolve<GameICUServerRpc>()?.SendQueryMovieStates();
    }

    /// <summary>
    /// GameICU 增强：添加一个虚拟挂机玩家（addbot 命令）。
    /// 服务器登记后广播给所有人，过场等待会把该挂机玩家算入"未到齐"，
    /// 模拟正常游玩中有人挂机的场景；force_cg 可照常解救。
    /// 用法：addbot [昵称]
    /// </summary>
    private static void AddBot(string nickname = "")
    {
        WukongApi.Services.Resolve<GameICUServerRpc>()?.SendAddBot(nickname);
        WukongApi.Chat.ShowLocalMessage("[addbot] 已请求添加挂机玩家", FLinearColor.Green);
    }

    /// <summary>
    /// GameICU 增强：移除虚拟挂机玩家（removebot 命令）。
    /// 用法：removebot
    /// </summary>
    private static void RemoveBot()
    {
        WukongApi.Services.Resolve<GameICUServerRpc>()?.SendRemoveBot();
        WukongApi.Chat.ShowLocalMessage("[removebot] 已请求移除挂机玩家", FLinearColor.Green);
    }
}
