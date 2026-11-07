using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// 설정 팝업 프리팹(Resources/UI/SettingsPopup)을 settings_renamed 그래픽으로 만든다.
// 디자인 기준 1920x1080, 좌표는 화면 중앙 기준. 다시 실행하면 프리팹을 덮어쓴다.
public static class SettingsPopupBuilder
{
    private const string SpriteDir = "Assets/03.Sprite/UI/settings_renamed/";
    private const string FontPath = "Assets/90.TextMesh Pro/Fonts/JayeonSans/JayeonSans-Medium SDF.asset";
    private const string PrefabDir = "Assets/Resources/UI";
    private const string PrefabPath = PrefabDir + "/SettingsPopup.prefab";

    private const float RowTopY = 149.1f;   // 첫 줄 중심
    private const float RowPitch = 92.3f;   // 줄 간격
    private const float RowX = 17.5f;
    private const float ValueX = 350f;      // 줄 기준 값 위치

    private static readonly Color LabelColor = new Color(1f, 1f, 1f, 0.55f);
    private static readonly Color TitleColor = new Color(0.75f, 0.72f, 0.68f, 0.75f);

    private static TMP_FontAsset font;

    private enum RowType { Choice, Number, Slider, Exit }

    private struct RowDef
    {
        public RowType type; public SettingKey key; public string label;
        public string[] options; public float min, max, step; public string format, suffix;
    }

    private struct TabDef { public string name; public float x; public string title; public RowDef[] rows; }

    [UnityEditor.MenuItem("Tools/Build Settings Popup")] // 프로젝트에 MenuItem 클래스가 따로 있어서 전체 이름으로 적는다
    private static void Build()
    {
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null) font = TMP_Settings.defaultFontAsset;

        var tabDefs = new[]
        {
            new TabDef { name = "GAME", x = -387f, title = "GAME SETTING", rows = new[]
            {
                new RowDef { type = RowType.Number, key = SettingKey.NoteSpeed, label = "노트 속도", min = 1f, max = 10f, step = 0.5f, format = "0.0" },
                new RowDef { type = RowType.Number, key = SettingKey.AudioOffset, label = "싱크 조절", min = -300f, max = 300f, step = 5f, format = "+0;-0;0", suffix = " ms" },
                new RowDef { type = RowType.Exit, key = SettingKey.None, label = "곡 나가기" }, // 인게임에서만 보임
            }},
            new TabDef { name = "SOUND", x = -132f, title = "SOUND SETTING", rows = new[]
            {
                new RowDef { type = RowType.Slider, key = SettingKey.BgmVolume, label = "배경음", step = 0.1f },
                new RowDef { type = RowType.Slider, key = SettingKey.SfxVolume, label = "효과음", step = 0.1f },
            }},
            new TabDef { name = "CONTROL", x = 103f, title = "CONTROL SETTING", rows = new[]
            {
                new RowDef { type = RowType.Choice, key = SettingKey.None, label = "키 배치", options = new[] { "D  F  J  K" } },
            }},
        };

        // 루트 (Canvas)
        var root = new GameObject("SettingsPopup", typeof(RectTransform));
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();
        var popup = root.AddComponent<SettingsPopup>();
        root.AddComponent<AudioSettingsApplier>();

        var panel = CreateRect(root.transform, "Panel", Vector2.zero, Vector2.zero);
        Stretch(panel);

        var bg = CreateFullImage(panel, "Background", "Settings_Background");
        bg.raycastTarget = true; // 뒤쪽 UI 클릭 막기
        CreateFullImage(panel, "Divider_Lines", "Divider_Lines");
        CreateFullImage(panel, "Key_Guide_Buttons", "Key_Guide_Buttons");

        // 탭 강조
        var tabSelection = CreateRect(panel, "TabSelection", new Vector2(tabDefs[0].x, 0f), Vector2.zero);
        CreateImage(tabSelection, "Tab_Highlight", "Tab_Highlight", new Vector2(0f, 325.5f));
        CreateImage(tabSelection, "Tab_Marker", "Tab_Marker", new Vector2(-0.5f, 312.5f));

        // 줄 강조 (Row_Cursor는 전체 화면 이미지라 마름모가 줄에 오도록 위치를 맞춘다)
        var rowSelection = CreateRect(panel, "RowSelection", new Vector2(0f, -35.5f), Vector2.zero);
        CreateImage(rowSelection, "Row_Highlight", "Row_Highlight", new Vector2(-179.5f, 0f));
        var cursor = CreateImage(rowSelection, "Row_Cursor", "Row_Cursor", new Vector2(0f, 35.5f));
        cursor.rectTransform.sizeDelta = new Vector2(1920, 1080);

