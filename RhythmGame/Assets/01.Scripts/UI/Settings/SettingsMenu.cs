using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 설정창 키보드 조작. ↑↓ 줄 이동(맨 위에서 ↑ = 탭 줄), ←→ 값 변경 / 탭 줄에서는 탭 이동, Enter 값 변경.
/// 값이 바뀔 때마다 DataManager에 저장한다. 열기/닫기는 SettingsPopup 담당.
/// </summary>
public class SettingsMenu : MonoBehaviour
{
    [SerializeField] private SettingsTab[] tabs;
    [SerializeField] private SettingsSelectionView selection;

    private const int TabBar = -1;

    private int tabIndex;
    private int rowIndex;
    private SettingsData working;

    // 현재 탭에서 보이는 줄만 담는다
    private readonly List<SettingRow> visibleRows = new List<SettingRow>();

    private IReadOnlyList<SettingRow> Rows => visibleRows;

    private void OnEnable()
    {
        working = LoadCopy();
        CollectRows();
        rowIndex = Rows.Count > 0 ? 0 : TabBar;
        Refresh(true);

        if (DataManager.Instance != null) DataManager.Instance.OnSettingsChanged += OnSettingsChanged;
    }

    private void OnDisable()
    {
        if (DataManager.Instance != null) DataManager.Instance.OnSettingsChanged -= OnSettingsChanged;
    }

    // 다른 곳(자동 보정 등)에서 설정이 바뀌면 화면 값도 갱신한다
    private void OnSettingsChanged(SettingsData data)
    {
        working = Copy(data);
        Refresh(false);
    }

    private void Update()
    {
        if (CalibrationPanel.BlocksInput) return; // 자동 보정 화면이 키 입력을 쓴다

        if (Input.GetKeyDown(KeyCode.UpArrow)) MoveRow(-1);
        else if (Input.GetKeyDown(KeyCode.DownArrow)) MoveRow(1);
        else if (Input.GetKeyDown(KeyCode.LeftArrow)) MoveHorizontal(-1);
        else if (Input.GetKeyDown(KeyCode.RightArrow)) MoveHorizontal(1);
        else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Confirm();
    }

    private void MoveRow(int direction)
    {
        rowIndex = Mathf.Clamp(rowIndex + direction, TabBar, Rows.Count - 1);
        Refresh(false);
    }

    private void MoveHorizontal(int direction)
    {
        if (rowIndex == TabBar)
        {
            tabIndex = (tabIndex + direction + tabs.Length) % tabs.Length;
            CollectRows();
            Refresh(false);
        }
        else ChangeValue(direction);
    }

    private void Confirm()
    {
        if (rowIndex == TabBar) MoveRow(1);
        else if (Rows[rowIndex].IsAction) Rows[rowIndex].Submit();
        else ChangeValue(1);
    }

    private void ChangeValue(int direction)
    {
        var row = Rows[rowIndex];
        if (!row.IsChangeable) return;

        float current = SettingsAccessor.Get(working, row.Key);
        float next = row.Step(current, direction);
        if (Mathf.Approximately(current, next)) return;

        SettingsAccessor.Set(working, row.Key, next);
        row.Show(next);
        Save();
    }

    private void CollectRows()
    {
        visibleRows.Clear();
        foreach (var row in tabs[tabIndex].Rows)
        {
            row.gameObject.SetActive(row.IsVisible);
            if (row.IsVisible) visibleRows.Add(row);
        }
    }

    private void Refresh(bool snap)
    {
        for (int t = 0; t < tabs.Length; t++)
            tabs[t].SetActive(t == tabIndex);

        for (int r = 0; r < Rows.Count; r++)
        {
            Rows[r].Show(SettingsAccessor.Get(working, Rows[r].Key));
            Rows[r].SetSelected(r == rowIndex);
        }

        var rowRect = rowIndex == TabBar ? null : (RectTransform)Rows[rowIndex].transform;
        selection.Show(tabs[tabIndex].LabelRect, rowRect, snap);
    }

    // DataManager가 가진 설정을 직접 바꾸지 않도록 복사본으로 작업한다
    private static SettingsData LoadCopy()
    {
        var dm = DataManager.Instance;
        return dm != null ? Copy(dm.Settings) : new SettingsData();
    }

    private void Save()
    {
        var dm = DataManager.Instance;
        if (dm == null) return;
        dm.SaveSettings(working);
        working = Copy(dm.Settings);
    }

    private static SettingsData Copy(SettingsData source)
    {
        return JsonUtility.FromJson<SettingsData>(JsonUtility.ToJson(source));
    }
}
