using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 이야기 진행 순서(스토리 -> 연주 -> 스토리 -> 분기 -> 엔딩)를 끌고 가는 정적 컨트롤러.
///
/// 하는 일
///   - 흐름 데이터(StoryFlowData)의 "지금 칸"을 기억하고, 칸의 종류에 맞는 씬으로 이동시킨다.
///       Story : SceneType.Story 로 이동 (대화 재생은 FlowStoryEntry가 맡는다)
///       Play  : SceneType.RhythmPlay 로 이동 (ScenePayload에 songId / difficulty / chapterId)
///       Route : 조건만 따져서 바로 다음 칸으로
///       End   : 엔딩을 기록하고 흐름을 끝낸다
///   - 곡이 끝나면(GameManager.OnPlayResultProcessed) 결과 화면(선택) -> 다음 곡 또는 다음 칸으로 진행한다.
///   - 현재 위치와 도달한 엔딩을 PlayerPrefs에 저장해서 "이어하기"를 지원한다.
///
/// 호출 방향은 기존 구조와 같다: UI/트리거(FlowStoryEntry, 버튼) -> StoryFlow -> SceneChanger/GameManager.
///
/// 칸이 끝났을 때 순서: setFlags -> clearChapter/unlockChapter -> branches(처음 참인 것) -> next
///
/// 곡 결과는 조건식에서 쓸 수 있도록 스토리 플래그로 남긴다 (곡이 끝날 때마다 덮어쓴다):
///   play_accuracy(정확도 정수), play_score, play_combo, play_miss
///   예) 분기 조건 "play_accuracy>=95"
///
/// 대사 안의 play 명령(연주하러 나갔다 같은 대사 위치로 돌아오기): PlayInline / TryTakeInline 참고.
///   Story 칸의 대화 도중 "play:곡ID" 줄을 넘기면 -> 연주 -> 결과 화면 -> 그 대사 다음 줄부터 이어진다.
///   연주를 마치지 못하고 나왔다가 같은 이야기에 다시 들어오면 play 줄부터 다시 보여준다.
///   곡 ID -> 곡 에셋 연결은 SongCatalog(시트의 songs 탭) + PlaySessionAdapter가 맡는다.
///
/// 필요한 외부 수정: SceneChanger.SceneType 에 Story 추가 + sceneEntries 등록, GameManager.ClearChapter 추가.
/// </summary>
public static class StoryFlow
{
    public const string DefaultFlowId = "main";

    /// <summary>엔딩 칸에 scene이 없거나, 마지막 칸에 next가 없어 흐름이 끝났을 때 이동할 씬</summary>
    public static SceneChanger.SceneType DefaultEndScene = SceneChanger.SceneType.Lobby;

    private const string ProgressKey = "StoryFlow.Progress";
    private const int MaxRouteChain = 64;

    #region State
    public static bool IsRunning { get; private set; }
    public static StoryFlowData Data { get; private set; }
    public static StoryFlowNode CurrentNode { get; private set; }

    /// <summary>Play 칸에서 지금 연주할(또는 방금 끝난 다음) 곡의 순번</summary>
    public static int SongIndex { get; private set; }

    /// <summary>지금 지나가고 있는 챕터 번호 (chapter 표시가 있는 칸을 지날 때 바뀐다)</summary>
    public static int CurrentChapter { get; private set; }

    /// <summary>칸에 진입할 때마다 발행 (UI 갱신, 디버그용)</summary>
    public static event Action<StoryFlowNode> OnNodeEntered;

    /// <summary>엔딩 칸에 도달했을 때 (엔딩 ID)</summary>
    public static event Action<string> OnEnding;

    private static int _lastAdvanceFrame = -1;
    private static int _routeChain = 0;