        // 탭과 페이지
        var tabsRoot = CreateRect(panel, "Tabs", Vector2.zero, Vector2.zero);
        Stretch(tabsRoot);
        var tabs = new SettingsTab[tabDefs.Length];
        for (int i = 0; i < tabDefs.Length; i++) tabs[i] = CreateTab(tabsRoot, tabDefs[i]);

        // 하단 키 안내
        CreateText(panel, "Guide_UpDown", "항목 이동", new Vector2(-757f, -523f), new Vector2(400, 40), 24, TextAlignmentOptions.MidlineLeft, Color.white, 0f);
        CreateText(panel, "Guide_LeftRight", "값 변경 / 탭 이동", new Vector2(-392f, -523f), new Vector2(400, 40), 24, TextAlignmentOptions.MidlineLeft, Color.white, 0f);
        CreateText(panel, "Guide_Enter", "확인", new Vector2(683f, -522f), new Vector2(150, 40), 24, TextAlignmentOptions.MidlineLeft, Color.white, 0f);
        CreateText(panel, "Guide_Esc", "닫기", new Vector2(857f, -522f), new Vector2(150, 40), 24, TextAlignmentOptions.MidlineLeft, Color.white, 0f);

        // 연결
        var view = panel.gameObject.AddComponent<SettingsSelectionView>();
        var viewSo = new SerializedObject(view);
        viewSo.FindProperty("tabSelection").objectReferenceValue = tabSelection;
        viewSo.FindProperty("rowSelection").objectReferenceValue = rowSelection;
        viewSo.ApplyModifiedPropertiesWithoutUndo();

        var menu = panel.gameObject.AddComponent<SettingsMenu>();
        var menuSo = new SerializedObject(menu);
        SetArray(menuSo.FindProperty("tabs"), tabs);
        menuSo.FindProperty("selection").objectReferenceValue = view;
        menuSo.ApplyModifiedPropertiesWithoutUndo();

        var popupSo = new SerializedObject(popup);
        popupSo.FindProperty("panel").objectReferenceValue = panel.gameObject;
        popupSo.ApplyModifiedPropertiesWithoutUndo();

        panel.gameObject.SetActive(false);

