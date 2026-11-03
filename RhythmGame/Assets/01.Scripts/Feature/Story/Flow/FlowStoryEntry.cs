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

    /// <summary>대사의 play 줄을 넘겼다: 연주를 시작한다. 시작하지 못하면 play를 건너뛰고 대사를 계속한다.</summary>
    private void HandlePlayRequested(DialoguePlaySnapshot snapshot)
    {
        if (!StoryFlow.PlayInline(snapshot))
        {
            dialogueManager.CancelPlay();
        }
    }

    private void HandleDialogEnd()
    {
        if (StoryFlow.IsRunning)
        {
            StoryFlow.CompleteStory();
        }
    }
}
