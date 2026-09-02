using System;
using System.Collections.Generic;
using UnityEngine;
using static SceneChanger;

/// <summary>
/// 게임 전체의 상태와 진행도를 총괄하는 싱글톤 매니저.
/// - "이동해도 되는지 / 저장할지 / 결과를 어떻게 반영할지"는 GameManager가 담당
/// </summary>
public class GameManager : MonoBehaviour
{
    // 싱글톤
    #region Singleton
    public static GameManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeGameData();
    }
    #endregion

    // 게임 상태 관리
    #region Game State
    public enum GameState
    {
        Title,
        Lobby,
        Playing,
        Paused,
        Result,
        GameOver
    }
 
    public GameState CurrentState { get; private set; } = GameState.Title;
 
    /// <summary>상태가 바뀔 때 UIManager, SoundManager 등이 구독</summary>
    public event Action<GameState, GameState> OnGameStateChanged; // (이전 상태, 새 상태)
 
    public void ChangeState(GameState newState)
    {
        if (CurrentState == newState) return;
 
        GameState prevState = CurrentState;
        CurrentState = newState;
 
        // 일시정지 상태에 따른 시간 흐름 제어
        Time.timeScale = (newState == GameState.Paused) ? 0f : 1f;
 
        OnGameStateChanged?.Invoke(prevState, newState);
    }
    #endregion

    // 챕터 진행도 관리
    #region Progress Data
    [Serializable]
    public class ChapterProgress
    {
        public int chapterId;
        public bool isUnlocked;
        public bool isCleared;
    }

    [Serializable]
    public class SongRecord
    {
        public string songId;
        public string difficulty;      // Easy / Normal / Hard
        public int bestScore;
        public float bestAccuracy;     // 0 ~ 100
        public string bestRank;        // S / A / B / C 등
        public int maxCombo;
    }

    [Serializable]
    public class GameProgressData
    {
        public List<ChapterProgress> chapters = new List<ChapterProgress>();
        public List<SongRecord> songRecords = new List<SongRecord>();
        public int storyFlag = 0; // 어느 대화/컷씬까지 진행했는지
    }

    public GameProgressData ProgressData { get; private set; }

    private void InitializeGameData()
    {
        // 실제 프로젝트에서는 DataManager.Load() 등으로 대체
        ProgressData = new GameProgressData();

        // 예시: 챕터 1은 기본 해금
        ProgressData.chapters.Add(new ChapterProgress
        {
            chapterId = 1,
            isUnlocked = true,
            isCleared = false
        });
        ProgressData.chapters.Add(new ChapterProgress
        {
            chapterId = 2,
            isUnlocked = false,
            isCleared = false
        });
        ProgressData.chapters.Add(new ChapterProgress
        {
            chapterId = 3,
            isUnlocked = false,
            isCleared = false
        });
        ProgressData.chapters.Add(new ChapterProgress
        {
            chapterId = 4,
            isUnlocked = false,
            isCleared = false
        });
    }

    public bool IsChapterUnlocked(int chapterId)
    {
        var chapter = ProgressData.chapters.Find(c => c.chapterId == chapterId);
        return chapter != null && chapter.isUnlocked;
    }

    public void UnlockChapter(int chapterId)
    {
        var chapter = ProgressData.chapters.Find(c => c.chapterId == chapterId);
        if (chapter == null)
        {
            ProgressData.chapters.Add(new ChapterProgress
            {
                chapterId = chapterId,
                isUnlocked = true,
                isCleared = false
            });
        }
        else
        {
            chapter.isUnlocked = true;
        }
    }
    #endregion

    // 결과 화면 핸들링
    #region Play Result Handling
    [Serializable]
    public class PlayResult
    {
        public string songId;
        public string difficulty;
        public int score;
        public float accuracy;
        public int maxCombo;
        public int perfectCount;
        public int greatCount;
        public int goodCount;
        public int missCount;
    }

    public PlayResult LastResult { get; private set; }

    /// <summary>결과창에서 값을 표시할 수 있도록 이벤트로 알림</summary>
    public event Action<PlayResult> OnPlayResultProcessed;

    /// <summary>
    /// 리듬게임 플레이 씬에서 곡이 끝났을 때 호출.
    /// 신기록 갱신, 챕터 해금 판정 등을 이곳에서 처리.
    /// </summary>
    public void SubmitPlayResult(PlayResult result)
    {
        LastResult = result;

        var record = ProgressData.songRecords.Find(
            r => r.songId == result.songId && r.difficulty == result.difficulty);

        if (record == null)
        {
            record = new SongRecord
            {
                songId = result.songId,
                difficulty = result.difficulty
            };
            ProgressData.songRecords.Add(record);
        }

        if (result.score > record.bestScore)
        {
            record.bestScore = result.score;
            record.bestAccuracy = result.accuracy;
            record.maxCombo = result.maxCombo;
            record.bestRank = CalculateRank(result.accuracy);
        }

        // 예시: 특정 곡 클리어 시 다음 챕터 해금
        TryUnlockNextChapter(result);

        SaveGame();

        OnPlayResultProcessed?.Invoke(result);
        ChangeState(GameState.Result);
    }

    private string CalculateRank(float accuracy)
    {
        if (accuracy >= 95f) return "S";
        if (accuracy >= 90f) return "A";
        if (accuracy >= 80f) return "B";
        if (accuracy >= 60f) return "C";
        return "D";
    }

    private void TryUnlockNextChapter(PlayResult result)
    {
        // 프로젝트 규칙에 맞게 챕터-곡 매핑 로직으로 교체
        // 예: "chapter1_song" 클리어 시 chapterId 2 해금
    }
    #endregion

    // 세이브 / 로드
    #region Save / Load
    /// <summary>
    /// 실제 파일 입출력은 DataManager에 위임하고,
    /// GameManager는 "언제 저장할지"만 결정하는 구조를 권장.
    /// </summary>
    public void SaveGame()
    {
        // DataManager.Instance.Save(ProgressData);
    }

    public void LoadGame()
    {
        // ProgressData = DataManager.Instance.Load();
    }
    #endregion

    // 정지 / 재개
    #region Pause / Resume
    public void PauseGame()
    {
        if (CurrentState == GameState.Playing)
        {
            ChangeState(GameState.Paused);
        }
    }

    public void ResumeGame()
    {
        if (CurrentState == GameState.Paused)
        {
            ChangeState(GameState.Playing);
        }
    }
    #endregion
}