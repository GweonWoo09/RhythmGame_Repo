using UnityEngine;

/// <summary>
/// 대화(스토리)가 모두 끝나면 targetScene으로 이동한다.
/// DialogueManager.OnDialogEnd를 구독하고, 실제 이동은 SceneTransitionTrigger의 공통 로직에 맡긴다.
///
/// 사용법: 스토리 씬의 아무 오브젝트에 붙이고 dialogueManager를 연결한다.
/// 컴포넌트를 처음 추가하면 targetScene이 ChapterSelect로 미리 설정된다.
/// </summary>
public class SceneTransitionDialogueEnd : SceneTransitionTrigger
{
    [Tooltip("끝나는 시점을 감시할 DialogueManager. 비워두면 같은 오브젝트에서 찾는다.")]
    [SerializeField] private DialogueManager dialogueManager;

    /// <summary>컴포넌트를 추가할 때 한 번 호출되어, 기본 이동 대상을 ChapterSelect로 지정한다.</summary>
    private void Reset()
    {
        targetScene = SceneChanger.SceneType.ChapterSelect;
    }

    private void Awake()
    {
        if (dialogueManager == null)
        {
            dialogueManager = GetComponent<DialogueManager>();
        }
    }

    private void OnEnable()
    {
        if (dialogueManager == null)
        {
            Debug.LogError("[SceneTransitionDialogueEnd] DialogueManager가 연결되지 않았습니다.");
            return;
        }

        dialogueManager.OnDialogEnd += GoToTargetScene;
    }

    private void OnDisable()
    {
        if (dialogueManager == null) return;

        dialogueManager.OnDialogEnd -= GoToTargetScene;
    }
}
