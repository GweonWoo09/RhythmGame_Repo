using UnityEngine;

/// <summary>
/// Story 씬에 붙이는 연결 컴포넌트. 흐름(StoryFlow)이 돌고 있으면
///   1) 지금 칸(Story)의 대화 시퀀스로 DialogueManager의 시작 시퀀스를 바꾸고
///   2) 대화가 끝나면 StoryFlow.CompleteStory()로 다음 칸으로 넘긴다.
///
/// 대사 안의 play 명령도 이 컴포넌트가 연결한다.
///   3) DialogueManager가 play 줄을 넘기며 연주를 요청하면 StoryFlow.PlayInline으로 연주를 시작하고,
///   4) 연주에서 돌아와 이 씬이 다시 열리면(StoryFlow.TryTakeInline) 연주 직전 대사 위치에서 이어간다.
///
/// 흐름이 돌고 있지 않으면(Story 씬만 단독으로 실행해 테스트할 때) 시작 시퀀스는 아무것도 바꾸지 않아서
/// DialogueManager가 자기 설정(dialogSequence / startSequenceId)대로 재생한다.
///
/// 사용법: Story 씬의 아무 오브젝트에 붙이고 dialogueManager를 연결한다.
/// 이 씬에서는 SceneTransitionDialogueEnd를 빼도 되고, 두어도 흐름이 돌고 있는 동안에는 스스로 물러난다.
/// </summary>
public class FlowStoryEntry : MonoBehaviour
{
    [Tooltip("재생할 DialogueManager. 비워두면 같은 오브젝트에서 찾는다.")]
    [SerializeField] private DialogueManager dialogueManager;

    // DialogueManager.Start()가 시퀀스를 읽기 전에 시작 시퀀스를 바꿔야 하므로 Awake에서 처리한다.
    // (모든 Awake는 어떤 Start보다 먼저 끝난다.)
    private void Awake()
    {
        if (dialogueManager == null)
        {
            dialogueManager = GetComponent<DialogueManager>();
        }

        if (dialogueManager == null)
        {
            Debug.LogError("[FlowStoryEntry] DialogueManager가 연결되지 않았습니다.");
            return;
        }

        // 연주에서 돌아온 경우: 흐름의 시작 시퀀스를 바꾸지 않고 연주 직전 위치에서 이어간다.
        // 흐름이 돌고 있으면 지금 칸의 시퀀스, 아니면 DialogueManager의 시작 시퀀스와 같은 이야기일 때만 이어간다.
        StoryFlowNode currentNode = StoryFlow.CurrentNode;
        string expectedSequence = (StoryFlow.IsRunning && currentNode != null && currentNode.type == StoryFlowNodeType.Story)
            ? currentNode.target
            : dialogueManager.StartSequenceId;

        DialoguePlaySnapshot snapshot;
        bool songFinished;
        if (StoryFlow.TryTakeInline(expectedSequence, out snapshot, out songFinished))
        {
            dialogueManager.PrepareResume(snapshot, songFinished);
            return;
        }

        if (!StoryFlow.IsRunning) return;

        StoryFlowNode node = StoryFlow.CurrentNode;
        if (node == null || node.type != StoryFlowNodeType.Story)
        {
            Debug.LogWarning($"[FlowStoryEntry] 흐름의 지금 칸이 Story가 아닙니다. (칸: {(node != null ? node.id + "/" + node.type : "없음")})");
            return;
        }

        dialogueManager.OverrideStartSequence(node.target);
    }

    private void OnEnable()
    {
        if (dialogueManager == null) return;

        dialogueManager.OnDialogEnd += HandleDialogEnd;
        dialogueManager.OnPlayRequested += HandlePlayRequested;
    }

    private void OnDisable()
    {
        if (dialogueManager == null) return;

        dialogueManager.OnDialogEnd -= HandleDialogEnd;
        dialogueManager.OnPlayRequested -= HandlePlayRequested;
    }

    [Tooltip("끄면(기본) play 줄에서 곡을 곡 선택창의 Story 탭에 추가하고 곡 선택창으로 이동한다.\n" +
             "켜면 예전처럼 그 자리에서 바로 연주하고 끝나면 대사로 돌아온다.")]
    [SerializeField] private bool playImmediately = false;

    /// <summary>
    /// 대사의 play 줄을 넘겼다.
    /// 기본: 곡을 Story 탭에 추가하고, play 줄 다음 줄을 대사 위치로 저장한 뒤 곡 선택창(Story 탭, 그 곡 선택)으로 간다.
    /// playImmediately: 연주를 바로 시작한다. 시작하지 못하면 play를 건너뛰고 대사를 계속한다.
    /// </summary>
    private void HandlePlayRequested(DialoguePlaySnapshot snapshot)
    {
        if (playImmediately)
        {
            if (!StoryFlow.PlayInline(snapshot)) dialogueManager.CancelPlay();
            return;
        }

        if (SceneChanger.Instance == null || GameManager.Instance == null)
        {
            Debug.LogError("[FlowStoryEntry] SceneChanger/GameManager가 없어 곡 선택창으로 갈 수 없습니다. play를 건너뜁니다. (Loading 씬부터 실행)");
            dialogueManager.CancelPlay();
            return;
        }

        // 곡 카탈로그에 없는 곡(시트 songs 탭의 path가 아직 비어 있는 곡)은 연주 없이 다음 대사로 넘어간다
        SongCatalog.Entry entry;
        if (!SongCatalog.TryGet(snapshot.songId, out entry))
        {
            Debug.LogWarning($"[FlowStoryEntry] play:{snapshot.songId}의 곡이 곡 카탈로그에 없어 건너뜁니다. (시트 songs 탭의 path를 채운 뒤 동기화)");
            dialogueManager.CancelPlay();
            return;
        }

        GameManager.Instance.UnlockStorySong(snapshot.songId);
        dialogueManager.LeaveAfterPlayLine();

        // 이야기 흐름이 돌고 있었다면 멈춘다 (흐름의 저장 위치는 남는다)
        if (StoryFlow.IsRunning) StoryFlow.Stop();

        SongSelectController.RequestStorySong(snapshot.songId, snapshot.difficulty);
        SceneChanger.Instance.LoadScene(SceneChanger.SceneType.SongSelect);
    }

    private void HandleDialogEnd()
    {
        if (StoryFlow.IsRunning)
        {
            StoryFlow.CompleteStory();
        }
    }
}
