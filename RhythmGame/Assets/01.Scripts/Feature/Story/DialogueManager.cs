using System;
using UnityEngine;

/// <summary>
/// 대화 진행 로직만 담당한다. 실제 UI(Image, Text)는 건드리지 않고
/// 이벤트를 통해 현재 대사/선택지 정보를 알린다. 화면 표시는 DialogueUIView가 담당.
/// 대사 데이터는 DialogueDataSO 에셋으로부터 받아오며, BGM 전환은 AudioManager에 위임한다.
/// (이전 이름: DialogManager)
///
/// [수정 내역]
/// - 대사 인덱스(startIndex / choice.targetIndex) 범위 검증 추가. 잘못된 값이면 에러만 남기고
///   현재 상태(선택 대기 포함)를 그대로 유지한다.
/// </summary>
public class DialogueManager : MonoBehaviour
{
    [Tooltip("재생할 대화 시퀀스 에셋 (DialogueDataSO)")]
    [SerializeField] private DialogueDataSO dialogSequence;

    [Tooltip("dialogSequence가 비어있을 때 JSON(Resources/Dialogue/{언어}/{ID}.json)에서 불러올 시퀀스 ID")]
    [SerializeField] private string startSequenceId;

    private int currentDialogIndex = -1;
    private bool isFinished = false;
    private bool isWaitingForChoice = false;

    /// <summary>새 대사가 표시될 때: (화면 슬롯, 화자 데이터, 표정 이름, 대사 텍스트)</summary>
    public event Action<int, SpeakerDataSO, string, string> OnDialogUpdated;

    /// <summary>현재 줄이 선택지를 가지고 있을 때 발행. UI는 이 배열로 버튼을 그린다.</summary>
    public event Action<DialogChoice[]> OnChoicesPresented;

    /// <summary>대화가 모두 끝났을 때</summary>
    public event Action OnDialogEnd;

    private void Start()
    {
        if (dialogSequence == null && !string.IsNullOrEmpty(startSequenceId))
        {
            dialogSequence = DialogueJsonLoader.Load(startSequenceId);
        }

        if (HasLines())
        {
            PlaySequenceBGM();
            ShowDialog(0);
        }
    }

    /// <summary>
    /// 다른 챕터/컷씬의 대화로 교체하고 싶을 때 (예: SceneChanger의 payload로 전달받은 경우).
    /// 시퀀스가 비어있거나 startIndex가 범위를 벗어나면 에러만 남기고 현재 상태를 바꾸지 않는다.
    /// </summary>
    public void SetDialogSequence(DialogueDataSO sequence, int startIndex = 0)
    {
        if (!IsValidIndex(sequence, startIndex))
        {
            Debug.LogError($"[DialogueManager] 시퀀스를 시작할 수 없습니다. " +
                           $"'{(sequence != null ? sequence.name : "null")}' 시퀀스에 {startIndex}번 줄이 없습니다.");
            return;
        }

        dialogSequence = sequence;
        currentDialogIndex = -1;
        isFinished = false;
        isWaitingForChoice = false;

        PlaySequenceBGM();
        ShowDialog(startIndex);
    }

    /// <summary>
    /// 외부(입력 처리 스크립트 등)에서 "다음으로 진행" 요청 시 호출.
    /// 선택지 대기 중일 때는 무시된다 (선택지는 SelectChoice로만 진행).
    /// </summary>
    public void AdvanceDialog()
    {
        if (isFinished || isWaitingForChoice || !HasLines()) return;

        int nextIndex = currentDialogIndex + 1;

        if (nextIndex < dialogSequence.lines.Length)
        {
            ShowDialog(nextIndex);
        }
        else
        {
            isFinished = true;
            OnDialogEnd?.Invoke();
        }
    }