        if (!AssetDatabase.IsValidFolder(PrefabDir)) AssetDatabase.CreateFolder("Assets/Resources", "UI");
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Debug.Log("설정 팝업 프리팹 생성: " + PrefabPath);
    }

    private static SettingsTab CreateTab(RectTransform parent, TabDef def)
    {
        var tabRect = CreateRect(parent, "Tab_" + def.name, Vector2.zero, Vector2.zero);
        Stretch(tabRect);

        var label = CreateText(tabRect, "Label", def.name, new Vector2(def.x, 340f), new Vector2(260, 60), 44, TextAlignmentOptions.Center, Color.white, 0.5f);

        var page = CreateRect(tabRect, "Page", Vector2.zero, Vector2.zero);
        Stretch(page);
        CreateText(page, "Title", def.title, new Vector2(-552f, 252f), new Vector2(900, 70), 48, TextAlignmentOptions.MidlineLeft, TitleColor, 0f);

        var rows = new SettingRow[def.rows.Length];
        for (int i = 0; i < def.rows.Length; i++)
            rows[i] = CreateRow(page, def.rows[i], new Vector2(RowX, RowTopY - RowPitch * i));

        var tab = tabRect.gameObject.AddComponent<SettingsTab>();
        var so = new SerializedObject(tab);
        so.FindProperty("tabLabel").objectReferenceValue = label;
        so.FindProperty("page").objectReferenceValue = page.gameObject;
        SetArray(so.FindProperty("rows"), rows);
        so.ApplyModifiedPropertiesWithoutUndo();
        return tab;
    }

    private static SettingRow CreateRow(RectTransform parent, RowDef def, Vector2 pos)
    {
        var rowRect = CreateRect(parent, "Row_" + (def.type == RowType.Exit ? "Exit" : def.key.ToString()), pos, new Vector2(1135, 92));
        var label = CreateText(rowRect, "Label", def.label, new Vector2(-522.5f, 0f), new Vector2(500, 80), 52, TextAlignmentOptions.MidlineLeft, LabelColor, 0f);

        SettingRow row;
        SerializedObject so;
        if (def.type == RowType.Exit)
        {
            CreateText(rowRect, "Value", "ENTER", new Vector2(ValueX, 0f), new Vector2(260, 70), 46, TextAlignmentOptions.Center, Color.white, 0.5f);
            row = rowRect.gameObject.AddComponent<SongExitSettingRow>();
            so = new SerializedObject(row);
        }
        else if (def.type == RowType.Slider)
        {
            var track = CreateUISprite(rowRect, "Track", new Vector2(ValueX + 2.5f, 0f), new Vector2(194, 8), new Color(0.15f, 0.15f, 0.15f, 0.6f));
            var fill = CreateUISprite(track.rectTransform, "Fill", Vector2.zero, Vector2.zero, new Color(0.85f, 0.85f, 0.85f));
            Stretch(fill.rectTransform);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            var handle = CreateUISprite(track.rectTransform, "Handle", Vector2.zero, new Vector2(20, 20), Color.white);
            handle.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            handle.type = Image.Type.Simple;

            row = rowRect.gameObject.AddComponent<SliderSettingRow>();
            so = new SerializedObject(row);
            so.FindProperty("fill").objectReferenceValue = fill;
            so.FindProperty("handle").objectReferenceValue = handle.rectTransform;
            so.FindProperty("step").floatValue = def.step;
        }
        else
        {
            var arrows = CreateImage(rowRect, "Value_Arrows", "Value_Arrows", new Vector2(ValueX, 1f));
            var value = CreateText(rowRect, "Value", "", new Vector2(ValueX, 0f), new Vector2(260, 70), 46, TextAlignmentOptions.Center, Color.white, 0.5f);

            if (def.type == RowType.Number)
            {
                row = rowRect.gameObject.AddComponent<NumberSettingRow>();
                so = new SerializedObject(row);
                so.FindProperty("min").floatValue = def.min;
                so.FindProperty("max").floatValue = def.max;
                so.FindProperty("step").floatValue = def.step;
                so.FindProperty("format").stringValue = def.format ?? "0";
                so.FindProperty("suffix").stringValue = def.suffix ?? "";
            }
            else
            {
                row = rowRect.gameObject.AddComponent<ChoiceSettingRow>();
                so = new SerializedObject(row);
                var options = so.FindProperty("options");
                options.arraySize = def.options.Length;
                for (int i = 0; i < def.options.Length; i++) options.GetArrayElementAtIndex(i).stringValue = def.options[i];
            }

            so.FindProperty("valueText").objectReferenceValue = value;
            var selectedOnly = so.FindProperty("selectedOnly");
            selectedOnly.arraySize = 1;
            selectedOnly.GetArrayElementAtIndex(0).objectReferenceValue = arrows.gameObject;
            arrows.gameObject.SetActive(false);
        }

        so.FindProperty("key").enumValueIndex = (int)def.key;
        so.FindProperty("label").objectReferenceValue = label;
        so.ApplyModifiedPropertiesWithoutUndo();
        return row;
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

    private static Sprite LoadSprite(string fileName)
    {
        var path = AssetDatabase.FindAssets(fileName, new[] { SpriteDir.TrimEnd('/') })
            .Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault(p => System.IO.Path.GetFileNameWithoutExtension(p) == fileName);
        if (path == null) { Debug.LogWarning("스프라이트를 찾을 수 없습니다: " + fileName); return null; }
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
    }

    private static Image CreateFullImage(RectTransform parent, string name, string sprite)
    {
        var image = CreateRect(parent, name, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
        Stretch(image.rectTransform);
        image.sprite = LoadSprite(sprite);
        image.raycastTarget = false;
        return image;
    }

    private static Image CreateImage(RectTransform parent, string name, string sprite, Vector2 pos)
    {
        var image = CreateRect(parent, name, pos, Vector2.zero).gameObject.AddComponent<Image>();
        image.sprite = LoadSprite(sprite);
        image.raycastTarget = false;
        image.SetNativeSize();
        return image;
    }

    private static Image CreateUISprite(RectTransform parent, string name, Vector2 pos, Vector2 size, Color color)
    {
        var image = CreateRect(parent, name, pos, size).gameObject.AddComponent<Image>();
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text CreateText(RectTransform parent, string name, string text, Vector2 pos, Vector2 size, float fontSize, TextAlignmentOptions align, Color color, float pivotX)
    {
        var rect = CreateRect(parent, name, pos, size);
        rect.pivot = new Vector2(pivotX, 0.5f);
        var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.color = color;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static void SetArray(SerializedProperty prop, Object[] items)
    {
        prop.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }
}
