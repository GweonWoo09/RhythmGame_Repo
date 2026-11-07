using System.Collections.Generic;

/// <summary>
/// 지금 바꾸면 안 되는 설정을 잠근다 (예: 인게임 중 노트 속도).
/// 잠긴 줄은 설정창에서 값이 바뀌지 않는다.
/// </summary>
public static class SettingsLock
{
    private static readonly HashSet<SettingKey> locked = new HashSet<SettingKey>();

    public static bool IsLocked(SettingKey key) => locked.Contains(key);

    public static void Lock(SettingKey key) => locked.Add(key);

    public static void Unlock(SettingKey key) => locked.Remove(key);
}
