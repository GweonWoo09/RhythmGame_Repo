using UnityEngine;

/// <summary>선택된 탭과 줄을 강조 그래픽이 따라가게 한다.</summary>
public class SettingsSelectionView : MonoBehaviour
{
    [SerializeField] private RectTransform tabSelection; // Tab_Highlight + Tab_Marker
    [SerializeField] private RectTransform rowSelection; // Row_Highlight + Row_Cursor
    [SerializeField] private float followSpeed = 20f;

    private float tabTargetX;
    private float rowTargetY;

    public void Show(RectTransform tabLabel, RectTransform row, bool snap)
    {
        tabTargetX = tabSelection.parent.InverseTransformPoint(tabLabel.position).x;

        rowSelection.gameObject.SetActive(row != null);
        if (row != null) rowTargetY = rowSelection.parent.InverseTransformPoint(row.position).y;

        if (snap) Move(1f);
    }

    private void Update()
    {
        Move(1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime));
    }

    private void Move(float t)
    {
        var tab = tabSelection.localPosition;
        tab.x = Mathf.Lerp(tab.x, tabTargetX, t);
        tabSelection.localPosition = tab;

        var row = rowSelection.localPosition;
        row.y = Mathf.Lerp(row.y, rowTargetY, t);
        rowSelection.localPosition = row;
    }
}
