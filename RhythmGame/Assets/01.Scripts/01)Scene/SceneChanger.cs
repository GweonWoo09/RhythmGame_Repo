using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬 전환만을 책임지는 싱글톤 매니저. 실제 페이드 연출은 ScreenFader에,
/// BGM 전환은 AudioManager에 위임하고, 이 클래스는 "언제 무엇을 할지" 순서만 조율한다.
/// (이전 이름: GameSceneManager / SceneManager)
/// </summary>
public class SceneChanger : MonoBehaviour
{
    #region Singleton
    public static SceneChanger Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    #endregion

    #region Scene Type
    public enum SceneType
    {
        Loading,
        Title,
        Lobby,
        ChapterSelect,
        SongSelect,
        RhythmPlay,
        Result,
        Story,
        Settings
    }

    /// <summary>SceneType과 실제 빌드 씬 이름, 그리고 그 씬 진입 시 재생할 BGM 매핑</summary>
    [Serializable]
    public struct SceneEntry
    {
        public SceneType type;
        public string sceneName;

        [Tooltip("이 씬에 진입할 때 재생할 BGM. 비워두면 기존 BGM을 그대로 유지한다 (예: 스토리 씬처럼 DialogueDataSO가 BGM을 따로 관리하는 경우).")]
        public AudioClip bgm;
    }

    [Header("Scene 매핑 (Inspector에서 등록)")]
    [SerializeField] private SceneEntry[] sceneEntries;

    public SceneType CurrentScene { get; private set; } = SceneType.Title;
    public SceneType PreviousScene { get; private set; } = SceneType.Title;

    private SceneEntry? FindEntry(SceneType type)
    {
        foreach (var entry in sceneEntries)
        {
            if (entry.type == type) return entry;
        }

        Debug.LogError($"[SceneChanger] '{type}'에 대한 씬 정보가 등록되지 않았습니다.");
        return null;
    }
    #endregion

    #region Scene Payload (씬 간 데이터 전달)
    /// <summary>
    /// 예: 곡 선택 씬 -> 리듬게임 플레이 씬으로 넘길 데이터.
    /// </summary>
    public class ScenePayload
    {
        public string songId;
        public string difficulty;
        public int chapterId;
    }

    private ScenePayload _pendingPayload;

    public void SetPayload(ScenePayload payload)
    {
        _pendingPayload = payload;
    }

    /// <summary>한 번 읽으면 비워짐 (다음 씬 진입 시 1회성 소비)</summary>
    public ScenePayload ConsumePayload()
    {
        var payload = _pendingPayload;
        _pendingPayload = null;
        return payload;
    }
    #endregion

    #region Load / Transition Events
    public event Action<float> OnLoadProgress;
    public event Action<SceneType> OnSceneLoadStart;
    public event Action<SceneType> OnSceneLoadComplete;

    [Header("트랜지션 설정")]
    [SerializeField] private float fadeDuration = 0.4f;

    private bool _isLoading = false;
    #endregion

    #region Public API
    public void LoadScene(SceneType targetScene, ScenePayload payload = null)
    {
        if (_isLoading)
        {
            Debug.LogWarning("[SceneChanger] 이미 씬을 로드하는 중입니다.");
            return;
        }

        if (payload != null)
        {
            SetPayload(payload);
        }

        StartCoroutine(LoadSceneRoutine(targetScene));
    }

    /// <summary>일시정지 등에서 이전 씬으로 돌아갈 때 사용</summary>
    public void LoadPreviousScene()
    {
        LoadScene(PreviousScene);
    }
    #endregion

    #region Load Routine
    private IEnumerator LoadSceneRoutine(SceneType targetScene)
    {
        var entry = FindEntry(targetScene);
        if (entry == null) yield break;

        _isLoading = true;
        OnSceneLoadStart?.Invoke(targetScene);

        // 1. 화면을 가린다 (ScreenFader에 위임)
        if (ScreenFader.Instance != null)
        {
            yield return ScreenFader.Instance.FadeOut(fadeDuration);
        }

        // 2. 비동기 씬 로드
        AsyncOperation op = SceneManager.LoadSceneAsync(entry.Value.sceneName);
        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
        {
            OnLoadProgress?.Invoke(op.progress / 0.9f);
            yield return null;
        }

        OnLoadProgress?.Invoke(1f);
        op.allowSceneActivation = true;

        yield return new WaitUntil(() => op.isDone);

        PreviousScene = CurrentScene;
        CurrentScene = targetScene;

        // 3. 화면이 가려진 동안 BGM 교체 (화면이 검은 상태라 끊김이 티 나지 않는다)
        if (entry.Value.bgm != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBGM(entry.Value.bgm);
        }

        // 4. 화면을 다시 걷어낸다
        if (ScreenFader.Instance != null)
        {
            yield return ScreenFader.Instance.FadeIn(fadeDuration);
        }

        _isLoading = false;
        OnSceneLoadComplete?.Invoke(targetScene);
    }
    #endregion
}