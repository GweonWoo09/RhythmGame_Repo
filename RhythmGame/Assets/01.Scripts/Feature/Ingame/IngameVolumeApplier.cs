using RhythmGameStarter;
using UnityEngine;

/// <summary>
/// 설정의 볼륨을 인게임 곡(배경음)과 타격음(효과음)에 적용한다.
/// </summary>
public class IngameVolumeApplier : MonoBehaviour
{
    [SerializeField] private SongManager songManager;

    private Track[] tracks;

    private void Start()
    {
        tracks = FindObjectsByType<Track>(FindObjectsSortMode.None);

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
        if (songManager != null && songManager.audioSource != null)
            songManager.audioSource.volume = data.bgmVolume;

        foreach (var track in tracks)
            if (track != null && track.trackHitAudio != null)
                track.trackHitAudio.volume = data.sfxVolume;
    }
}
