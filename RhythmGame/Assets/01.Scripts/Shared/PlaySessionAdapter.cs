using RhythmGameStarter;
using UnityEngine;

/// <summary>
/// 카탈로그의 곡을 리듬게임 씬이 읽는 PlaySession(정적 설정)에 넣어 주는 어댑터.
/// StoryFlow가 연주를 시작하기 직전에 호출한다. (씬이 바뀌어도 정적 값은 유지된다)
///
/// [확인 필요] PlaySession.cs를 아직 보지 못했다. ResultSubmitter에서 확인한 것은 다음 두 가지뿐이다.
///   - PlaySession.Song        : 곡 에셋. 이름(.name)을 읽는다.
///   - PlaySession.Difficulty  : 난이도. 문자열로 쓰인다.
/// 아래 대입이 프로젝트의 실제 타입과 다르면 컴파일 오류가 나는데, 이 파일의 Apply 안만 고치면 된다.
/// (곡 에셋 타입을 SongItem으로 가정했다. 다른 타입이면 Load<>의 타입 인자만 바꾼다.)
/// </summary>
public static class PlaySessionAdapter
{
    public static bool Apply(SongCatalog.Entry entry, string difficulty)
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

        // 난이도가 비어 있으면 PlaySession의 기존 값을 그대로 둔다
        if (!string.IsNullOrEmpty(difficulty))
        {
            PlaySession.Difficulty = difficulty;
        }

        return true;
    }
}
