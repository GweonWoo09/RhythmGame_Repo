using TMPro;
using UnityEngine;

/// <summary>
/// 싱크 자동 보정 화면 (얼불춤 보정 방식).
/// 메트로놈 소리에 맞춰 키를 누르면 평균 차이를 계산해 싱크 조절 값으로 저장한다.
/// Enter 적용 / R 다시 / ESC 취소.
/// </summary>
public class CalibrationPanel : MonoBehaviour
{
    [SerializeField] private CalibrationMetronome metronome;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private int targetCount = 16;
    [SerializeField] private KeyCode[] hitKeys = { KeyCode.D, KeyCode.F, KeyCode.J, KeyCode.K, KeyCode.Space };

    private const int MinOffsetMs = -300;
    private const int MaxOffsetMs = 300;

    private readonly OffsetCalibrator calibrator = new OffsetCalibrator();
    private bool finished;

    private static bool isOpen;
    private static int closedFrame = -1;

    // 닫힌 프레임에도 막아야 ESC 한 번에 설정창까지 닫히지 않는다
    public static bool BlocksInput => isOpen || Time.frameCount == closedFrame;

    public void Open()
    {
        isOpen = true;
        gameObject.SetActive(true);
        Restart();
    }

    private void OnDisable()
    {
        if (!isOpen) return; // 설정창이 통째로 닫힌 경우도 정리
        isOpen = false;
        closedFrame = Time.frameCount;
        metronome.Stop();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) Close();
        else if (finished) UpdateResult();
        else UpdateMeasuring();
    }

    private void Restart()
    {
        calibrator.Clear();
        finished = false;
        metronome.Play();
        ShowProgress("소리에 맞춰 D F J K 또는 Space를 누르세요");
    }

    private void UpdateMeasuring()
    {
        if (!AnyHitKeyDown()) return;

        double now = AudioSettings.dspTime;
        if (now < metronome.FirstBeatTime - metronome.Interval / 2) return; // 첫 박 전에 누른 건 무시

        int diff = calibrator.Add(now, metronome.FirstBeatTime, metronome.Interval);
        ShowProgress("이번: " + FormatMs(diff));

        if (calibrator.Count >= targetCount) Finish();
    }

    private void Finish()
    {
        finished = true;
        metronome.Stop();
        countText.text = FormatMs(ResultMs);
        statusText.text = "Enter 적용     R 다시     ESC 취소";
    }

    private void UpdateResult()
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            Apply();
            Close();
        }
        else if (Input.GetKeyDown(KeyCode.R)) Restart();
    }

    private int ResultMs => Mathf.Clamp(calibrator.AverageMs, MinOffsetMs, MaxOffsetMs);

    private void Apply()
    {
        var dm = DataManager.Instance;
        if (dm == null) return;

        var data = JsonUtility.FromJson<SettingsData>(JsonUtility.ToJson(dm.Settings));
        data.audioOffsetMs = ResultMs;
        dm.SaveSettings(data);
    }

    private void Close() => gameObject.SetActive(false);

    private void ShowProgress(string status)
    {
        countText.text = calibrator.Count + " / " + targetCount;
        statusText.text = status;
    }

    private bool AnyHitKeyDown()
    {
        foreach (var key in hitKeys)
            if (Input.GetKeyDown(key)) return true;
        return false;
    }

    private static string FormatMs(int ms) => ms.ToString("+0;-0;0") + " ms";
}
