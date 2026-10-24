using RhythmGameStarter;
using UnityEngine;

// 곡이 끝나면 StatsSystem의 기록을 GameManager에 전달하고 결과 씬으로 이동한다.
public class ResultSubmitter : MonoBehaviour
{
    [SerializeField] private StatsSystem stats;

    // SongManager > On Song Finished 이벤트에 연결
    public void Submit()
    {
        var levels = stats.levels.values;

        var result = new GameManager.PlayResult
        {
            songId = PlaySession.Song != null ? PlaySession.Song.name : "Unknown",
            difficulty = PlaySession.Difficulty,
            score = stats.score,
            accuracy = stats.GetAccuracy(),
            maxCombo = stats.maxCombo,
            perfectCount = levels[0].count, // Excellent
            greatCount = levels[1].count,   // Good
            goodCount = levels[2].count,    // Bad
            missCount = stats.missed
        };

        GameManager.Instance.SubmitPlayResult(result);
        SceneChanger.Instance.LoadScene(SceneChanger.SceneType.Result);
    }
}
