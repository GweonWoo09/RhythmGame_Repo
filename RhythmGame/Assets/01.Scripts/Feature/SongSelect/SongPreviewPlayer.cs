using System.Collections;
using UnityEngine;

/// <summary>
/// 선택한 곡의 미리듣기 재생만 담당한다. 실제 재생은 AudioManager에 맡긴다.
/// </summary>
public class SongPreviewPlayer : MonoBehaviour
{
    [SerializeField] private SongSelectController controller;

    [Tooltip("곡을 빠르게 넘길 때 매번 재생하지 않도록 기다리는 시간(초)")]
    [SerializeField] private float delay = 0.3f;

    [Tooltip("곡 선택창을 떠날 때 미리듣기를 끄는 시간(초)")]
    [SerializeField] private float stopFadeDuration = 0.3f;

    private Coroutine pending;

    private void OnEnable()
    {
        controller.SongIndexChanged += OnSongChanged;
        controller.Leaving += OnLeaving;
    }

    private void OnDisable()
    {
        controller.SongIndexChanged -= OnSongChanged;
        controller.Leaving -= OnLeaving;
        CancelPending();
    }

    private void OnSongChanged(int index)
    {
        CancelPending();
        pending = StartCoroutine(PlayAfterDelay(controller.CurrentSong));
    }

    private IEnumerator PlayAfterDelay(SongDataSO song)
    {
        yield return new WaitForSecondsRealtime(delay);
        pending = null;

        var clip = song != null ? song.GetPreviewClip() : null;
        if (clip != null && AudioManager.Instance != null)
            AudioManager.Instance.PlayBGM(clip); // 같은 곡이 재생 중이면 AudioManager가 무시함
    }

    private void OnLeaving()
    {
        CancelPending();
        if (AudioManager.Instance != null)
            AudioManager.Instance.StopBGM(stopFadeDuration);
    }

    private void CancelPending()
    {
        if (pending == null) return;
        StopCoroutine(pending);
        pending = null;
    }
}
