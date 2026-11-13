using UnityEngine;

/// <summary>Enter를 누르면 싱크 자동 보정 화면을 여는 줄.</summary>
public class CalibrationSettingRow : SettingRow
{
    [SerializeField] private CalibrationPanel panel;

    public override bool IsAction => true;

    public override void Show(float value) { }

    public override float Step(float value, int direction) => value;

    public override void Submit() => panel.Open();
}
