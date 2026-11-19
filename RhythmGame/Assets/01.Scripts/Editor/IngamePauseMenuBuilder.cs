using System.Linq;
using System.Reflection;
using RhythmGameStarter;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// 인게임 일시정지 메뉴를 03.Sprite/UI/Pause 그래픽으로 열려 있는 씬(04.InGame)에 만든다.
// 그림은 1920x1080 원본에서 잘라 낸 스프라이트라, 잘라 낸 위치 그대로 놓으면 디자인과 같아진다.
// 다시 실행하면 지우고 새로 만든다.
public static class IngamePauseMenuBuilder
{
    private const string SpriteDir = "Assets/03.Sprite/UI/Pause/";
    private const string FontPath = "Assets/90.TextMesh Pro/Fonts/JayeonSans/JayeonSans-Medium SDF.asset";
    private const string RootName = "IngamePauseMenu";
    private const float DesignWidth = 1920f;
    private const float DesignHeight = 1080f;

    // 항목 줄의 중심 y (화면 가운데 기준). 구분선(Line) 위치에서 잰 값
    private static readonly float[] RowY = { 172.5f, 68.5f, -39.5f, -143.5f };
    private static readonly string[] RowLabels = { "계속하기", "다시 시작", "선곡 화면으로 돌아가기", "로비로 돌아가기" };

    private static TMP_FontAsset font;

    [UnityEditor.MenuItem("Tools/Build Ingame Pause Menu")] // 프로젝트에 MenuItem 클래스가 따로 있어서 전체 이름으로 적는다
    private static void Build()
    {
        var songManager = Object.FindAnyObjectByType<SongManager>();
        var pauseController = Object.FindAnyObjectByType<IngamePauseController>();
        if (songManager == null || pauseController == null)
        {
            Debug.LogError("04.InGame 씬을 열고 실행하세요. (SongManager / IngamePauseController를 찾을 수 없습니다)");
            return;
        }

        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null) font = TMP_Settings.defaultFontAsset;

        var old = GameObject.Find(RootName);
        if (old != null) Undo.DestroyObjectImmediate(old);

        // 루트 (Canvas) - 설정창(100)보다 아래, 게임 화면보다 위
        var root = new GameObject(RootName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(root, "Build Ingame Pause Menu");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(DesignWidth, DesignHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        root.AddComponent<GraphicRaycaster>();

        var panel = CreateRect(root.transform, "Panel", Vector2.zero, Vector2.zero);
        Stretch(panel);

        // 그리는 순서: 배경 → 버튼 배경 → 항목(선택 강조 + 글자) → 구분선 → 제목 → 하단 키 안내
        PlaceAll(panel, "Panel", true);
        PlaceAll(panel, "ButtonBg");

        var pauseMenu = root.AddComponent<IngamePauseMenu>();
        UnityAction[] actions = { pauseMenu.Resume, pauseMenu.Restart, pauseMenu.GoToSongSelect, pauseMenu.GoToLobby };

        var selectSprite = LoadSprites("Select").FirstOrDefault();
        var items = new global::MenuItem[RowLabels.Length];
        for (int i = 0; i < RowLabels.Length; i++)
            items[i] = CreateRow(panel, i, selectSprite, actions[i]);

        PlaceAll(panel, "Line");
        PlaceAll(panel, "Title");
        PlaceAll(panel, "KeyBg", true);
        PlaceAll(panel, "Key");

        CreateText(panel, "Guide_Move", "항목이동", new Vector2(-760f, -482f), new Vector2(200, 40), 18, TextAlignmentOptions.MidlineLeft, 0f);
        CreateText(panel, "Guide_Confirm", "확인", new Vector2(680f, -482f), new Vector2(120, 40), 18, TextAlignmentOptions.MidlineLeft, 0f);
        CreateText(panel, "Guide_Close", "닫기", new Vector2(855f, -482f), new Vector2(120, 40), 18, TextAlignmentOptions.MidlineLeft, 0f);

        // 연결
        var menu = panel.gameObject.AddComponent<KeyboardMenu>();
        var menuSo = new SerializedObject(menu);
        var itemsProp = menuSo.FindProperty("items");
        itemsProp.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++) itemsProp.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        menuSo.ApplyModifiedPropertiesWithoutUndo();

        var so = new SerializedObject(pauseMenu);
        so.FindProperty("pauseController").objectReferenceValue = pauseController;
        so.FindProperty("songManager").objectReferenceValue = songManager;
        so.FindProperty("panel").objectReferenceValue = panel.gameObject;
        so.FindProperty("menu").objectReferenceValue = menu;
        so.ApplyModifiedPropertiesWithoutUndo();

        panel.gameObject.SetActive(false);

        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeObject = root;
        Debug.Log("일시정지 메뉴 생성 완료. 씬을 저장하세요 (Ctrl+S).");
    }

