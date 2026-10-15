using System;
using UnityEngine;

/// <summary>
/// 대화(스토리)가 모두 끝나면 씬을 이동한다.
/// DialogueManager.OnDialogEnd를 구독하고, 이동할 곳은 다음 순서로 정한다.
///   1) 시퀀스의 onEnd.scene 이 지정돼 있으면 그 씬 (songId/difficulty/chapterId는 payload로 함께 전달)
///   2) 비어있으면 이 컴포넌트의 targetScene (기본값 ChapterSelect)
///
/// 챕터 클리어/해금(onEnd.clearChapter, unlockChapter)은 씬 이동과 무관하게 DialogueManager가
/// GameManager에 직접 반영하므로 여기서는 다루지 않는다.
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

        dialogueManager.OnDialogEnd += HandleDialogEnd;
    }

    private void OnDisable()
    {
        if (dialogueManager == null) return;

        dialogueManager.OnDialogEnd -= HandleDialogEnd;
    }

    private void HandleDialogEnd()
    {
        var action = dialogueManager.EndAction;

        // onEnd.scene이 없으면 기존 방식(인스펙터의 targetScene)으로 이동
        if (string.IsNullOrEmpty(action.scene))
        {
            GoToTargetScene();
            return;
        }

        // Enum.TryParse는 "5" 같은 숫자도 통과시키므로 IsDefined로 한 번 더 확인한다
        if (!Enum.TryParse(action.scene, true, out SceneChanger.SceneType scene)
            || !Enum.IsDefined(typeof(SceneChanger.SceneType), scene))
        {
            Debug.LogError($"[SceneTransitionDialogueEnd] onEnd.scene '{action.scene}'은(는) SceneType에 없는 이름입니다. " +
                           $"기본 targetScene({targetScene})으로 이동합니다.");
            GoToTargetScene();
            return;
        }

        if (SceneChanger.Instance == null)
        {
            Debug.LogError("[SceneTransitionDialogueEnd] 씬에 SceneChanger 인스턴스가 없습니다.");
            return;
        }

        bool needsPayload = dialogueManager.ChapterId > 0
                            || !string.IsNullOrEmpty(action.songId)
                            || !string.IsNullOrEmpty(action.difficulty);

        if (needsPayload)
        {
            SceneChanger.Instance.LoadScene(scene, new SceneChanger.ScenePayload
            {
                chapterId = dialogueManager.ChapterId,
                songId = action.songId,
                difficulty = action.difficulty
            });
        }
        else
        {
            SceneChanger.Instance.LoadScene(scene);
        }
    }
}