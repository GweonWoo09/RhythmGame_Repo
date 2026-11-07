using TMPro;
using UnityEngine;

/// <summary>숫자를 일정 간격으로 올리고 내리는 줄 (노트 속도, 싱크 등).</summary>
public class NumberSettingRow : SettingRow
{
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private float min;
    [SerializeField] private float max = 10f;
    [SerializeField] private float step = 1f;
    [SerializeField] private string format = "0";
    [SerializeField] private string suffix;

    public override void Show(float value)
    {
        if (valueText != null) valueText.text = value.ToString(format) + suffix;
    }

    public override float Step(float value, int direction)
    {
        float next = Mathf.Round((value + step * direction) / step) * step;
        return Mathf.Clamp(next, min, max);
    }
}
