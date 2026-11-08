using RhythmGameStarter;
using UnityEngine;

/// <summary>
/// 카탈로그의 곡을 PlaySession(정적 설정)에 넣어 주는 어댑터.
///   PlaySession.Song        : SongItem  (Resources에서 Load<SongItem>으로 읽는다)
///   PlaySession.Difficulty  : string    (기본값 "Normal". 빈 값이면 기존 값을 유지한다)
///   PlaySession.ReturnScene : 연주가 끝나면 돌아갈 씬 (스토리에서 온 연주는 Story)
///
/// PlaySession.Start(song, returnScene)은 곡 객체를 받아 씬을 바로 열므로,
/// 대사 payload(songId, chapterId)를 함께 넘기려면 이 어댑터로 값만 넣고 LoadScene을 직접 호출한다.
/// </summary>
public static class PlaySessionAdapter
{
    /// <param name="returnScene">연주 후 돌아갈 씬. null이면 기존 값을 유지한다.</param>
    public static bool Apply(SongCatalog.Entry entry, string difficulty, SceneChanger.SceneType? returnScene = null)
    {
        if (entry == null || string.IsNullOrEmpty(entry.path))
        {
            Debug.LogError("[PlaySessionAdapter] 곡 정보가 비어 있습니다.");
            return false;
        }

        var song = Resources.Load<SongItem>(entry.path);
        if (song == null)
        {
            Debug.LogError($"[PlaySessionAdapter] 곡 에셋을 찾을 수 없습니다: Resources/{entry.path} (곡 ID '{entry.id}')");
            return false;
        }

        PlaySession.Song = song;

        // 곡 선택창에서 남은 SongData를 지운다. 남아 있으면 ResultSubmitter가 그 곡의 songId로 결과를 보내서
        // StoryFlow가 결과를 무시하고(카탈로그 ID와 불일치) 기록도 엉뚱한 곡에 저장된다.
        // 비워 두면 결과 songId는 곡 에셋 이름(= 카탈로그 ID)이 된다.
        PlaySession.SongData = null;

        if (!string.IsNullOrEmpty(difficulty))
        {
            PlaySession.Difficulty = difficulty;
        }

        if (returnScene.HasValue)
        {
            PlaySession.ReturnScene = returnScene.Value;
        }

        return true;
    }
}