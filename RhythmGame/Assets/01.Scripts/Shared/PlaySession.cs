using RhythmGameStarter;

/*
name inchang
system 곡 배속 난이도 돌아갈곳을 담는 클래스
How to use 
- PlaySession.Start(선택한곡, SceneType.SongSelect);
- PlaySession.Start(챕터곡, SceneType.Story);
 day 26-10-16
 */
public static class PlaySession
{
    public static SongItem Song;
    public static string Difficulty = "Normal";
    public static float Speed = 1f;
    public static SceneChanger.SceneType ReturnScene = SceneChanger.SceneType.SongSelect;

    public static void Start(SongItem song, SceneChanger.SceneType returnScene)
    {
        Song = song;
        ReturnScene = returnScene;
        SceneChanger.Instance.LoadScene(SceneChanger.SceneType.RhythmPlay);
    }

}