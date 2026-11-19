using System.Collections;
using System.Collections.Generic;
using RhythmGameStarter;
using TMPro;
using UnityEngine;

/// <summary>
/// 인게임 곡 멈춤/재개(카운트다운)만 담당한다.
/// 설정창, 일시정지 메뉴, 창 전환(포커스)처럼 여러 곳이 동시에 멈출 수 있으므로, 멈춘 곳이 모두 풀어야 카운트다운 후 다시 재생한다.
/// </summary>
public class IngamePauseController : MonoBehaviour
{
    [SerializeField] private SongManager songManager;
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private int countdownSeconds = 3;

    private static readonly object SettingsSource = new object();
    private static readonly object FocusSource = new object(); // [수정] 창 전환(Alt+Tab 등)

    private readonly HashSet<object> holders = new HashSet<object>();
    private Coroutine countdown;

    private void OnEnable()
    {
        SettingsPopup.Opened += HoldForSettings;
        SettingsPopup.Closed += ReleaseForSettings;
        SetCountdownText("");
    }

    private void OnDisable()
    {
        SettingsPopup.Opened -= HoldForSettings;
        SettingsPopup.Closed -= ReleaseForSettings;
    }

    private void HoldForSettings() => Hold(SettingsSource);

    private void ReleaseForSettings() => Release(SettingsSource);

    // [수정] 포커스를 잃으면 곡을 멈추고, 돌아오면 카운트다운 후 다시 재생한다
    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus) Release(FocusSource);
        else Hold(FocusSource);
    }

    /// <summary>곡을 멈춘다. 멈춘 곳(source)이 Release를 불러야 풀린다.</summary>
    public void Hold(object source)
    {
        if (!songManager.SongHasStarted) return;

        CancelCountdown(); // 카운트다운 중에 다시 멈추면 카운트다운 취소
        holders.Add(source);
        songManager.PauseSong();
    }

    /// <summary>멈춘 곳이 모두 풀리면 카운트다운 후 다시 재생한다.</summary>
    public void Release(object source)
    {
        if (!holders.Remove(source)) return;
        if (holders.Count == 0 && songManager.SongHasStarted) countdown = StartCoroutine(Countdown());
    }

    private void CancelCountdown()
    {
        if (countdown == null) return;
        StopCoroutine(countdown);
        countdown = null;
        SetCountdownText("");
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
        if (holders.Count == 0 && songManager.SongHasStarted) songManager.ResumeSong(); // 곡을 나간 경우 재개하지 않는다
    }

    private void SetCountdownText(string text)
    {
        if (countdownText != null) countdownText.text = text;
    }
}
