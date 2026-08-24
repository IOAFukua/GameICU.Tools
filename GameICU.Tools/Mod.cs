using HarmonyLib;
using Microsoft.Extensions.Logging;
using ReadyM.Api.DI;
using GameICU.Tools.Commands;
using GameICU.Tools.Patches;
using WukongMp.Api;
using WukongMp.Sdk;
using WukongMp.Sdk.Api;

namespace GameICU.Tools;

/// <summary>
/// GameICU.Tools：独立第三方增强 mod（官方 WukongMp.Coop 保持原版不动）。
/// 提供：
///   - force_cg 控制台命令：强制跳过"等待其他玩家"直接播放过场 + 全员传送到房主
///   - test_wait 控制台命令：单人模拟多人等待（测试用）
///   - tpall 控制台命令：全员传送到房主位置
/// </summary>
public sealed class Mod : ModBase
{
    public override string Name => "GameICU.Tools";

    protected override void Initialize(IDependencyContainer services)
    {
        // RPC 类必须注册进 DI 容器：SDK 在 LateInit 时扫描容器中所有
        // ClientRpcHandler 子类分配 C2C 事件码（SetUpClientRpcOffsets），
        // 不注册则功能静默失效。
        services.RegisterSingleton<GameICURpcEvents>();
        services.RegisterSingleton<GameICUServerRpc>();

        Logger.LogInformation("Initializing {ModName}", Name);

        GameICUCommands.RegisterCommands(WukongApi.Console);

        // 运行时 Harmony 补丁：不能用 PreludeLib 编译期 [HarmonyPatch]——
        // PatchSimulateWaitForPlayers 的目标在另一个 mod 程序集（WukongMp.Api）里，
        // 编译期 weaver 跨 mod 程序集解析会抛 ObjectDisposedException 导致整个 mod 加载失败。
        try
        {
            var harmony = new Harmony("gameicu.tools");
            PatchSimulateWaitForPlayers.Apply(harmony);
            Logger.LogInformation("Applied GameICU runtime harmony patches");
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to apply GameICU runtime harmony patches");
        }

        Logger.LogInformation("Initialized {PluginName}", Name);
    }
}
