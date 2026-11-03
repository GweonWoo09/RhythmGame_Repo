using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Loads flow JSON from Resources/Flow/{flowId}.json.</summary>
public static class StoryFlowLoader
{
    private const string Root = "Flow";
    private static readonly Dictionary<string, StoryFlowData> Cache = new Dictionary<string, StoryFlowData>(StringComparer.OrdinalIgnoreCase);

    [Serializable]
    private sealed class FlowJson
    {
        public string flowId;
        public string startNodeId;
        public NodeJson[] nodes;
    }

    [Serializable]
    private sealed class NodeJson
    {
        public string id;
        public string type;
        public string next;
        public int chapter;
        public string target;
        public string scene;
        public string[] songs;
        public string difficulty;
        public bool showResult = true;
        public BranchJson[] branches;
        public string[] setFlags;
        public int clearChapter;
        public int unlockChapter;
    }

    [Serializable]
    private sealed class BranchJson { public string condition; public string next; }

    public static StoryFlowData Load(string flowId)
    {
        if (string.IsNullOrWhiteSpace(flowId))
        {
            Debug.LogError("[StoryFlowLoader] flowId가 비어 있습니다.");
            return null;
        }
        if (Cache.TryGetValue(flowId, out StoryFlowData cached)) return cached;

        TextAsset asset = Resources.Load<TextAsset>($"{Root}/{flowId}");
        if (asset == null)
        {
            Debug.LogError($"[StoryFlowLoader] 흐름 파일을 찾을 수 없습니다: Resources/{Root}/{flowId}.json");
            return null;
        }

        FlowJson json;
        try { json = JsonUtility.FromJson<FlowJson>(asset.text); }
        catch (ArgumentException e)
        {
            Debug.LogError($"[StoryFlowLoader] JSON 형식 오류 (Resources/{Root}/{flowId}.json): {e.Message}");
            return null;
        }
        if (json == null || string.IsNullOrWhiteSpace(json.startNodeId) || json.nodes == null)
        {
            Debug.LogError($"[StoryFlowLoader] flowId, startNodeId, nodes가 필요합니다: Resources/{Root}/{flowId}.json");
            return null;
        }

        var data = new StoryFlowData
        {
            flowId = string.IsNullOrWhiteSpace(json.flowId) ? flowId : json.flowId,
            startNodeId = json.startNodeId,
            nodes = new StoryFlowNode[json.nodes.Length]
        };
        for (int i = 0; i < json.nodes.Length; i++)
        {
            NodeJson src = json.nodes[i];
            if (src == null) continue;
            if (!Enum.TryParse(src.type, true, out StoryFlowNodeType type) || !Enum.IsDefined(typeof(StoryFlowNodeType), type))
            {
                Debug.LogError($"[StoryFlowLoader] 노드 '{src.id}'의 type '{src.type}'이 올바르지 않습니다.");
                return null;
            }
            var branches = new StoryFlowBranch[src.branches == null ? 0 : src.branches.Length];
            for (int j = 0; j < branches.Length; j++)
                if (src.branches[j] != null) branches[j] = new StoryFlowBranch { condition = src.branches[j].condition, next = src.branches[j].next };
            data.nodes[i] = new StoryFlowNode
            {
                id = src.id, type = type, next = src.next, chapter = src.chapter,
                target = src.target, scene = src.scene, songs = src.songs, difficulty = src.difficulty,
                showResult = src.showResult, branches = branches, setFlags = src.setFlags,
                clearChapter = src.clearChapter, unlockChapter = src.unlockChapter
            };
        }

        if (data.Find(data.startNodeId) == null)
        {
            Debug.LogError($"[StoryFlowLoader] 시작 노드 '{data.startNodeId}'가 없습니다 (flow '{data.flowId}').");
            return null;
        }
        Cache[flowId] = data;
        return data;
    }

    public static void Reload(string flowId = null)
    {
        if (string.IsNullOrWhiteSpace(flowId)) Cache.Clear();
        else Cache.Remove(flowId);
    }
}
