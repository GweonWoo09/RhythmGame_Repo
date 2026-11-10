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

    /// <summary>
    /// 스토리 대사의 play:곡ID가 이 곡을 가리키는가.
    /// 곡 ID(songId), 이 에셋 이름, 난이도별 채보(SongItem) 에셋 이름 중 하나와 같으면 같은 곡으로 본다. (대소문자 무시)
    /// </summary>
    public bool MatchesId(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        return Same(songId, id) || Same(name, id)
            || (easy != null && Same(easy.name, id))
            || (normal != null && Same(normal.name, id))
            || (hard != null && Same(hard.name, id));
    }

    // 시트의 곡 ID는 밑줄(Merry_Deception_124_Normal), 에셋 이름은 공백(Merry Deception_124_Normal)을 쓰는 경우가 있어
    // 공백, 밑줄, 하이픈을 무시하고 비교한다
    private static bool Same(string a, string b) =>
        a != null && string.Equals(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string s) => s.Replace(" ", "").Replace("_", "").Replace("-", "");

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
