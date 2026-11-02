using System;
using RhythmGameStarter;
using UnityEngine;

/// <summary>
/// 곡 하나의 정보(화면 표시 정보 + 난이도별 채보)만 담는 데이터.
/// 채보는 시퀀서에서 만든 SongItem을 연결한다.
/// </summary>
[CreateAssetMenu(fileName = "SongData", menuName = "Rhythm/Song Data")]
public class SongDataSO : ScriptableObject
{
    [Header("기본 정보")]
    [Tooltip("기록 저장에 쓰는 고유 ID. 한 번 정하면 바꾸지 않는다")]
    public string songId;
    public string title;
    public string composer;
    [Tooltip("0이면 채보(SongItem)의 BPM을 표시")]
    public int bpm;
    [TextArea(2, 4)] public string flavorText;
    public Sprite cover;

    [Header("분류")]
    [Tooltip("story 탭에도 표시할 곡")]
    public bool isStorySong;

    [Header("미리듣기 (비우면 채보의 음악 사용)")]
    public AudioClip previewClip;

    [Header("난이도별 채보 (비우면 그 난이도는 선택 불가)")]
    public SongItem easy;
    public SongItem normal;
    public SongItem hard;

    public SongItem GetChart(Difficulty difficulty)
    {
        switch (difficulty)
        {
            case Difficulty.Easy: return easy;
            case Difficulty.Normal: return normal;
            default: return hard;
        }
    }

    public bool HasChart(Difficulty difficulty) => GetChart(difficulty) != null;

    /// <summary>표시용 BPM. 직접 입력한 값이 없으면 첫 번째 채보의 BPM</summary>
    public int Bpm
    {
        get
        {
            if (bpm > 0) return bpm;
            var chart = FirstChart();
            return chart != null ? chart.bpm : 0;
        }
    }

    public AudioClip GetPreviewClip()
    {
        if (previewClip != null) return previewClip;
        var chart = FirstChart();
        return chart != null ? chart.clip : null;
    }

    private SongItem FirstChart()
    {
        foreach (Difficulty difficulty in Enum.GetValues(typeof(Difficulty)))
        {
            var chart = GetChart(difficulty);
            if (chart != null) return chart;
        }
        return null;
    }
}
