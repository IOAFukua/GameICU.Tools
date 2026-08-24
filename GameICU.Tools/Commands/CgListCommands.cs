using System.Collections;
using System.Reflection;
using b1;
using UnrealEngine.Engine;
using UnrealEngine.Runtime;
using WukongMp.Api;
using WukongMp.Api.WukongUtils;
using WukongMp.Sdk.Api;

namespace GameICU.Tools.Commands;

/// <summary>cglist 查询参数。</summary>
public sealed class CgListQuery
{
    public CgListQuery(bool isArea, bool isDone, bool isUndone)
    {
        IsArea = isArea;
        IsDone = isDone;
        IsUndone = isUndone;
    }

    public bool IsArea { get; }
    public bool IsDone { get; }
    public bool IsUndone { get; }
}

/// <summary>
/// cglist 命令的结果处理：
/// 1) 服务器聚合所有区域的 MovieComponent（已开始/已完成序列）；
/// 2) 本地反射探测 BIC_MovieData 的播放历史与可能的全集；
/// 3) 本地播放队列与当前播放中的过场；
/// 4) 组合输出。
/// </summary>
public static class CgListCommands
{
    /// <summary>待处理的查询（命令发出后、服务器返回前）。</summary>
    public static CgListQuery? PendingQuery;

    // 服务器返回的最新数据
    private static readonly List<int> GlobalStarted = new();
    private static readonly List<int> GlobalFinished = new();
    private static readonly Dictionary<string, (List<int> Started, List<int> Finished)> ByArea = new();
    private static string? _currentAreaId;

    public static void HandleMovieStatesResult(string payload)
    {
        var query = PendingQuery;
        PendingQuery = null;
        if (query == null)
            return;

        ParsePayload(payload);

        var pawn = GameUtils.GetControlledPawn();
        if (pawn == null)
        {
            WukongApi.Chat.ShowLocalMessage("[cglist] 找不到本地角色", FLinearColor.OrangeRed);
            return;
        }

        // 本地数据
        var queueIds = GetQueueIds(pawn);
        var historyIds = ReflectIntList(pawn, "BIC_MovieData", new[] { "played", "finished", "history", "record" });
        var allIds = ReflectIntList(pawn, "BIC_MovieData", new[] { "all", "sequence", "table", "list" });
        var playingId = GetCurrentPlayingId(pawn);

        // 全集：优先反射到的配置表；否则用本会话已观测集合
        var observed = new HashSet<int>();
        foreach (var id in GlobalStarted) observed.Add(id);
        foreach (var id in GlobalFinished) observed.Add(id);
        foreach (var id in queueIds) observed.Add(id);
        foreach (var id in historyIds) observed.Add(id);
        if (playingId > 0) observed.Add(playingId);

        var all = allIds != null && allIds.Count > 0 ? new HashSet<int>(allIds) : new HashSet<int>(observed);
        var noTable = allIds == null || allIds.Count == 0;

        // 已经历 = 本地历史 ∪ 服务器已完成 ∪ 服务器已开始
        var doneSet = new HashSet<int>();
        foreach (var id in historyIds) doneSet.Add(id);
        foreach (var id in GlobalFinished) doneSet.Add(id);
        foreach (var id in GlobalStarted) doneSet.Add(id);

        // 当前区域（服务器返回的 areaId 与 CurrentAreaId 匹配）
        var areaStarted = new List<int>();
        var areaFinished = new List<int>();
        if (_currentAreaId != null && ByArea.TryGetValue(_currentAreaId, out var areaData))
        {
            areaStarted = areaData.Started;
            areaFinished = areaData.Finished;
        }

        var areaDoneSet = new HashSet<int>(doneSet);
        foreach (var id in areaStarted) areaDoneSet.Add(id);
        foreach (var id in areaFinished) areaDoneSet.Add(id);

        // ---- 输出 ----
        var note = noTable ? "（未找到完整配置表，全集=本会话已观测集合）" : "";
        WukongApi.Chat.ShowLocalMessage($"[cglist] 当前区域：{(_currentAreaId?.ToString() ?? "未知")}", FLinearColor.Gray);

        if (!query.IsArea && !query.IsDone && !query.IsUndone)
        {
            ShowList("所有已知全员等待ID", all, note);
        }
        else if (query.IsArea && !query.IsDone && !query.IsUndone)
        {
            var areaAll = new HashSet<int>(areaStarted);
            foreach (var id in areaFinished) areaAll.Add(id);
            foreach (var id in queueIds) areaAll.Add(id);
            ShowList("当前区域全员等待ID", areaAll, "（区域内已发生/进行中/待播放）");
        }
        else if (!query.IsArea && query.IsDone)
        {
            ShowList("所有已经历ID", doneSet, "");
        }
        else if (!query.IsArea && query.IsUndone)
        {
            var undone = all.Where(id => !doneSet.Contains(id)).ToList();
            ShowList("所有未经历ID", undone, note);
        }
        else if (query.IsArea && query.IsDone)
        {
            ShowList("当前区域已经历ID", areaDoneSet, "");
        }
        else if (query.IsArea && query.IsUndone)
        {
            var undone = all.Where(id => !areaDoneSet.Contains(id)).ToList();
            ShowList("当前区域未经历ID", undone, note);
        }
    }

