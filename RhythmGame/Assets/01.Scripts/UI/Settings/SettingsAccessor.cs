using UnityEngine;

/// <summary>
/// SettingKey로 SettingsData의 값을 읽고 쓴다.
/// 설정 항목을 추가할 때는 SettingKey와 이 클래스만 고치면 된다.
/// </summary>
public static class SettingsAccessor
{
    public static float Get(SettingsData data, SettingKey key)
    {
        switch (key)
        {
            case SettingKey.BgmVolume: return data.bgmVolume;
            case SettingKey.SfxVolume: return data.sfxVolume;
            case SettingKey.NoteSpeed: return data.noteSpeed;
            case SettingKey.AudioOffset: return data.audioOffsetMs;
            default: return 0f;
        }
    }

    public static void Set(SettingsData data, SettingKey key, float value)
    {
        switch (key)
        {
            case SettingKey.BgmVolume: data.bgmVolume = value; break;
            case SettingKey.SfxVolume: data.sfxVolume = value; break;
            case SettingKey.NoteSpeed: data.noteSpeed = value; break;
            case SettingKey.AudioOffset: data.audioOffsetMs = Mathf.RoundToInt(value); break;
        }
    }
}
