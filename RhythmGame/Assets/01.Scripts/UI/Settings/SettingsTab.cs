using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>탭 하나 (탭 이름 + 그 탭의 설정 줄들).</summary>
public class SettingsTab : MonoBehaviour
{
    [SerializeField] private TMP_Text tabLabel;
    [SerializeField] private GameObject page;
    [SerializeField] private SettingRow[] rows;
    [SerializeField] private Color activeColor = Color.white;
    [SerializeField] private Color inactiveColor = new Color(0.85f, 0.82f, 0.75f, 0.6f);

    public RectTransform LabelRect => tabLabel.rectTransform;
    public IReadOnlyList<SettingRow> Rows => rows;

    public void SetActive(bool active)
    {
        if (page != null) page.SetActive(active);
        if (tabLabel != null) tabLabel.color = active ? activeColor : inactiveColor;
    }
}