    private static void ParsePayload(string payload)
    {
        GlobalStarted.Clear();
        GlobalFinished.Clear();
        ByArea.Clear();
        _currentAreaId = WukongApi.Sync.CurrentAreaId?.ToString();

        if (string.IsNullOrEmpty(payload))
            return;

        foreach (var areaPart in payload.Split(';').Where(s => s.Length > 0))
        {
            var seg = areaPart.Split('|');
            if (seg.Length < 2 || !long.TryParse(seg[0], out var areaId))
                continue;

            var started = new List<int>();
            var finished = new List<int>();
            for (var i = 1; i < seg.Length; i++)
            {
                if (seg[i].StartsWith("s:"))
                    ParseIds(seg[i].Substring(2), started);
                else if (seg[i].StartsWith("f:"))
                    ParseIds(seg[i].Substring(2), finished);
            }

            ByArea[seg[0]] = (started, finished);
            GlobalStarted.AddRange(started);
            GlobalFinished.AddRange(finished);
        }
    }

    private static void ParseIds(string csv, List<int> target)
    {
        foreach (var part in csv.Split(',').Where(s => s.Length > 0))
        {
            if (int.TryParse(part, out var id))
                target.Add(id);
        }
    }

    private static void ShowList(string title, IEnumerable<int> ids, string note)
    {
        var list = ids.Distinct().OrderBy(x => x).ToList();
        if (list.Count == 0)
        {
            WukongApi.Chat.ShowLocalMessage($"[cglist] {title}：无", FLinearColor.Gray);
            return;
        }

        WukongApi.Chat.ShowLocalMessage($"[cglist] {title}（{list.Count} 个）：", FLinearColor.Green);
        for (var i = 0; i < list.Count; i += 8)
        {
            WukongApi.Chat.ShowLocalMessage(string.Join("  ", list.Skip(i).Take(8)), FLinearColor.White);
        }

        if (!string.IsNullOrEmpty(note))
            WukongApi.Chat.ShowLocalMessage($"[cglist] {note}", FLinearColor.Gray);

        Logging.LogDebug("[GameICU] cglist {Title}: {Ids}", title, string.Join(",", list));
    }

    // ---- 本地数据收集 ----

    private static List<int> GetQueueIds(AActor pawn)
    {
        var queue = BGWGameInstanceCS.GetObject<BGW_GameDataMgr>(pawn)?.GetGameInstanceWritableData<BIC_MovieData>()?.PlayMovieRequestQueue;
        var list = new List<int>();
        if (queue == null)
            return list;
        foreach (var req in queue)
            list.Add(req.SequenceID);
        return list;
    }

    private static int GetCurrentPlayingId(AActor pawn)
    {
        try
        {
            var movieData = BGU_DataUtil.GetGameStateReadonlyData<BGC_MovieData>(GameUtils.GetWorld());
            return movieData?.CameraMovieInstance?.SequenceId ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// 运行时反射：在指定类型的实例上寻找名字含关键词的 int 集合字段。
    /// UE 桥接类型（BIC_MovieData 等）在参考程序集里字段被剥离，运行时字段才完整，
    /// 所以必须用运行时反射动态发现（防御性：找不到就返回空）。
    /// </summary>
    private static List<int> ReflectIntList(AActor pawn, string dataTypeName, string[] keywords)
    {
        try
        {
            object? data = dataTypeName switch
            {
                "BIC_MovieData" => BGWGameInstanceCS.GetObject<BGW_GameDataMgr>(pawn)?.GetGameInstanceWritableData<BIC_MovieData>(),
                _ => null,
            };
            if (data == null)
                return new List<int>();

            foreach (var field in data.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var name = field.Name.ToLowerInvariant();
                if (!keywords.Any(k => name.Contains(k)))
                    continue;

                var value = field.GetValue(data);
                var list = TryExtractInts(value);
                if (list != null && list.Count > 0)
                {
                    Logging.LogDebug("[GameICU] cglist reflection found field {Field} with {Count} ints", field.Name, list.Count);
                    return list;
                }
            }
        }
        catch (Exception ex)
        {
            Logging.LogDebug("[GameICU] cglist reflection failed: {Error}", ex.Message);
        }

        return new List<int>();
    }

    private static List<int>? TryExtractInts(object? value)
    {
        try
        {
            if (value == null)
                return null;
            if (value is IEnumerable<int> ints)
                return ints.ToList();
            if (value is IList list)
            {
                var result = new List<int>();
                foreach (var item in list)
                {
                    if (item is int i) result.Add(i);
                    else if (item is long l && l is >= int.MinValue and <= int.MaxValue) result.Add((int)l);
                }
                return result.Count > 0 ? result : null;
            }
        }
        catch
        {
            // 字段类型不匹配/无法枚举：跳过
        }

        return null;
    }
}
