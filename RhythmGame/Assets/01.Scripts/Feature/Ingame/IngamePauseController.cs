using System.Collections;
using RhythmGameStarter;
using TMPro;
using UnityEngine;

/// <summary>
/// 설정창이 열리면 곡을 멈추고, 닫히면 카운트다운 후 다시 재생한다.
/// </summary>
public class IngamePauseController : MonoBehaviour
{
    [SerializeField] private SongManager songManager;
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private int countdownSeconds = 3;

    private Coroutine countdown;
    private bool pausedBySettings;

    private void OnEnable()
    {
        SettingsPopup.Opened += Pause;
        SettingsPopup.Closed += StartCountdown;
        SetCountdownText("");
    }

    private void OnDisable()
    {
        SettingsPopup.Opened -= Pause;
        SettingsPopup.Closed -= StartCountdown;
    }

    private void Pause()
    {
        if (!songManager.SongHasStarted) return;

        // 카운트다운 중에 다시 열면 카운트다운을 취소한다
        if (countdown != null)
        {
            StopCoroutine(countdown);
            countdown = null;
            SetCountdownText("");
        }

        songManager.PauseSong();
        pausedBySettings = true;
    }

    private void StartCountdown()
    {
        if (!pausedBySettings || !songManager.SongHasStarted) return; // 곡을 나간 경우 재개하지 않는다
        countdown = StartCoroutine(Countdown());
    }

    private IEnumerator Countdown()
    {
        for (int i = countdownSeconds; i > 0; i--)
        {
            SetCountdownText(i.ToString());
            yield return new WaitForSecondsRealtime(1f);
        }

        SetCountdownText("");
        countdown = null;
        pausedBySettings = false;
        if (songManager.SongHasStarted) songManager.ResumeSong();
    }

    private void SetCountdownText(string text)
    {
        if (countdownText != null) countdownText.text = text;
    }
}
