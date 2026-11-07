using UnityEngine;

/// <summary>Enter를 누르면 곡을 그만두고 나가는 줄. 인게임에서만 보인다.</summary>
public class SongExitSettingRow : SettingRow
{
    public override bool IsVisible => SongExitRequest.IsAvailable;
    public override bool IsAction => true;

    public override void Show(float value) { }

    public override float Step(float value, int direction) => value;

    public override void Submit() => SongExitRequest.Invoke();
}
