namespace GameICU.Tools;

/// <summary>
/// 虚拟挂机玩家状态（由服务器 BotStatus RPC 驱动）。
/// 有挂机玩家时，过场等待判定会把它计入"永远未到齐"的玩家，
/// 从而真实模拟"有人挂机导致等待卡住"，可用 force_cg 解救。
/// </summary>
public static class GameICUBotState
{
    public static bool HasBot { get; private set; }

    public static string Nickname { get; private set; } = "";

    public static void Set(string payload)
    {
        if (string.IsNullOrEmpty(payload))
        {
            HasBot = false;
            Nickname = "";
            return;
        }

        var parts = payload.Split('|');
        HasBot = true;
        Nickname = parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]) ? parts[1] : "挂机bot";
    }
}
