using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 결과 씬의 Canvas 아래에 값 표시용 텍스트를 배치하고 ResultView에 연결한다.
// 디자인 기준 1920x1080, 모든 좌표는 화면 중앙 기준이며 Canvas Scaler 기준 해상도에 맞춰 자동 보정한다.
public static class ResultSceneBuilder
{
    private const string RootName = "ResultTexts";
    private const float DesignWidth = 1920f;

    private static float scale = 1f;

    [UnityEditor.MenuItem("Tools/Build Result UI")]
    private static void Build()
    {
        var canvas = GameObject.Find("Canvas");
        if (canvas == null)
        {
            Debug.LogError("Canvas를 찾을 수 없습니다. 05.Result 씬을 연 상태에서 실행하세요.");
            return;
        }

        var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
        scale = scaler != null ? scaler.referenceResolution.x / DesignWidth : 1f;

        var old = canvas.transform.Find(RootName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        var root = new GameObject(RootName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(root, "Build Result UI");
        root.transform.SetParent(canvas.transform, false);

        var rootRect = (RectTransform)root.transform;
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        var rank = CreateText(rootRect, "Rank", "S", new Vector2(0, 70), new Vector2(400, 400), 320, TextAlignmentOptions.Center, 0.5f);
        var score = CreateText(rootRect, "Score", "923,230", new Vector2(0, -305), new Vector2(700, 120), 100, TextAlignmentOptions.Center, 0.5f);
        var excellent = CreateText(rootRect, "Excellent", "420", new Vector2(785, 162), new Vector2(200, 60), 48, TextAlignmentOptions.MidlineRight, 1f);
        var good = CreateText(rootRect, "Good", "28", new Vector2(785, 100), new Vector2(200, 60), 48, TextAlignmentOptions.MidlineRight, 1f);
        var bad = CreateText(rootRect, "Bad", "4", new Vector2(785, 40), new Vector2(200, 60), 48, TextAlignmentOptions.MidlineRight, 1f);
        var miss = CreateText(rootRect, "Miss", "2", new Vector2(785, -21), new Vector2(200, 60), 48, TextAlignmentOptions.MidlineRight, 1f);
        var maxCombo = CreateText(rootRect, "MaxCombo", "428", new Vector2(788, -123), new Vector2(200, 60), 52, TextAlignmentOptions.MidlineRight, 1f);
        var accuracy = CreateText(rootRect, "Accuracy", "98.54%", new Vector2(790, -212), new Vector2(300, 80), 64, TextAlignmentOptions.MidlineRight, 1f);

        var view = root.AddComponent<ResultView>();
        var so = new SerializedObject(view);
        so.FindProperty("rank").objectReferenceValue = rank;
        so.FindProperty("score").objectReferenceValue = score;
        so.FindProperty("accuracy").objectReferenceValue = accuracy;
        so.FindProperty("maxCombo").objectReferenceValue = maxCombo;
        so.FindProperty("excellent").objectReferenceValue = excellent;
        so.FindProperty("good").objectReferenceValue = good;
        so.FindProperty("bad").objectReferenceValue = bad;
        so.FindProperty("miss").objectReferenceValue = miss;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(canvas.scene);
    }

    private static TMP_Text CreateText(RectTransform parent, string name, string sample, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment, float pivotX)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(pivotX, 0.5f);
        rect.anchoredPosition = position * scale;
        rect.sizeDelta = size * scale;

        var text = go.AddComponent<TextMeshProUGUI>();
        text.text = sample;
        text.fontSize = fontSize * scale;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }
}
