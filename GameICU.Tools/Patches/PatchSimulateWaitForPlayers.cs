using System.Reflection;
using HarmonyLib;
using GameICU.Tools;

namespace GameICU.Tools.Patches;

/// <summary>
/// GameICU 测试辅助：单人模拟"多人等待过场"。
/// 用运行时 Harmony patch 把 SDK 的 CheckAllPlayersWaitingForCutscene 判定
/// 强制返回 false（测试模式下），让单人也会永久卡在"等待其他玩家"界面，
/// 从而可以单独测试 force_cg 命令。
///
/// 注意：不能用 PreludeLib 编译期 [HarmonyPatch] + [HarmonyTargetMethodHint]
/// 方式——目标方法在另一个 mod 程序集（WukongMp.Api）里，编译期 weaver 跨
/// mod 程序集解析方法体时会抛 ObjectDisposedException，导致整个 mod 加载
/// 失败（init_managed_mod_loader failed，游戏退回纯本地存档）。
/// 运行时 harmony.Patch 没有这个问题。
/// </summary>
public static class PatchSimulateWaitForPlayers
{
    /// <summary>测试模式开关（test_wait 命令控制）。</summary>
    public static bool SimulateEnabled;

    /// <summary>
    /// 运行时挂载 Postfix。在 Mod 初始化时调用；失败只记日志不影响 mod 加载。
    /// </summary>
    public static void Apply(Harmony harmony)
    {
        try
        {
            var type = AccessTools.TypeByName("WukongMp.Api.WukongUtils.CutsceneUtils");
            if (type == null)
                return;
            var target = AccessTools.Method(type, "CheckAllPlayersWaitingForCutscene");
            if (target == null)
                return;
            harmony.Patch(target, postfix: new HarmonyMethod(typeof(PatchSimulateWaitForPlayers).GetMethod(nameof(Postfix), BindingFlags.Public | BindingFlags.Static)));
        }
        catch
        {
            // 不抛出：patch 失败不应影响 mod 其余功能
        }
    }

    public static void Postfix(ref bool __result)
    {
        // 测试模式（test_wait）或存在虚拟挂机玩家（addbot）时，
        // 一律判定"还有玩家未到齐" → 过场等待界面卡住，force_cg 可放行。
        if (SimulateEnabled || GameICUBotState.HasBot)
            __result = false;
    }
}
