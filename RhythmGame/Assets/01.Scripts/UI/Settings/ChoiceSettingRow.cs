using TMPro;
using UnityEngine;

/// <summary>정해진 보기 중 하나를 고르는 줄. 값은 보기의 번호다.</summary>
public class ChoiceSettingRow : SettingRow
{
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private string[] options = { "OFF", "ON" };

    public override void Show(float value)
    {
        if (valueText != null && options.Length > 0)
            valueText.text = options[Mathf.Clamp(Mathf.RoundToInt(value), 0, options.Length - 1)];
    }

    public override float Step(float value, int direction)
    {
        if (options.Length == 0) return value;
        int index = Mathf.RoundToInt(value) + direction;
        return (index % options.Length + options.Length) % options.Length; // 끝에서 처음으로 넘어간다
    }

    protected override bool CanChange => base.CanChange && options.Length > 1;
}
