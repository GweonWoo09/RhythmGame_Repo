using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 곡 선택창의 상태(곡 목록, 선택한 곡, 난이도, 탭)와 키 입력만 담당한다.
/// 화면 표시와 미리듣기는 각 View가 이벤트를 받아서 처리한다.
/// </summary>
public class SongSelectController : MonoBehaviour
{
    [SerializeField] private SongLibrarySO library;

    [Header("키 설정")]
    [SerializeField] private KeyCode upKey = KeyCode.UpArrow;
    [SerializeField] private KeyCode downKey = KeyCode.DownArrow;
    [SerializeField] private KeyCode leftKey = KeyCode.LeftArrow;
    [SerializeField] private KeyCode rightKey = KeyCode.RightArrow;
    [SerializeField] private KeyCode startKey = KeyCode.Return;
    [SerializeField] private KeyCode categoryKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode backKey = KeyCode.Escape;

    [Header("뒤로가기(ESC) 했을 때 갈 씬")]
    [SerializeField] private SceneChanger.SceneType backScene = SceneChanger.SceneType.Lobby;

    /// <summary>탭이 바뀌어 곡 목록이 바뀔 때</summary>
    public event Action<IReadOnlyList<SongDataSO>> ListChanged;
    /// <summary>선택한 곡의 목록 위치가 바뀔 때 (곡이 없으면 -1)</summary>
    public event Action<int> SongIndexChanged;
    /// <summary>선택한 곡이나 난이도가 바뀔 때</summary>
    public event Action<SongDataSO, Difficulty> SelectionChanged;
    public event Action<SongCategory> CategoryChanged;
    /// <summary>곡을 시작해서 인게임으로 넘어갈 때</summary>
    public event Action SongStarted;
    /// <summary>곡 선택창을 떠날 때 (곡 시작, 뒤로가기 모두)</summary>
    public event Action Leaving;

    public SongDataSO CurrentSong => songs.Count > 0 ? songs[songIndex] : null;
    public Difficulty CurrentDifficulty { get; private set; } = Difficulty.Normal;
    public SongCategory CurrentCategory { get; private set; } = SongCategory.All;

    private List<SongDataSO> songs = new List<SongDataSO>();
    private int songIndex;
    private bool isLeaving;

    private void Start()
    {
        // 결과창에서 돌아왔을 때 마지막으로 고른 곡과 난이도를 다시 선택
        if (Enum.TryParse(PlaySession.Difficulty, out Difficulty lastDifficulty))
            CurrentDifficulty = lastDifficulty;

        LoadList(CurrentCategory, PlaySession.SongData);
    }

    private void Update()
    {
        if (isLeaving || SettingsPopup.BlocksInput) return;

        if (Input.GetKeyDown(upKey)) MoveSong(-1);
        else if (Input.GetKeyDown(downKey)) MoveSong(1);
        else if (Input.GetKeyDown(leftKey)) ChangeDifficulty(-1);
        else if (Input.GetKeyDown(rightKey)) ChangeDifficulty(1);
        else if (Input.GetKeyDown(categoryKey)) ToggleCategory();
        else if (Input.GetKeyDown(startKey) || Input.GetKeyDown(KeyCode.KeypadEnter)) StartSong();
        else if (Input.GetKeyDown(backKey)) GoBack();
    }

    private void LoadList(SongCategory category, SongDataSO keepSong)
    {
        CurrentCategory = category;
        songs = library != null ? library.GetSongs(category) : new List<SongDataSO>();
        if (library == null) Debug.LogError("[SongSelect] SongLibrary가 연결되지 않았습니다.", this);

        songIndex = Mathf.Max(0, keepSong != null ? songs.IndexOf(keepSong) : 0);

        CategoryChanged?.Invoke(category);
        ListChanged?.Invoke(songs);
        SelectSong(songIndex);
    }

    private void MoveSong(int direction)
    {
        if (songs.Count == 0) return;
        SelectSong(songIndex + direction);
    }

    private void SelectSong(int index)
    {
        if (songs.Count == 0)
        {
            SongIndexChanged?.Invoke(-1);
            SelectionChanged?.Invoke(null, CurrentDifficulty);
            return;
        }

        songIndex = (index + songs.Count) % songs.Count; // 끝에서 처음으로 돌아감
        CurrentDifficulty = FindAvailableDifficulty(CurrentSong, CurrentDifficulty);

        SongIndexChanged?.Invoke(songIndex);
        SelectionChanged?.Invoke(CurrentSong, CurrentDifficulty);
    }

    /// <summary>채보가 있는 다음 난이도로 이동 (없는 난이도는 건너뜀)</summary>
    private void ChangeDifficulty(int direction)
    {
        var song = CurrentSong;
        if (song == null) return;

        int count = Enum.GetValues(typeof(Difficulty)).Length;
        for (int next = (int)CurrentDifficulty + direction; next >= 0 && next < count; next += direction)
        {
            if (!song.HasChart((Difficulty)next)) continue;

            CurrentDifficulty = (Difficulty)next;
            SelectionChanged?.Invoke(song, CurrentDifficulty);
            return;
        }
    }

    /// <summary>원하는 난이도에 채보가 없으면 가장 가까운 난이도를 고른다</summary>
    private static Difficulty FindAvailableDifficulty(SongDataSO song, Difficulty preferred)
    {
        if (song == null || song.HasChart(preferred)) return preferred;

        int count = Enum.GetValues(typeof(Difficulty)).Length;
        for (int distance = 1; distance < count; distance++)
        {
            int lower = (int)preferred - distance;
            int higher = (int)preferred + distance;
            if (lower >= 0 && song.HasChart((Difficulty)lower)) return (Difficulty)lower;
            if (higher < count && song.HasChart((Difficulty)higher)) return (Difficulty)higher;
        }
        return preferred;
    }

    private void ToggleCategory()
    {
        var next = CurrentCategory == SongCategory.All ? SongCategory.Story : SongCategory.All;
        LoadList(next, CurrentSong);
    }

    private void StartSong()
    {
        var song = CurrentSong;
        if (song == null) return;

        var chart = song.GetChart(CurrentDifficulty);
        if (chart == null)
        {
            Debug.LogWarning($"[SongSelect] {song.title}의 {CurrentDifficulty} 채보가 없습니다.", song);
            return;
        }

        if (!HasSceneChanger()) return;

        isLeaving = true;
        PlaySession.SongData = song;
        PlaySession.Difficulty = CurrentDifficulty.ToString();
        SongStarted?.Invoke();
        Leaving?.Invoke();
        PlaySession.Start(chart, SceneChanger.SceneType.SongSelect);
    }

    private void GoBack()
    {
        if (!HasSceneChanger()) return;

        isLeaving = true;
        Leaving?.Invoke();
        SceneChanger.Instance.LoadScene(backScene);
    }

    private bool HasSceneChanger()
    {
        if (SceneChanger.Instance != null) return true;

        Debug.LogError("[SongSelect] SceneChanger가 없습니다. Loading 씬부터 실행해야 씬 이동이 됩니다.", this);
        return false;
    }
}
