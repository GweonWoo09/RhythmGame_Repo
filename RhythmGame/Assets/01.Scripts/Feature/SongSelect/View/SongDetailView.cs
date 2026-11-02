using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 선택한 곡의 상세 정보(번호, 제목, 작곡가, BPM, 설명, 최고 기록) 표시만 담당한다.
/// </summary>
public class SongDetailView : MonoBehaviour
{
    [SerializeField] private SongSelectController controller;

    [Header("표시할 곳 (없는 항목은 비워둠)")]
    [SerializeField] private TMP_Text numberText;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text composerText;
    [SerializeField] private TMP_Text bpmText;
    [SerializeField] private TMP_Text flavorText;
    [SerializeField] private TMP_Text highScoreText;
    [SerializeField] private TMP_Text highRankText;
    [SerializeField] private Image coverImage;

    [Header("표시 형식 ({0} 자리에 값이 들어감)")]
    [SerializeField] private string numberFormat = "No.{0:00}";
    [SerializeField] private string composerFormat = "{0}";
    [SerializeField] private string bpmFormat = "BPM: {0}";
    [SerializeField] private string highScoreFormat = "최고점수: {0}";
    [SerializeField] private string highRankFormat = "최고판정: {0}";
    [SerializeField] private string noRecordText = "-";

    private void OnEnable()
    {
        controller.SongIndexChanged += OnSongIndexChanged;
        controller.SelectionChanged += OnSelectionChanged;
    }

    private void OnDisable()
    {
        controller.SongIndexChanged -= OnSongIndexChanged;
        controller.SelectionChanged -= OnSelectionChanged;
    }

    private void OnSongIndexChanged(int index)
    {
        SetText(numberText, numberFormat, index >= 0 ? (object)(index + 1) : noRecordText);
    }

    private void OnSelectionChanged(SongDataSO song, Difficulty difficulty)
    {
        if (song == null)
        {
            SetText(titleText, "{0}", "");
            SetText(composerText, "{0}", "");
            SetText(bpmText, "{0}", "");
            SetText(flavorText, "{0}", "");
            SetText(highScoreText, highScoreFormat, noRecordText);
            SetText(highRankText, highRankFormat, noRecordText);
            if (coverImage) coverImage.enabled = false;
            return;
        }

        SetText(titleText, "{0}", song.title);
        SetText(composerText, composerFormat, song.composer);
        SetText(bpmText, bpmFormat, song.Bpm);
        SetText(flavorText, "{0}", song.flavorText);
        if (coverImage)
        {
            coverImage.sprite = song.cover;
            coverImage.enabled = song.cover != null;
        }

        var record = FindRecord(song.songId, difficulty);
        bool hasRecord = record != null;
        SetText(highScoreText, highScoreFormat, hasRecord ? record.bestScore.ToString("N0") : noRecordText);
        SetText(highRankText, highRankFormat,
            hasRecord && !string.IsNullOrEmpty(record.bestRank) ? record.bestRank : noRecordText);
    }

    /// <summary>GameManager에 저장된 이 곡·난이도의 최고 기록 (없으면 null)</summary>
    private static GameManager.SongRecord FindRecord(string songId, Difficulty difficulty)
    {
        var gameManager = GameManager.Instance;
        if (gameManager == null || gameManager.ProgressData == null) return null;

        string difficultyName = difficulty.ToString();
        return gameManager.ProgressData.songRecords.Find(
            r => r.songId == songId && r.difficulty == difficultyName);
    }

    private static void SetText(TMP_Text target, string format, object value)
    {
        if (target) target.text = string.Format(format, value);
    }
}