    private static global::MenuItem CreateRow(RectTransform parent, int index, Sprite selectSprite, UnityAction action)
    {
        var row = CreateRect(parent, "Row_" + RowLabels[index], new Vector2(0f, RowY[index]), new Vector2(1100, 105));

        // 선택 강조 (첫 줄 위치에 그려진 그림이라 줄 간격만큼 내려서 놓는다)
        GameObject mark = null;
        if (selectSprite != null)
        {
            var select = PlaceSprite(row, selectSprite, "Select");
            select.rectTransform.anchoredPosition -= new Vector2(0f, RowY[index]); // row 기준 좌표로
            select.rectTransform.anchoredPosition += new Vector2(0f, RowY[index] - RowY[0]);
            mark = select.gameObject;
            mark.SetActive(index == 0);
        }

        var label = CreateText(row, "Label", RowLabels[index], Vector2.zero, new Vector2(1000, 90), 50, TextAlignmentOptions.Center, 0.5f);
        label.characterSpacing = 12f;

        var item = row.gameObject.AddComponent<global::MenuItem>();
        var so = new SerializedObject(item);
        so.FindProperty("selectedMark").objectReferenceValue = mark;
        so.ApplyModifiedPropertiesWithoutUndo();

        var onSubmit = (UnityEvent)typeof(global::MenuItem)
            .GetField("onSubmit", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(item);
        UnityEventTools.AddVoidPersistentListener(onSubmit, action);
        EditorUtility.SetDirty(item);
        return item;
    }

    private static Sprite[] LoadSprites(string fileName)
    {
        return AssetDatabase.LoadAllAssetsAtPath(SpriteDir + fileName + ".png").OfType<Sprite>()
            .OrderBy(s => s.name).ToArray();
    }

    // 그림 파일의 스프라이트를 원본(1920x1080)에서 잘라 낸 위치 그대로 놓는다
    private static void PlaceAll(RectTransform parent, string fileName, bool blockRaycast = false)
    {
        var sprites = LoadSprites(fileName);
        if (sprites.Length == 0) Debug.LogWarning("스프라이트를 찾을 수 없습니다: " + SpriteDir + fileName);
        foreach (var sprite in sprites)
            PlaceSprite(parent, sprite, sprite.name).raycastTarget = blockRaycast; // 배경은 뒤쪽 클릭을 막는다
    }

    private static Image PlaceSprite(RectTransform parent, Sprite sprite, string name)
    {
        var r = sprite.rect;
        var center = new Vector2(r.x + r.width / 2f - DesignWidth / 2f, r.y + r.height / 2f - DesignHeight / 2f);
        var image = CreateRect(parent, name, center, new Vector2(r.width, r.height)).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    private static RectTransform CreateRect(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static TMP_Text CreateText(RectTransform parent, string name, string text, Vector2 pos, Vector2 size, float fontSize, TextAlignmentOptions align, float pivotX)
    {
        var rect = CreateRect(parent, name, pos, size);
        rect.pivot = new Vector2(pivotX, 0.5f);
        var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        return tmp;
    }
}
