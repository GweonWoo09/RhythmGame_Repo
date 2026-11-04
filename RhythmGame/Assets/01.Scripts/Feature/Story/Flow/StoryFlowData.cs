using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>흐름의 한 칸이 하는 일</summary>
public enum StoryFlowNodeType
{
    /// <summary>대화 시퀀스를 재생한다 (Story 씬)</summary>
    Story,

    /// <summary>리듬게임 곡을 연주한다 (RhythmPlay 씬, 곡이 여러 개면 차례로)</summary>
    Play,

    /// <summary>아무것도 하지 않고 조건에 따라 갈 곳만 정한다</summary>
    Route,

    /// <summary>엔딩. 엔딩 기록을 남기고 흐름을 끝낸다</summary>
    End
}

/// <summary>조건부 이동 한 건. 노드의 branches 중 처음으로 조건이 참인 것을 따라간다.</summary>
public struct StoryFlowBranch
{
    public string condition;
    public string next;
}

/// <summary>
/// 이야기 진행 순서(스토리 -> 연주 -> 스토리 ...)의 한 칸.
/// 칸이 끝나면: setFlags 적용 -> 챕터 클리어/해금 -> (branches 중 참인 것) 또는 next 로 이동.
/// </summary>
public sealed class StoryFlowNode
{
    public string id;
    public StoryFlowNodeType type;

    /// <summary>Story: 대화 시퀀스 ID / End: 엔딩 ID</summary>
    public string target;

    /// <summary>Play: 연주할 곡 ID 목록 (순서대로)</summary>
    public string[] songs;

    /// <summary>Play: 난이도 (비우면 지정하지 않음)</summary>
    public string difficulty;

    /// <summary>0보다 크면 이 칸이 해당 챕터의 시작점이다. 지나가는 동안 현재 챕터가 이 값으로 바뀐다.</summary>
    public int chapter;

    /// <summary>Play: 곡이 끝난 뒤 결과 화면을 보여줄지 (false면 바로 다음으로)</summary>
    public bool showResult = true;

    /// <summary>End: 엔딩 후 이동할 씬 이름 (SceneChanger.SceneType). 비우면 StoryFlow.DefaultEndScene</summary>
    public string scene;

    /// <summary>다음 칸의 id. 비어 있으면 흐름이 여기서 끝난다.</summary>
    public string next;

    public StoryFlowBranch[] branches;
    public string[] setFlags;
    public int clearChapter;
    public int unlockChapter;
}

/// <summary>하나의 흐름(예: "main")의 전체 구성</summary>
public sealed class StoryFlowData
{
    public string flowId;
    public string startNodeId;

    public readonly List<StoryFlowNode> nodes = new List<StoryFlowNode>();
    private readonly Dictionary<string, StoryFlowNode> _byId = new Dictionary<string, StoryFlowNode>();

    public StoryFlowNode Find(string nodeId)
    {
        StoryFlowNode node;
        return (nodeId != null && _byId.TryGetValue(nodeId, out node)) ? node : null;
    }

    /// <summary>chapter 값이 chapterId인 첫 칸 (챕터 시작점). 없으면 null.</summary>
    public StoryFlowNode FindChapterStart(int chapterId)
    {
        foreach (StoryFlowNode node in nodes)
        {
            if (node.chapter == chapterId) return node;
        }

        return null;
    }

    public bool Add(StoryFlowNode node)
    {
        if (string.IsNullOrEmpty(node.id) || _byId.ContainsKey(node.id)) return false;

        _byId[node.id] = node;
        nodes.Add(node);
        return true;
    }
}

#region JSON Models
// DialogueJsonLoader와 같은 이유로 JSON에는 문자열/숫자만 담는다.
// "if"는 C# 예약어라 @if 로 선언했다 (JsonUtility가 보는 필드 이름은 "if").

[Serializable]
public class StoryFlowJson
{
    public string flow;
    public string start;
    public StoryFlowNodeJson[] nodes;
}

[Serializable]
public class StoryFlowNodeJson
{
    public string id;
    public string type;          // story / play / route / end
    public string target;        // story: 시퀀스 ID, end: 엔딩 ID
    public string[] songs;       // play: 곡 ID 목록
    public string difficulty;
    public string result;        // play: "skip"이면 결과 화면 없이 진행 (그 외는 보여줌)
    public string scene;         // end: 이동할 씬 이름
    public int chapter;
    public string[] setFlags;
    public int clearChapter;
    public int unlockChapter;
    public StoryFlowBranchJson[] branch;
    public string next;
}

[Serializable]
public class StoryFlowBranchJson
{
    public string @if;
    public string next;
}
#endregion

