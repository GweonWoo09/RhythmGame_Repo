using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬 전환만을 책임지는 싱글톤 매니저. 실제 페이드 연출은 ScreenFader에,
/// BGM 전환은 AudioManager에 위임하고, 이 클래스는 "언제 무엇을 할지" 순서만 조율한다.
///
/// [전환 이벤트 정책]
///  - 요청이 검증 단계(등록/빌드 설정)에서 거부되면: OnSceneLoadFailed만 발행 (Start/Complete는 발행되지 않음).
///  - 요청이 수락되면: OnSceneLoadStart 발행 후, 반드시 Complete 또는 Failed 중 정확히 하나가 뒤따른다.
///      · 새 씬이 활성화되기 전에 실패 -> OnSceneLoadFailed (씬/상태는 그대로 유지)
///      · 새 씬이 활성화된 뒤에는      -> OnSceneLoadComplete (BGM 오류 등 부가 실패는 로그만 남김)
///  - 이미 로딩 중이라 무시된 요청은 이벤트 없이 false만 반환한다 (진행 중인 로드가 자기 이벤트를 발행하므로).
///
/// [이전 씬 정책]
///  - HasPreviousScene이 true일 때만 LoadPreviousScene()이 동작한다.
///  - 시작 전용 씬(Boot/Loading 등)은 sceneEntries에 등록하지 않거나, 등록하더라도 excludeFromHistory를 체크하면
///    그 씬을 "돌아갈 대상"으로 기록하지 않는다. 따라서 첫 전환 직후 뒤로가기가 부트 씬으로 되돌아가지 않는다.
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

        InitializeCurrentScene();
    }
    #endregion

    // 씬 매핑
    #region Scene Type
    public enum SceneType
    {
        Loading,
        Lobby,
        ChapterSelect,
        SongSelect,
        RhythmPlay,
        Result,
        Story,
        Settings
        // 주의: 값을 중간에 끼워 넣으면 이미 직렬화된 인스펙터 값이 밀린다. 새 항목은 항상 맨 뒤에 추가할 것.
    }

    /// <summary>SceneType과 실제 빌드 씬 이름, 그리고 그 씬 진입 시 재생할 BGM 매핑</summary>
    [Serializable]
    public struct SceneEntry
    {
        public SceneType type;
        public string sceneName;

        [Tooltip("이 씬에 진입할 때 재생할 BGM. 비워두면 기존 BGM을 그대로 유지한다 (예: 스토리 씬처럼 DialogueDataSO가 BGM을 따로 관리하는 경우).")]
        public AudioClip bgm;

        [Tooltip("체크하면 이 씬에서 다른 씬으로 떠날 때 이 씬을 '이전 씬'으로 기록하지 않는다. (시작 전용 Boot/Loading 씬용)")]
        public bool excludeFromHistory;
    }

    [Header("Scene 매핑 (Inspector에서 등록)")]
    [SerializeField] private SceneEntry[] sceneEntries;

    /// <summary>현재 씬. IsCurrentSceneRegistered가 false일 때는 의미 없는 값(기본값)이다.</summary>
    public SceneType CurrentScene { get; private set; }

    /// <summary>현재 활성 씬이 sceneEntries에 등록된 씬인지 (등록되지 않은 부트 씬에서 시작하면 false)</summary>
    public bool IsCurrentSceneRegistered { get; private set; }

    /// <summary>직전 씬. HasPreviousScene이 true일 때만 유효하다.</summary>
    public SceneType PreviousScene { get; private set; }

    /// <summary>돌아갈 수 있는 유효한 이전 씬이 있는지</summary>
    public bool HasPreviousScene { get; private set; }

    private bool _currentExcludedFromHistory;

    private bool TryGetEntry(SceneType type, out SceneEntry result)
    {
        if (sceneEntries != null)
        {
            foreach (var entry in sceneEntries)
            {
                if (entry.type == type)
                {
                    result = entry;
                    return true;
                }
            }
        }

        result = default;
        return false;
    }

    private bool TryGetEntryBySceneName(string activeSceneName, out SceneEntry result)
    {
        if (sceneEntries != null)
        {
            foreach (var entry in sceneEntries)
            {
                if (SceneNameMatches(entry.sceneName, activeSceneName))
                {
                    result = entry;
                    return true;
                }
            }
        }

        result = default;
        return false;
    }

    /// <summary>등록된 이름이 경로("Assets/Scenes/Lobby.unity")로 적혀 있어도 씬 이름으로 비교할 수 있게 한다.</summary>
    private static bool SceneNameMatches(string registeredName, string activeSceneName)
    {
        if (string.IsNullOrWhiteSpace(registeredName)) return false;

        return string.Equals(registeredName, activeSceneName, StringComparison.Ordinal)
            || string.Equals(Path.GetFileNameWithoutExtension(registeredName), activeSceneName, StringComparison.Ordinal);
    }

    /// <summary>
    /// 시작 시점의 실제 활성 씬을 등록된 매핑과 대조해 CurrentScene을 초기화한다.
    /// 등록되지 않은 씬(부트/로딩 전용)에서 시작했다면 현재 씬은 "미등록"으로 두고, 이전 씬 기록도 비워둔다.
    /// </summary>
    private void InitializeCurrentScene()
    {
        string activeName = SceneManager.GetActiveScene().name;

        if (TryGetEntryBySceneName(activeName, out var entry))
        {
            CurrentScene = entry.type;
            IsCurrentSceneRegistered = true;
            _currentExcludedFromHistory = entry.excludeFromHistory;
        }
        else
        {
            IsCurrentSceneRegistered = false;
            _currentExcludedFromHistory = true;
            Debug.LogWarning($"[SceneChanger] 시작 씬 '{activeName}'은(는) sceneEntries에 등록되어 있지 않습니다. " +
                             "시작 전용 씬으로 간주하며, 첫 전환 이후에도 이 씬으로는 돌아가지 않습니다.");
        }

        HasPreviousScene = false;
    }

    /// <summary>씬 활성화가 끝난 시점에 Current/Previous 상태를 한 번에 갱신한다.</summary>
    private void CommitSceneChange(SceneType target, SceneEntry targetEntry)
    {
        bool leavingToDifferentScene = !IsCurrentSceneRegistered || CurrentScene != target;

        // 떠나는 씬이 등록돼 있고, 기록 제외 대상이 아니며, 같은 씬 재로드가 아닐 때만 "이전 씬"으로 남긴다.
        if (IsCurrentSceneRegistered && !_currentExcludedFromHistory && leavingToDifferentScene)
        {
            PreviousScene = CurrentScene;
            HasPreviousScene = true;
        }

        CurrentScene = target;
        IsCurrentSceneRegistered = true;
        _currentExcludedFromHistory = targetEntry.excludeFromHistory;
    }
    #endregion

    // 씬 간 데이터 전달
    #region Scene Payload
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

    // 씬 로드/전환할 때 이벤트
    #region Load / Transition Events
    public event Action<float> OnLoadProgress;
    public event Action<SceneType> OnSceneLoadStart;
    public event Action<SceneType> OnSceneLoadComplete;

    /// <summary>씬 전환 실패. (시도한 씬, 실패 사유). 정책은 클래스 상단 주석 참고.</summary>
    public event Action<SceneType, string> OnSceneLoadFailed;

    [Header("트랜지션 설정")]
    [SerializeField] private float fadeDuration = 0.4f;

    private bool _isLoading = false;
    #endregion

    // API
    #region Public API
    /// <summary>
    /// 씬 전환을 요청한다.
    /// 반환값: 전환이 실제로 시작되면 true. 로딩 중이거나 검증에 실패해 거부되면 false.
    /// (검증 실패 시에는 페이드아웃 전에 거부되며, OnSceneLoadFailed가 발행된다.)
    /// </summary>
    public bool LoadScene(SceneType targetScene, ScenePayload payload = null)
    {
        if (_isLoading)
        {
            Debug.LogWarning("[SceneChanger] 이미 씬을 로드하는 중입니다.");
            return false;
        }

        if (!TryValidateTarget(targetScene, out var entry, out string reason))
        {
            Debug.LogError($"[SceneChanger] '{targetScene}' 전환 거부: {reason}");
            OnSceneLoadFailed?.Invoke(targetScene, reason);
            return false;
        }

        // 검증을 통과한 뒤에만 payload를 저장한다 (거부된 요청이 payload를 남기지 않도록).
        bool payloadFromThisCall = payload != null;
        if (payloadFromThisCall)
        {
            SetPayload(payload);
        }

        StartCoroutine(LoadSceneRoutine(targetScene, entry, payloadFromThisCall));
        return true;
    }

    /// <summary>
    /// 일시정지 등에서 이전 씬으로 돌아갈 때 사용. 유효한 이전 씬이 없으면 아무 것도 하지 않고 false를 반환한다.
    /// </summary>
    public bool LoadPreviousScene()
    {
        if (!HasPreviousScene)
        {
            Debug.LogWarning("[SceneChanger] 돌아갈 이전 씬이 없습니다.");
            return false;
        }

        return LoadScene(PreviousScene);
    }
    #endregion

    // 예외처리
    #region Validation
    private bool TryValidateTarget(SceneType targetScene, out SceneEntry entry, out string reason)
    {
        if (!TryGetEntry(targetScene, out entry))
        {
            reason = $"'{targetScene}'에 대한 씬 정보가 sceneEntries에 등록되지 않았습니다.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(entry.sceneName))
        {
            reason = $"'{targetScene}'의 sceneName이 비어 있습니다.";
            return false;
        }

        if (!Application.CanStreamedLevelBeLoaded(entry.sceneName))
        {
            reason = $"씬 '{entry.sceneName}'을(를) 로드할 수 없습니다. Build Settings 등록 여부와 이름(오타)을 확인하세요.";
            return false;
        }

        reason = null;
        return true;
    }
    #endregion

    // 씬 불러올 때 루틴
    #region Load Routine
    private IEnumerator LoadSceneRoutine(SceneType targetScene, SceneEntry entry, bool payloadFromThisCall)
    {
        _isLoading = true;

        bool sceneActivated = false; // 새 씬 활성화까지 끝났는가 (이후 실패는 '전환 실패'로 보지 않는다)
        bool fadedOut = false;       // 화면을 가린 상태가 남아 있는가
        string failReason = null;

        try
        {
            OnSceneLoadStart?.Invoke(targetScene);

            // 1. 화면을 가린다 (ScreenFader에 위임)
            if (ScreenFader.Instance != null)
            {
                fadedOut = true; // 페이드 도중 중단돼도 복구 대상이 되도록 먼저 표시
                yield return ScreenFader.Instance.FadeOut(fadeDuration);
            }

            // 2. 비동기 씬 로드
            AsyncOperation op = SceneManager.LoadSceneAsync(entry.sceneName);
            if (op == null)
            {
                failReason = $"LoadSceneAsync('{entry.sceneName}')가 null을 반환했습니다.";
                yield break; // finally에서 정리
            }

            op.allowSceneActivation = false;

            while (op.progress < 0.9f)
            {
                OnLoadProgress?.Invoke(op.progress / 0.9f);
                yield return null;
            }

            OnLoadProgress?.Invoke(1f);
            op.allowSceneActivation = true;

            yield return new WaitUntil(() => op.isDone);

            sceneActivated = true;
            CommitSceneChange(targetScene, entry);

            // 3. 화면이 가려진 동안 BGM 교체 (BGM 오류가 전환 전체를 막지 않도록 격리)
            TryPlayBgm(entry.bgm);

            // 4. 화면을 다시 걷어낸다
            if (ScreenFader.Instance != null)
            {
                yield return ScreenFader.Instance.FadeIn(fadeDuration);
            }

            fadedOut = false;
        }
        finally
        {
            // 정상 종료, yield break, 예외, 코루틴 중단 모두 이 경로를 지난다.
            _isLoading = false;

            // 가려진 채로 남았다면 즉시 걷어낸다 (finally 안에서는 yield를 쓸 수 없어 즉시 해제)
            if (fadedOut && ScreenFader.Instance != null)
            {
                ScreenFader.Instance.ClearImmediate();
            }

            if (sceneActivated)
            {
                OnSceneLoadComplete?.Invoke(targetScene);
            }
            else
            {
                if (payloadFromThisCall) _pendingPayload = null; // 실패한 전환의 payload가 다음 전환에 새어 나가지 않게 한다.

                if (string.IsNullOrEmpty(failReason))
                {
                    failReason = "전환 중 예외가 발생했거나 전환이 중단되었습니다. (콘솔의 예외 로그 참고)";
                }

                Debug.LogError($"[SceneChanger] '{targetScene}' 전환 실패: {failReason}");
                OnSceneLoadFailed?.Invoke(targetScene, failReason);
            }
        }
    }

    private void TryPlayBgm(AudioClip clip)
    {
        if (clip == null || AudioManager.Instance == null) return;

        try
        {
            AudioManager.Instance.PlayBGM(clip);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SceneChanger] BGM 재생 중 오류 (씬 전환은 계속 진행): {e}");
        }
    }
    #endregion
}