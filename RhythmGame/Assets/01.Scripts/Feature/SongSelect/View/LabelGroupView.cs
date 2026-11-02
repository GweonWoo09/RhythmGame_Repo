using System;
using TMPro;
using UnityEngine;

/// <summary>
/// 글자 여러 개 중 선택된 하나를 강조하는 표시만 담당한다.
/// 난이도 표시, 탭 표시가 이 클래스를 상속해서 쓴다.
/// </summary>
public abstract class LabelGroupView : MonoBehaviour
{
    [SerializeField] protected SongSelectController controller;

    [Tooltip("순서대로 넣는다")]
    [SerializeField] private TMP_Text[] labels;

    [Header("표시 방식")]
    [SerializeField] private Color selectedColor = Color.white;
    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.5f);
    [SerializeField] private Color disabledColor = new Color(1f, 1f, 1f, 0.15f);
    [SerializeField] private float selectedScale = 1.15f;

    /// <param name="selectedIndex">강조할 글자 번호</param>
    /// <param name="isAvailable">선택 가능 여부 (null이면 모두 가능)</param>
    protected void Highlight(int selectedIndex, Func<int, bool> isAvailable = null)
    {
        if (labels == null) return;

        for (int i = 0; i < labels.Length; i++)
        {
            if (!labels[i]) continue;

            bool selected = i == selectedIndex;
            bool available = isAvailable == null || isAvailable(i);

            labels[i].color = selected ? selectedColor : available ? normalColor : disabledColor;
            labels[i].transform.localScale = Vector3.one * (selected ? selectedScale : 1f);
        }
    }
}