/// <summary>
/// Resources/Flow/{flowId}.json 을 읽어 StoryFlowData로 변환한다. (언어와 무관하므로 언어 폴더가 없다)
/// 시트의 flow 탭에서 임포터가 만들어 주는 파일이다.
/// </summary>
public static class StoryFlowLoader
{
    private const string FlowRoot = "Flow";

    private static readonly Dictionary<string, StoryFlowData> _cache = new Dictionary<string, StoryFlowData>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _cache.Clear();
    }

    /// <summary>임포터로 JSON을 다시 만든 뒤 플레이 중에 바로 반영하고 싶을 때</summary>
    public static void ClearCache()
    {
        _cache.Clear();
    }

    public static StoryFlowData Load(string flowId)
    {
        if (string.IsNullOrEmpty(flowId))
        {
            Debug.LogError("[StoryFlowLoader] flowId가 비어 있습니다.");
            return null;
        }

        StoryFlowData cached;
        if (_cache.TryGetValue(flowId, out cached)) return cached;

        TextAsset asset = Resources.Load<TextAsset>($"{FlowRoot}/{flowId}");
        if (asset == null)
        {
            Debug.LogError($"[StoryFlowLoader] 흐름 파일을 찾을 수 없습니다: Resources/{FlowRoot}/{flowId}.json");
            return null;
        }

        StoryFlowJson json;
        try
        {
            json = JsonUtility.FromJson<StoryFlowJson>(asset.text);
        }
        catch (ArgumentException e)
        {
            Debug.LogError($"[StoryFlowLoader] JSON 형식 오류 ({flowId}): {e.Message}");
            return null;
        }

        if (json == null || json.nodes == null || json.nodes.Length == 0)
        {
            Debug.LogError($"[StoryFlowLoader] 노드(nodes)가 비어 있습니다: {flowId}");
            return null;
        }

        StoryFlowData data = Convert(json, flowId);
        if (data == null) return null;

        _cache[flowId] = data;
        return data;
    }

    private static StoryFlowData Convert(StoryFlowJson json, string flowId)
    {
        var data = new StoryFlowData { flowId = flowId };

        foreach (StoryFlowNodeJson src in json.nodes)
        {
            StoryFlowNodeType type;
            if (!TryParseType(src.type, out type))
            {
                Debug.LogError($"[StoryFlowLoader] {flowId}: 노드 '{src.id}'의 type '{src.type}'을(를) 알 수 없습니다.");
                return null;
            }

            var node = new StoryFlowNode
            {
                id = src.id,
                type = type,
                target = src.target,
                songs = NullIfEmpty(src.songs),
                difficulty = src.difficulty,
                chapter = src.chapter,
                showResult = !string.Equals(src.result, "skip", StringComparison.OrdinalIgnoreCase),
                scene = src.scene,
                next = src.next,
                branches = ConvertBranches(src.branch),
                setFlags = NullIfEmpty(src.setFlags),
                clearChapter = src.clearChapter,
                unlockChapter = src.unlockChapter
            };

            if (!data.Add(node))
            {
                Debug.LogError($"[StoryFlowLoader] {flowId}: 노드 id '{src.id}'가 비어 있거나 중복됩니다.");
                return null;
            }
        }

        data.startNodeId = string.IsNullOrEmpty(json.start) ? data.nodes[0].id : json.start;

        if (data.Find(data.startNodeId) == null)
        {
            Debug.LogError($"[StoryFlowLoader] {flowId}: 시작 노드 '{data.startNodeId}'가 없습니다.");
            return null;
        }

        return data;
    }

    private static bool TryParseType(string text, out StoryFlowNodeType type)
    {
        switch ((text ?? "").Trim().ToLowerInvariant())
        {
            case "story": type = StoryFlowNodeType.Story; return true;
            case "play": type = StoryFlowNodeType.Play; return true;
            case "route": type = StoryFlowNodeType.Route; return true;
            case "end": type = StoryFlowNodeType.End; return true;
        }

        type = StoryFlowNodeType.Route;
        return false;
    }

    private static StoryFlowBranch[] ConvertBranches(StoryFlowBranchJson[] src)
    {
        if (src == null || src.Length == 0) return null;

        var branches = new StoryFlowBranch[src.Length];
        for (int i = 0; i < branches.Length; i++)
        {
            branches[i] = new StoryFlowBranch { condition = src[i].@if, next = src[i].next };
        }

        return branches;
    }

    private static T[] NullIfEmpty<T>(T[] array)
    {
        return (array == null || array.Length == 0) ? null : array;
    }
}