    /// <summary>UI에서 선택지 버튼을 눌렀을 때 호출. choiceIndex는 현재 줄의 choices 배열 인덱스.</summary>
    public void SelectChoice(int choiceIndex)
    {
        if (!isWaitingForChoice) return;

        var currentLine = dialogSequence.lines[currentDialogIndex];
        if (currentLine.choices == null || choiceIndex < 0 || choiceIndex >= currentLine.choices.Length) return;

        var choice = currentLine.choices[choiceIndex];

        // 다른 시퀀스로 분기하는 선택지라면, 상태를 바꾸기 전에 대상 시퀀스를 먼저 확보한다.
        // (직접 참조가 없고 ID만 있으면 JSON에서 로드)
        DialogueDataSO nextSequence = choice.targetSequence;
        if (nextSequence == null && !string.IsNullOrEmpty(choice.targetSequenceId))
        {
            nextSequence = DialogueJsonLoader.Load(choice.targetSequenceId);
            if (nextSequence == null)
            {
                // 로드 실패 시 선택 대기 상태를 유지해서 엉뚱한 줄로 넘어가지 않게 한다
                return;
            }
        }

        // 점프할 대상 시퀀스(다른 시퀀스 or 현재 시퀀스)에 targetIndex가 실제로 존재하는지 검증.
        // 실패하면 플래그도 기록하지 않고, 선택 대기 상태를 유지한다.
        DialogueDataSO targetSequence = nextSequence != null ? nextSequence : dialogSequence;
        if (!IsValidIndex(targetSequence, choice.targetIndex))
        {
            Debug.LogError($"[DialogueManager] 선택지 '{choice.choiceText}'의 targetIndex({choice.targetIndex})가 " +
                           $"'{targetSequence.name}' 시퀀스의 범위를 벗어났습니다. " +
                           $"(줄 수: {(targetSequence.lines != null ? targetSequence.lines.Length : 0)})");
            return;
        }

        isWaitingForChoice = false;

        if (!string.IsNullOrEmpty(choice.flagKey) && GameManager.Instance != null)
        {
            GameManager.Instance.SetStoryFlag(choice.flagKey, choice.storyFlagValue);
        }

        if (nextSequence != null)
        {
            // 다른 시퀀스(챕터 분기)로 전환 (BGM도 새 시퀀스 기준으로 교체됨)
            SetDialogSequence(nextSequence, choice.targetIndex);
        }
        else
        {
            // 같은 시퀀스 내에서 점프
            ShowDialog(choice.targetIndex);
        }
    }

    private void ShowDialog(int index)
    {
        currentDialogIndex = index;

        var line = dialogSequence.lines[index];

        // 이 줄에서 BGM을 바꾸도록 지정되어 있으면 교체
        if (line.bgmOverride != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBGM(line.bgmOverride);
        }

        OnDialogUpdated?.Invoke(line.screenSlot, line.speaker, line.expressionName, line.dialogText);

        if (line.choices != null && line.choices.Length > 0)
        {
            isWaitingForChoice = true;
            OnChoicesPresented?.Invoke(line.choices);
        }
    }

    /// <summary>시퀀스 진입 시 기본 BGM 재생 (지정 안 됐으면 기존 BGM 유지)</summary>
    private void PlaySequenceBGM()
    {
        if (dialogSequence.bgm != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBGM(dialogSequence.bgm);
        }
    }

    /// <summary>JSON 파일(Resources/Dialogue/{언어}/{sequenceId}.json)을 불러와 바로 재생</summary>
    public void LoadSequenceFromJson(string sequenceId, int startIndex = 0)
    {
        var sequence = DialogueJsonLoader.Load(sequenceId);
        if (sequence != null)
        {
            SetDialogSequence(sequence, startIndex);
        }
    }

    /// <summary>씬 진입 시 처음부터 다시 재생</summary>
    public void ResetDialog()
    {
        if (HasLines())
        {
            SetDialogSequence(dialogSequence, 0);
        }
    }

    private bool HasLines()
    {
        return dialogSequence != null && dialogSequence.lines != null && dialogSequence.lines.Length > 0;
    }

    /// <summary>sequence가 존재하고 index가 lines 범위 안인지 확인</summary>
    private static bool IsValidIndex(DialogueDataSO sequence, int index)
    {
        return sequence != null
               && sequence.lines != null
               && index >= 0
               && index < sequence.lines.Length;
    }

    public bool IsFinished => isFinished;
    public bool IsWaitingForChoice => isWaitingForChoice;
}