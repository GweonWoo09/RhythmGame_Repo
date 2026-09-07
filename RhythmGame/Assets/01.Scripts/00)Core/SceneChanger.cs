using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬 전환만을 책임지는 싱글톤 매니저.
/// "이동해도 되는지"(챕터 해금 등)는 GameManager가 판단하고,
/// 이 클래스는 실제 로드/페이드/로딩화면 처리만 담당한다.
/// </summary>
public class SceneChanger : MonoBehaviour
{
    // 싱글톤
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

    // 씬 종류 매핑
    #region Scene Type
    public enum SceneType
    {
        Loading,
        Title,
        Lobby,
        ChapterSelect,
        SongSelect,
        InGame,
        Result,
        CutScene,
        Settings
    }

    /// <summary>SceneType과 실제 빌드 씬 이름 매핑 (Build Settings에 등록된 이름과 일치시킬 것)</summary>
    [Serializable]
    public struct SceneEntry
    {
        public SceneType type;
        public string sceneName;
    }

    [Header("Scene Name 매핑 (Inspector에서 등록)")]
    [SerializeField] private SceneEntry[] sceneEntries;

    public SceneType CurrentScene { get; private set; } = SceneType.Title;
    public SceneType PreviousScene { get; private set; } = SceneType.Title;

    private string GetSceneName(SceneType type)
    {
        foreach (var entry in sceneEntries)
        {
            if (entry.type == type) return entry.sceneName;
        }

        Debug.LogError($"[SceneChanger] '{type}'에 대한 씬 이름이 등록되지 않았습니다.");
        return null;
    }
    #endregion

    #region Scene Payload (씬 간 데이터 전달)
    /// <summary>
    /// 예: 곡 선택 씬 -> 리듬게임 플레이 씬으로 넘길 데이터.
    /// 필요한 정보를 이 컨테이너에 담아 SetPayload로 전달, 다음 씬에서 ConsumePayload로 읽는다.
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

    // 진행도 데이터 로드 및 씬 트랜지션
    #region Load / Transition Events
    /// <summary>로딩 진행률 (0~1)을 UI(로딩바)가 구독</summary>
    public event Action<float> OnLoadProgress;

    /// <summary>씬 로드 시작/완료 시 다른 매니저(사운드, UI 등)가 구독</summary>
    public event Action<SceneType> OnSceneLoadStart;
    public event Action<SceneType> OnSceneLoadComplete;

    [Header("트랜지션 설정")]
    [SerializeField] private float fadeDuration = 0.4f;
    [SerializeField] private CanvasGroup fadeCanvasGroup; // 검은 화면 페이드용 (Inspector에서 연결)

    private bool _isLoading = false;
    #endregion

    // 씬 불러오기 API
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
        string sceneName = GetSceneName(targetScene);
        if (string.IsNullOrEmpty(sceneName)) yield break;

        _isLoading = true;
        OnSceneLoadStart?.Invoke(targetScene);

        // 1. 페이드 아웃 (화면을 검게)
        yield return Fade(0f, 1f);

        // 2. 비동기 로드
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
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

        // 3. 페이드 인 (화면을 다시 밝게)
        yield return Fade(1f, 0f);

        _isLoading = false;
        OnSceneLoadComplete?.Invoke(targetScene);
    }

    private IEnumerator Fade(float from, float to)
    {
        if (fadeCanvasGroup == null) yield break;

        float elapsed = 0f;
        fadeCanvasGroup.blocksRaycasts = true;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeCanvasGroup.alpha = Mathf.Lerp(from, to, elapsed / fadeDuration);
            yield return null;
        }

        fadeCanvasGroup.alpha = to;
        fadeCanvasGroup.blocksRaycasts = (to > 0f);
    }
    #endregion
}