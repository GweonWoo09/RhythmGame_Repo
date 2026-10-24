using RhythmGameStarter;
using UnityEngine;

// 인게임 씬이 시작될 때 곡 선택에서 고른 곡을 SongManager의 재생곡으로 지정한다.
public class SongLoader : MonoBehaviour
{
    [SerializeField] private SongManager songManager;

    private void Awake()
    {
        if (PlaySession.Song != null)
        {
            songManager.defaultSong = PlaySession.Song;
        }
    }
}
