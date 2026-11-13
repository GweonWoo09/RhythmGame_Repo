using RhythmGameStarter;
using UnityEngine;

/// <summary>설정의 싱크 조절 값(ms)을 SongManager에 적용한다. 일시정지 중에 바꿔도 바로 적용된다.</summary>
public class IngameAudioOffsetApplier : MonoBehaviour
{
    [SerializeField] private SongManager songManager;

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

    private void Apply(SettingsData data) => songManager.timingOffset = data.audioOffsetMs / 1000f;
}
