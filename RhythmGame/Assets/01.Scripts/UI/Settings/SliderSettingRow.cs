using UnityEngine;
using UnityEngine.UI;

/// <summary>0~1 값을 막대로 보여주는 줄 (볼륨).</summary>
public class SliderSettingRow : SettingRow
{
    [SerializeField] private Image fill;            // Image Type: Filled
    [SerializeField] private RectTransform handle;
    [SerializeField] private float step = 0.1f;

    public override void Show(float value)
    {
        if (fill != null) fill.fillAmount = value;
        if (handle != null)
        {
            handle.anchorMin = new Vector2(value, 0.5f);
            handle.anchorMax = new Vector2(value, 0.5f);
            handle.anchoredPosition = Vector2.zero;
        }
    }

    public override float Step(float value, int direction)
    {
        float next = Mathf.Round((value + step * direction) / step) * step;
        return Mathf.Clamp01(next);
    }
}
