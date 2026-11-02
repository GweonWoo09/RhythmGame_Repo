using System.Collections.Generic;
using RhythmGameStarter;
using TMPro;
using UnityEngine;

// 곡 선택 화면: ↑↓로 곡을 고르고 Enter로 시작한다.
public class SongSelectController : MonoBehaviour
{
    [SerializeField] private List<SongItem> songs;
    [SerializeField] private TMP_Text number;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text composer;
    [SerializeField] private TMP_Text bpm;

    private int index;

    private void Start()
    {
        Refresh();
    }

    private void Update()
    {
        if (songs.Count == 0 || SettingsPopup.BlocksInput) return;

        if (Input.GetKeyDown(KeyCode.UpArrow)) Move(-1);
        else if (Input.GetKeyDown(KeyCode.DownArrow)) Move(1);
        else if (Input.GetKeyDown(KeyCode.Return)) StartSong();
    }

    private void Move(int step)
    {
        index = (index + step + songs.Count) % songs.Count;
        Refresh();
    }

    private void StartSong()
    {
        PlaySession.Start(songs[index], SceneChanger.SceneType.SongSelect);
    }

    private void Refresh()
    {
        if (songs.Count == 0) return;

        var song = songs[index];
        number.text = (index + 1).ToString("00");
        title.text = song.name;
        composer.text = song.author;
        bpm.text = song.bpm.ToString();
    }
}
