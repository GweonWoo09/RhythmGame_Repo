using UnityEngine;

/// <summary>설정이 바뀌면 볼륨을 AudioManager에 적용한다.</summary>
public class AudioSettingsApplier : MonoBehaviour
{
    private void Start()
    {
        var dm = DataManager.Instance;
        if (dm == null) return;

        dm.OnSettingsChanged += Apply;
        Apply(dm.Settings);
    }

    private void OnDestroy()
    {
        if (DataManager.Instance != null) DataManager.Instance.OnSettingsChanged -= Apply;
    }

    private void Apply(SettingsData data)
    {
        var audio = AudioManager.Instance;
        if (audio == null) return;

        audio.SetBGMVolume(data.bgmVolume);
        audio.SetSFXVolume(data.sfxVolume);
    }
}