    /// <summary>
    /// 이번 프레임에 흐름이 이미 다음 칸으로 넘어갔는가.
    /// SceneTransitionDialogueEnd가 같은 프레임에 이중으로 씬을 이동하지 않도록 확인할 때 쓴다.
    /// </summary>
    public static bool JustAdvanced
    {
        get { return _lastAdvanceFrame == Time.frameCount; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        IsRunning = false;
        Data = null;
        CurrentNode = null;
        SongIndex = 0;
        CurrentChapter = 0;
        _lastAdvanceFrame = -1;
        _routeChain = 0;
        _inline = null;
        _inlineSongFinished = false;
    }
    #endregion

    #region Start / Resume
    /// <summary>
    /// 흐름을 시작한다. nodeId를 생략하면 흐름의 시작 칸부터. (새 게임 / 튜토리얼부터)
    /// songIndex, chapter는 이어하기에서 위치를 복원할 때 쓴다.
    /// </summary>
    public static bool StartFlow(string flowId = DefaultFlowId, string nodeId = null, int songIndex = 0, int chapter = 0)
    {
        StoryFlowData data = StoryFlowLoader.Load(flowId);
        if (data == null) return false;

        StoryFlowNode node = data.Find(string.IsNullOrEmpty(nodeId) ? data.startNodeId : nodeId);
        if (node == null)
        {
            Debug.LogError($"[StoryFlow] 흐름 '{flowId}'에 칸 '{nodeId}'가 없습니다.");
            return false;
        }

        StoryFlowRunner.EnsureExists();

        _inline = null;
        _inlineSongFinished = false;

        Data = data;
        IsRunning = true;
        CurrentChapter = chapter;

        EnterNode(node, songIndex);
        return true;
    }

    /// <summary>챕터 선택 화면에서: 해당 챕터의 시작 칸부터 흐름을 시작한다.</summary>
    public static bool StartChapter(int chapterId, string flowId = DefaultFlowId, bool requireUnlocked = true)
    {
        if (requireUnlocked && GameManager.Instance != null && !GameManager.Instance.IsChapterUnlocked(chapterId))
        {
            Debug.LogWarning($"[StoryFlow] 챕터 {chapterId}는 아직 해금되지 않았습니다.");
            return false;
        }

        StoryFlowData data = StoryFlowLoader.Load(flowId);
        if (data == null) return false;

        StoryFlowNode start = data.FindChapterStart(chapterId);
        if (start == null)
        {
            Debug.LogError($"[StoryFlow] 흐름 '{flowId}'에 챕터 {chapterId}의 시작 칸(chapter {chapterId})이 없습니다.");
            return false;
        }

        return StartFlow(flowId, start.id, 0, chapterId);
    }

    /// <summary>저장된 위치가 있는가 ("이어하기" 버튼 활성화 판단용)</summary>
    public static bool HasSavedProgress
    {
        get
        {
            Progress progress = LoadProgress();
            return !string.IsNullOrEmpty(progress.flowId) && !string.IsNullOrEmpty(progress.nodeId);
        }
    }

    /// <summary>저장된 위치에서 이어서 시작한다. 저장된 위치가 없으면 false.</summary>
    public static bool ResumeSaved()
    {
        Progress progress = LoadProgress();
        if (string.IsNullOrEmpty(progress.flowId) || string.IsNullOrEmpty(progress.nodeId)) return false;

        StoryFlowData data = StoryFlowLoader.Load(progress.flowId);
        if (data == null) return false;

        if (data.Find(progress.nodeId) != null)
        {
            return StartFlow(progress.flowId, progress.nodeId, progress.songIndex, progress.chapter);
        }

        // 시트가 바뀌어 저장된 칸이 사라진 경우: 저장된 챕터의 시작점으로, 그것도 없으면 처음부터
        Debug.LogWarning($"[StoryFlow] 저장된 칸 '{progress.nodeId}'이(가) 흐름 '{progress.flowId}'에 더 이상 없습니다. 챕터 시작점에서 다시 시작합니다.");

        StoryFlowNode chapterStart = progress.chapter > 0 ? data.FindChapterStart(progress.chapter) : null;
        return chapterStart != null
            ? StartFlow(progress.flowId, chapterStart.id, 0, progress.chapter)
            : StartFlow(progress.flowId);
    }

    /// <summary>흐름을 멈춘다 (저장된 위치는 그대로 둔다)</summary>
    public static void Stop()
    {
        IsRunning = false;
        CurrentNode = null;
        Data = null;
        SongIndex = 0;
        _routeChain = 0;
    }
    #endregion

    #region Completion callbacks
    /// <summary>Story 칸의 대화가 끝났을 때 (FlowStoryEntry가 OnDialogEnd에서 호출)</summary>
    public static void CompleteStory()
    {
        if (!IsRunning || CurrentNode == null || CurrentNode.type != StoryFlowNodeType.Story) return;

        AdvanceFrom(CurrentNode);
    }

    /// <summary>결과 화면의 "계속" 버튼에서 호출. 다음 곡이 남았으면 그 곡으로, 아니면 다음 칸으로.</summary>
    public static void ContinueAfterResult()
    {
        // 대사 도중 play 명령으로 나온 연주라면: 그 이야기(Story 씬)로 돌아간다
        if (_inline != null)
        {
            LoadScene(SceneChanger.SceneType.Story);
            return;
        }

        if (!IsRunning || CurrentNode == null || CurrentNode.type != StoryFlowNodeType.Play) return;

        if (CurrentNode.songs != null && SongIndex < CurrentNode.songs.Length)
        {
            StartSong();
            return;
        }

        AdvanceFrom(CurrentNode);
    }

    /// <summary>곡 결과가 처리됐을 때 (StoryFlowRunner가 GameManager.OnPlayResultProcessed에서 호출)</summary>
    internal static void HandlePlayResult(GameManager.PlayResult result)
    {
        // 대사 도중 play 명령으로 시작한 연주의 결과
        if (_inline != null)
        {
            HandleInlineResult(result);
            return;
        }

        if (!IsRunning || CurrentNode == null || CurrentNode.type != StoryFlowNodeType.Play) return;
        if (CurrentNode.songs == null || SongIndex >= CurrentNode.songs.Length) return;

        string expected = CurrentNode.songs[SongIndex];

        // 흐름과 상관없는 곡(자유 플레이 등)의 결과로 흐름이 넘어가지 않게 한다
        if (result != null && !string.IsNullOrEmpty(result.songId)
            && !string.Equals(result.songId, expected, StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning($"[StoryFlow] 기대한 곡은 '{expected}'인데 '{result.songId}'의 결과가 들어와 무시합니다.");
            return;
        }

        if (result != null)
        {
            DialogueExpression.SetFlag("play_accuracy", (int)result.accuracy);
            DialogueExpression.SetFlag("play_score", result.score);
            DialogueExpression.SetFlag("play_combo", result.maxCombo);
            DialogueExpression.SetFlag("play_miss", result.missCount);
        }

        SongIndex++;
        SaveProgress();
        _lastAdvanceFrame = Time.frameCount;

        if (CurrentNode.showResult)
        {
            LoadScene(SceneChanger.SceneType.Result);
        }
        else
        {
            ContinueAfterResult();
        }
    }
    #endregion

    #region Inline play (대사 안의 play 명령)
    private static DialoguePlaySnapshot _inline;
    private static bool _inlineSongFinished;

    /// <summary>대사 도중 연주하러 나가 있는(아직 돌아가지 않은) 상태인가 (결과 화면의 계속 버튼 판단용)</summary>
    public static bool HasInlinePending
    {
        get { return _inline != null; }
    }

    /// <summary>
    /// 연주를 "마쳤다"고 보는 기준. 기본은 끝까지 재생하면 마친 것으로 본다.
    /// 정확도 기준을 쓰고 싶으면 예) StoryFlow.InlineClearRule = r => r.accuracy >= 60f;
    /// 기준을 못 채우면 돌아왔을 때 play 줄부터 다시 보여준다 (넘기면 다시 연주).
    /// </summary>
    public static Predicate<GameManager.PlayResult> InlineClearRule = result => true;

    /// <summary>
    /// 대사의 play 명령으로 연주를 시작한다 (FlowStoryEntry가 DialogueManager.OnPlayRequested에서 호출).
    /// 시작하지 못하면 false -- 이때는 DialogueManager.CancelPlay()로 play를 건너뛰어야 한다.
    /// 이야기 흐름(IsRunning)이 돌고 있지 않아도 동작한다.
    /// </summary>
    public static bool PlayInline(DialoguePlaySnapshot snapshot)
    {
        if (snapshot == null || string.IsNullOrEmpty(snapshot.songId)) return false;

        SongCatalog.Entry entry;
        if (!SongCatalog.TryGet(snapshot.songId, out entry))
        {
            Debug.LogError($"[StoryFlow] play:{snapshot.songId}의 곡을 곡 카탈로그에서 찾을 수 없습니다. " +
                           $"(시트 songs 탭 동기화 후 Resources/{SongCatalog.ResourcePath}.json 이 만들어졌는지 확인)");
            return false;
        }

        if (SceneChanger.Instance == null)
        {
            Debug.LogError("[StoryFlow] SceneChanger 인스턴스가 없습니다. 부트 씬에 SceneChanger가 있어야 합니다.");
            return false;
        }

        if (!PlaySessionAdapter.Apply(entry, snapshot.difficulty)) return false;

        StoryFlowRunner.EnsureExists();

        _inline = snapshot;
        _inlineSongFinished = false;

        // 시작했다는 표시. 끝까지 연주하면 song_{id}_cleared도 1이 된다.
        DialogueExpression.SetFlag(SongFlagKey(snapshot.songId, "played"), 1);

        LoadScene(SceneChanger.SceneType.RhythmPlay, new SceneChanger.ScenePayload
        {
            songId = entry.id,
            difficulty = snapshot.difficulty,
            chapterId = CurrentChapter
        });

        return true;
    }

    /// <summary>
    /// 연주에서 돌아온 Story 씬이 이어갈 정보를 가져간다 (FlowStoryEntry가 Awake에서 호출).
    /// expectedSequenceId와 연주하러 나간 시퀀스가 다르면(다른 이야기에 들어왔으면) 이전 연주 흔적은 버리고 false.
    /// 한 번 확인하면 어느 쪽이든 정리된다.
    /// </summary>
    public static bool TryTakeInline(string expectedSequenceId, out DialoguePlaySnapshot snapshot, out bool songFinished)
    {
        snapshot = _inline;
        songFinished = _inlineSongFinished;

        if (_inline == null) return false;

        bool matches = string.IsNullOrEmpty(expectedSequenceId) || expectedSequenceId == _inline.sequenceId;

        _inline = null;
        _inlineSongFinished = false;

        if (!matches)
        {
            snapshot = null;
            songFinished = false;
            return false;
        }

        return true;
    }

    private static void HandleInlineResult(GameManager.PlayResult result)
    {
        // 흐름과 상관없는 곡의 결과로 처리되지 않게 한다 (곡 ID == 곡 에셋 이름 규칙)
        if (result != null && !string.IsNullOrEmpty(result.songId)
            && !string.Equals(result.songId, _inline.songId, StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning($"[StoryFlow] 기대한 곡은 '{_inline.songId}'인데 '{result.songId}'의 결과가 들어와 무시합니다.");
            return;
        }

        if (result != null)
        {
            DialogueExpression.SetFlag("play_accuracy", (int)result.accuracy);
            DialogueExpression.SetFlag("play_score", result.score);
            DialogueExpression.SetFlag("play_combo", result.maxCombo);
            DialogueExpression.SetFlag("play_miss", result.missCount);
        }

        if (!_inlineSongFinished && (InlineClearRule == null || InlineClearRule(result)))
        {
            _inlineSongFinished = true;
            DialogueExpression.SetFlag(SongFlagKey(_inline.songId, "cleared"), 1);
        }

        _lastAdvanceFrame = Time.frameCount;

        // 결과 화면을 보여준 뒤, 계속 버튼(ContinueAfterResult)이 스토리로 돌려보낸다
        LoadScene(SceneChanger.SceneType.Result);
    }

    /// <summary>곡 결과 플래그 이름. 예) song_tutorial_01_cleared</summary>
    private static string SongFlagKey(string songId, string suffix)
    {
        return $"song_{songId}_{suffix}";
    }
    #endregion

    #region Advance
    private static void EnterNode(StoryFlowNode node, int songIndex = 0)
    {
        CurrentNode = node;
        SongIndex = songIndex;
        if (node.chapter > 0) CurrentChapter = node.chapter;

        _routeChain = node.type == StoryFlowNodeType.Route ? _routeChain + 1 : 0;
        if (_routeChain > MaxRouteChain)
        {
            Debug.LogError($"[StoryFlow] route 칸이 {MaxRouteChain}번 넘게 연달아 이어졌습니다. 조건이 순환하는지 확인하세요. (칸 '{node.id}')");
            Stop();
            return;
        }

        _lastAdvanceFrame = Time.frameCount;
        SaveProgress();
        OnNodeEntered?.Invoke(node);

        switch (node.type)
        {
            case StoryFlowNodeType.Story:
                LoadScene(SceneChanger.SceneType.Story);
                break;

            case StoryFlowNodeType.Play:
                if (node.songs == null || node.songs.Length == 0)
                {
                    Debug.LogWarning($"[StoryFlow] 연주 칸 '{node.id}'에 곡이 없어 건너뜁니다.");
                    AdvanceFrom(node);
                }
                else
                {
                    StartSong();
                }
                break;

            case StoryFlowNodeType.Route:
                AdvanceFrom(node);
                break;

            case StoryFlowNodeType.End:
                FinishEnding(node);
                break;
        }
    }

    private static void StartSong()
    {
        string songId = CurrentNode.songs[SongIndex];

        // 곡 카탈로그(시트의 songs 탭)에 있는 곡이면 연주 씬이 읽는 PlaySession에 미리 넣어 둔다.
        // 카탈로그가 없으면 예전처럼 payload로만 곡 ID를 넘긴다.
        SongCatalog.Entry catalogEntry;
        if (SongCatalog.TryGet(songId, out catalogEntry))
        {
            PlaySessionAdapter.Apply(catalogEntry, CurrentNode.difficulty);
        }
        else if (SongCatalog.HasCatalog)
        {
            Debug.LogWarning($"[StoryFlow] 곡 '{songId}'가 곡 카탈로그에 없습니다. (songs 탭 확인)");
        }

        LoadScene(SceneChanger.SceneType.RhythmPlay, new SceneChanger.ScenePayload
        {
            songId = songId,
            difficulty = CurrentNode.difficulty,
            chapterId = CurrentChapter
        });
    }

    /// <summary>칸을 마무리(플래그, 챕터 처리)하고 다음 칸으로 간다.</summary>
    private static void AdvanceFrom(StoryFlowNode node)
    {
        ApplyCompletion(node);

        string nextId = ResolveNext(node);

        if (string.IsNullOrEmpty(nextId))
        {
            FinishFlow();
            return;
        }

        StoryFlowNode next = Data.Find(nextId);
        if (next == null)
        {
            Debug.LogError($"[StoryFlow] 칸 '{node.id}'의 다음 칸 '{nextId}'를 찾을 수 없습니다. 흐름을 멈춥니다.");
            Stop();
            return;
        }

        EnterNode(next);
    }

    private static void ApplyCompletion(StoryFlowNode node)
    {
        DialogueExpression.ApplyAll(node.setFlags);

        if (GameManager.Instance != null)
        {
            if (node.clearChapter > 0) GameManager.Instance.ClearChapter(node.clearChapter);
            if (node.unlockChapter > 0) GameManager.Instance.UnlockChapter(node.unlockChapter);
        }
    }

    private static string ResolveNext(StoryFlowNode node)
    {
        if (node.branches != null)
        {
            foreach (StoryFlowBranch branch in node.branches)
            {
                if (DialogueExpression.Evaluate(branch.condition) && !string.IsNullOrEmpty(branch.next))
                {
                    return branch.next;
                }
            }
        }

        return node.next;
    }

    private static void FinishFlow()
    {
        ClearSavedPosition();
        Stop();
        LoadScene(DefaultEndScene);
    }

    private static void FinishEnding(StoryFlowNode node)
    {
        ApplyCompletion(node);

        Progress progress = LoadProgress();
        if (!string.IsNullOrEmpty(node.target) && !progress.endings.Contains(node.target))
        {
            progress.endings.Add(node.target);
        }

        // 엔딩에 도달하면 "이어하기" 위치는 지우고, 도달한 엔딩 기록은 남긴다
        progress.flowId = "";
        progress.nodeId = "";
        progress.songIndex = 0;
        SaveProgressData(progress);

        SceneChanger.SceneType scene = DefaultEndScene;
        if (!string.IsNullOrEmpty(node.scene))
        {
            SceneChanger.SceneType parsed;
            if (Enum.TryParse(node.scene, true, out parsed) && Enum.IsDefined(typeof(SceneChanger.SceneType), parsed))
            {
                scene = parsed;
            }
            else
            {
                Debug.LogError($"[StoryFlow] 엔딩 칸 '{node.id}'의 scene '{node.scene}'을(를) 알 수 없어 기본 씬({DefaultEndScene})으로 이동합니다.");
            }
        }

        Stop();
        OnEnding?.Invoke(node.target);
        LoadScene(scene);
    }

    private static void LoadScene(SceneChanger.SceneType scene, SceneChanger.ScenePayload payload = null)
    {
        if (SceneChanger.Instance == null)
        {
            Debug.LogError("[StoryFlow] SceneChanger 인스턴스가 없습니다. 부트 씬에 SceneChanger가 있어야 합니다.");
            return;
        }

        SceneChanger.Instance.LoadScene(scene, payload);
    }
    #endregion

    #region Endings
    /// <summary>이 엔딩에 도달한 적이 있는가 (엔딩 컬렉션 표시용)</summary>
    public static bool HasReachedEnding(string endingId)
    {
        return LoadProgress().endings.Contains(endingId);
    }

    /// <summary>도달한 엔딩 ID 목록 (복사본)</summary>
    public static List<string> ReachedEndings
    {
        get { return new List<string>(LoadProgress().endings); }
    }
    #endregion

    #region Persistence
    [Serializable]
    private class Progress
    {
        public string flowId = "";
        public string nodeId = "";
        public int songIndex;
        public int chapter;
        public List<string> endings = new List<string>();
    }

    private static Progress LoadProgress()
    {
        string json = PlayerPrefs.GetString(ProgressKey, "");
        if (string.IsNullOrEmpty(json)) return new Progress();

        Progress progress = JsonUtility.FromJson<Progress>(json);
        if (progress == null) return new Progress();

        if (progress.endings == null) progress.endings = new List<string>();
        return progress;
    }

    private static void SaveProgressData(Progress progress)
    {
        PlayerPrefs.SetString(ProgressKey, JsonUtility.ToJson(progress));
        PlayerPrefs.Save();
    }

    /// <summary>현재 위치를 저장한다. (칸에 진입할 때, 곡이 끝날 때)</summary>
    private static void SaveProgress()
    {
        if (!IsRunning || Data == null || CurrentNode == null) return;

        Progress progress = LoadProgress();
        progress.flowId = Data.flowId;
        progress.nodeId = CurrentNode.id;
        progress.songIndex = SongIndex;
        progress.chapter = CurrentChapter;
        SaveProgressData(progress);
    }

    private static void ClearSavedPosition()
    {
        Progress progress = LoadProgress();
        progress.flowId = "";
        progress.nodeId = "";
        progress.songIndex = 0;
        SaveProgressData(progress);
    }

    /// <summary>"처음부터 하기"용: 저장된 위치를 지운다. 도달한 엔딩 기록은 남긴다.</summary>
    public static void ResetSavedPosition()
    {
        ClearSavedPosition();
    }

    /// <summary>저장된 위치와 엔딩 기록을 모두 지운다 (개발/테스트용)</summary>
    public static void ResetAllProgress()
    {
        PlayerPrefs.DeleteKey(ProgressKey);
        PlayerPrefs.Save();
    }
    #endregion
}
