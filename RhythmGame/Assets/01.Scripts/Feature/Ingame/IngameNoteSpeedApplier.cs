using RhythmGameStarter;
using UnityEngine;

/// <summary>
/// 설정의 노트 속도를 곡 시작 전에 TrackManager.beatSize로 적용한다.
/// BPM으로 나눠 계산하므로 곡이 달라도 같은 설정이면 같은 속도로 내려온다.
/// 곡 도중에 바꾸면 노트 위치가 어긋나므로 인게임 동안은 설정을 잠근다.
/// </summary>
public class IngameNoteSpeedApplier : MonoBehaviour
{
    [SerializeField] private TrackManager trackManager;
    [SerializeField] private SongManager songManager;
    [Tooltip("노트 속도 1당 1초에 내려오는 거리")]
    [SerializeField] private float unitsPerSecondPerSpeed = 4f;

    private void Awake()
    {
        var song = PlaySession.Song != null ? PlaySession.Song : songManager.defaultSong;
        var dm = DataManager.Instance;
        if (song == null || dm == null) return;

        trackManager.beatSize = unitsPerSecondPerSpeed * dm.Settings.noteSpeed * 60f / song.bpm;
    }

    private void OnEnable() => SettingsLock.Lock(SettingKey.NoteSpeed);

    private void OnDisable() => SettingsLock.Unlock(SettingKey.NoteSpeed);
}
