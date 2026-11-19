using RhythmGameStarter;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 결과 화면에 방금 플레이한 곡 정보(커버, 제목, 작곡가, 난이도) 표시만 담당한다.
/// </summary>
public class ResultMusicInfoView : MonoBehaviour
{
    [SerializeField] private Image cover;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text composer;
    [SerializeField] private TMP_Text difficulty;

    private void Start()
    {
        var song = PlaySession.SongData;
        if (song != null) Show(song.cover, song.title, song.composer);
        else if (PlaySession.Song != null) Show(PlaySession.Song.coverArt, PlaySession.Song.name, PlaySession.Song.author); // 곡 정보(SongData) 없이 시작한 경우
        else Show(null, "", "");

        if (difficulty) difficulty.text = PlaySession.Difficulty.ToLower();
    }

    private void Show(Sprite coverSprite, string titleText, string composerText)
    {
        if (cover)
        {
            cover.sprite = coverSprite;
            cover.enabled = coverSprite != null; // 커버가 없으면 그림의 회색 자리를 그대로 둔다
        }
        if (title) title.text = titleText;
        if (composer) composer.text = composerText;
    }
}
