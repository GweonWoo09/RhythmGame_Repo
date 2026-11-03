using System;
using System.Collections.Generic;

/// <summary>One story route loaded from Resources/Flow/{flowId}.json.</summary>
[Serializable]
public sealed class StoryFlowData
{
    public string flowId;
    public string startNodeId;
    public StoryFlowNode[] nodes;

    [NonSerialized] private Dictionary<string, StoryFlowNode> _byId;

    public StoryFlowNode Find(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId)) return null;
        BuildIndex();
        return _byId.TryGetValue(nodeId, out StoryFlowNode node) ? node : null;
    }

    public StoryFlowNode FindChapterStart(int chapter)
    {
        if (nodes == null || chapter <= 0) return null;
        foreach (StoryFlowNode node in nodes)
            if (node != null && node.chapter == chapter) return node;
        return null;
    }

    private void BuildIndex()
    {
        if (_byId != null) return;
        _byId = new Dictionary<string, StoryFlowNode>(StringComparer.OrdinalIgnoreCase);
        if (nodes == null) return;
        foreach (StoryFlowNode node in nodes)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.id)) continue;
            if (_byId.ContainsKey(node.id)) UnityEngine.Debug.LogError($"[StoryFlowData] 중복 노드 ID '{node.id}' (flow '{flowId}')");
            else _byId.Add(node.id, node);
        }
    }
}

public enum StoryFlowNodeType { Story, Play, Route, End }

[Serializable]
public sealed class StoryFlowNode
{
    public string id;
    public StoryFlowNodeType type;
    public string next;
    public int chapter;
    public string target;
    public string scene;
    public string[] songs;
    public string difficulty;
    public bool showResult = true;
    public StoryFlowBranch[] branches;
    public string[] setFlags;
    public int clearChapter;
    public int unlockChapter;
}

[Serializable]
public sealed class StoryFlowBranch
{
    public string condition;
    public string next;
}
