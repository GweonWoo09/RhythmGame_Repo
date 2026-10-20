using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 대화 진행 로직만 담당한다. 실제 UI(Image, Text)는 건드리지 않고
/// 이벤트를 통해 현재 대사/선택지 정보를 알린다. 화면 표시는 DialogueUIView가 담당.
/// 대사 데이터는 DialogueDataSO 에셋으로부터 받아오며, BGM 전환은 AudioManager에 위임한다.
///
/// [v2 진행 규칙]
/// 1) 줄을 보여주기 전에 condition이 거짓이면 그 줄을 통째로 건너뛴다 (setFlags/goto도 실행되지 않음).
/// 2) 줄이 표시될 때: bgmOverride -> setFlags 적용 -> commands 발행 -> OnDialogUpdated -> 선택지.
/// 3) 줄을 읽고 넘어갈 때: branches(처음 참인 것) -> gotoLabel -> 다음 줄 순서로 목적지를 정한다.
///    목적지가 "end"이거나 마지막 줄을 지나면 대화 종료.
/// 4) 선택지는 condition이 참인 것만 UI로 전달한다 (SelectChoice 인덱스도 전달된 목록 기준).
/// 5) 점프 목적지는 gotoLabel(권장)이 우선, 없으면 targetIndex. 잘못된 목적지면 에러만 남기고
///    현재 상태(선택 대기 포함)를 그대로 유지한다.
/// 6) 대화 종료 시 onEnd의 clearChapter/unlockChapter를 GameManager에 반영하고 OnDialogEnd를 발행한다.
///    씬 이동은 SceneTransitionDialogueEnd가 EndAction을 읽어 처리한다.
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

    // 조건을 통과해 UI로 전달된 선택지 목록. SelectChoice의 인덱스는 이 배열 기준이다.
    private DialogChoice[] _visibleChoices;

    /// <summary>새 대사가 표시될 때: (화면 슬롯, 화자 데이터(null이면 내레이션), 표정 이름, 대사 텍스트)</summary>
    public event Action<int, SpeakerDataSO, string, string> OnDialogUpdated;

    /// <summary>현재 줄이 선택지를 가지고 있을 때 발행. 조건을 통과한 선택지만 담겨 있다.</summary>
    public event Action<DialogChoice[]> OnChoicesPresented;

    /// <summary>대화가 모두 끝났을 때</summary>
    public event Action OnDialogEnd;

    /// <summary>시퀀스가 시작/교체될 때 그 시퀀스의 cast(무대에 서 있을 인물)를 알린다. 없으면 null.</summary>
    public event Action<DialogueCast[]> OnCastUpdated;

    /// <summary>줄의 commands를 "이름", "인자"로 쪼개 발행한다. 예: "hide:0" -> ("hide", "0"). 처리는 구독자 몫.</summary>
    public event Action<string, string> OnCommand;

    private void Start()
    {
        if (dialogSequence == null && !string.IsNullOrEmpty(startSequenceId))
        {
            dialogSequence = DialogueJsonLoader.Load(startSequenceId);
        }

        if (HasLines())
        {
            BeginSequence(0);
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
        _visibleChoices = null;

        BeginSequence(startIndex);
    }

    /// <summary>
    /// 외부(입력 처리 스크립트 등)에서 "다음으로 진행" 요청 시 호출.
    /// 선택지 대기 중일 때는 무시된다 (선택지는 SelectChoice로만 진행).
    /// </summary>
    public void AdvanceDialog()
    {
        if (isFinished || isWaitingForChoice || !HasLines()) return;

        var line = dialogSequence.lines[currentDialogIndex];

        if (!TryGetNextIndex(line, out int nextIndex))
        {
            // 목적지 라벨을 찾지 못함: 에러는 이미 남겼고, 엉뚱한 줄로 넘어가지 않도록 현재 줄에 머문다
            return;
        }

        ShowDialog(nextIndex);
    }

    /// <summary>UI에서 선택지 버튼을 눌렀을 때 호출. choiceIndex는 OnChoicesPresented로 전달된 배열의 인덱스.</summary>
    public void SelectChoice(int choiceIndex)
    {
        if (!isWaitingForChoice) return;
        if (_visibleChoices == null || choiceIndex < 0 || choiceIndex >= _visibleChoices.Length) return;

        var choice = _visibleChoices[choiceIndex];

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

        DialogueDataSO targetSequence = nextSequence != null ? nextSequence : dialogSequence;

        // 이동할 줄 결정 (gotoLabel 우선, 없으면 targetIndex). 검증에 실패하면 플래그도 기록하지 않고 선택 대기 유지.
        bool endsDialogue = choice.gotoLabel == DialogueDataSO.EndLabel;
        int targetIndex = choice.targetIndex;

        if (!endsDialogue)
        {
            if (!string.IsNullOrEmpty(choice.gotoLabel))
            {
                if (!TryResolveLabel(targetSequence, choice.gotoLabel, out targetIndex)) return;
            }

            if (!IsValidIndex(targetSequence, targetIndex))
            {
                Debug.LogError($"[DialogueManager] 선택지 '{choice.choiceText}'의 이동 위치({targetIndex})가 " +
                               $"'{targetSequence.name}' 시퀀스의 범위를 벗어났습니다. " +
                               $"(줄 수: {(targetSequence.lines != null ? targetSequence.lines.Length : 0)})");
                return;
            }
        }

        isWaitingForChoice = false;
        _visibleChoices = null;

        // 플래그 기록 (하위 호환 단일 플래그 + 새 setFlags)
        if (!string.IsNullOrEmpty(choice.flagKey))
        {
            DialogueExpression.SetFlag(choice.flagKey, choice.storyFlagValue);
        }
        DialogueExpression.ApplyAll(choice.setFlags);

        if (endsDialogue)
        {
            FinishDialog();
        }
        else if (nextSequence != null)
        {
            // 다른 시퀀스(챕터 분기)로 전환 (BGM, cast도 새 시퀀스 기준으로 교체됨)
            SetDialogSequence(nextSequence, targetIndex);
        }
        else
        {
            // 같은 시퀀스 내에서 점프
            ShowDialog(targetIndex);
        }
    }

    #region Flow
    private void BeginSequence(int startIndex)
    {
        PlaySequenceBGM();
        OnCastUpdated?.Invoke(dialogSequence.cast);
        ShowDialog(startIndex);
    }

    /// <summary>index부터 시작해 condition이 참인 첫 줄을 보여준다. 없으면 대화 종료.</summary>
    private void ShowDialog(int index)
    {
        index = FindPlayableIndex(index);
        if (index < 0)
        {
            FinishDialog();
            return;
        }

        currentDialogIndex = index;

        var line = dialogSequence.lines[index];

        // 이 줄에서 BGM을 바꾸도록 지정되어 있으면 교체
        if (line.bgmOverride != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBGM(line.bgmOverride);
        }

        // 플래그 기록 -> 명령 발행 (선택지 조건은 이 기록이 반영된 뒤에 평가된다)
        DialogueExpression.ApplyAll(line.setFlags);
        RaiseCommands(line.commands);

        OnDialogUpdated?.Invoke(line.screenSlot, line.speaker, line.expressionName, line.dialogText);

        if (line.choices != null && line.choices.Length > 0)
        {
            var visible = FilterChoices(line.choices);

            if (visible.Length > 0)
            {
                _visibleChoices = visible;
                isWaitingForChoice = true;
                OnChoicesPresented?.Invoke(visible);
            }
            else
            {
                Debug.LogWarning($"[DialogueManager] {dialogSequence.name} {index}번 줄: 조건을 통과한 선택지가 없어 일반 대사로 진행합니다.");
            }
        }
    }

    private void FinishDialog()
    {
        isFinished = true;
        isWaitingForChoice = false;
        _visibleChoices = null;

        if (dialogSequence != null && GameManager.Instance != null)
        {
            var end = dialogSequence.onEnd;

            if (end.clearChapter > 0) GameManager.Instance.MarkChapterCleared(end.clearChapter);
            if (end.unlockChapter > 0) GameManager.Instance.UnlockChapter(end.unlockChapter);
        }

        OnDialogEnd?.Invoke();
    }

    /// <summary>현재 줄을 읽고 난 뒤 이동할 인덱스를 정한다. 마지막 줄을 지나거나 "end"면 lines.Length를 돌려준다.</summary>
    private bool TryGetNextIndex(DialogLine line, out int nextIndex)
    {
        string target = null;

        if (line.branches != null)
        {
            foreach (var branch in line.branches)
            {
                if (DialogueExpression.Evaluate(branch.condition))
                {
                    target = branch.gotoLabel;
                    break;
                }
            }
        }

        if (string.IsNullOrEmpty(target)) target = line.gotoLabel;

        if (string.IsNullOrEmpty(target))
        {
            nextIndex = currentDialogIndex + 1;
            return true;
        }

        if (target == DialogueDataSO.EndLabel)
        {
            nextIndex = dialogSequence.lines.Length;
            return true;
        }

        return TryResolveLabel(dialogSequence, target, out nextIndex);
    }

    private int FindPlayableIndex(int startIndex)
    {
        for (int i = Mathf.Max(0, startIndex); i < dialogSequence.lines.Length; i++)
        {
            if (DialogueExpression.Evaluate(dialogSequence.lines[i].condition))
            {
                return i;
            }
        }

        return -1;
    }

    private static DialogChoice[] FilterChoices(DialogChoice[] all)
    {
        var visible = new List<DialogChoice>(all.Length);

        foreach (var choice in all)
        {
            if (DialogueExpression.Evaluate(choice.condition))
            {
                visible.Add(choice);
            }
        }

        return visible.ToArray();
    }

    private void RaiseCommands(string[] commands)
    {
        if (commands == null || OnCommand == null) return;

        foreach (string command in commands)
        {
            if (string.IsNullOrWhiteSpace(command)) continue;

            string[] parts = command.Split(new[] { ':' }, 2);
            string name = parts[0].Trim();
            string arg = parts.Length > 1 ? parts[1].Trim() : string.Empty;

            OnCommand.Invoke(name, arg);
        }
    }

    private static bool TryResolveLabel(DialogueDataSO sequence, string label, out int index)
    {
        index = sequence.FindLabelIndex(label);

        if (index < 0)
        {
            Debug.LogError($"[DialogueManager] '{sequence.name}' 시퀀스에서 id '{label}'인 줄을 찾을 수 없습니다.");
            return false;
        }

        return true;
    }
    #endregion

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

    /// <summary>현재(마지막) 시퀀스의 챕터 번호</summary>
    public int ChapterId => dialogSequence != null ? dialogSequence.chapterId : 0;

    /// <summary>현재(마지막) 시퀀스의 종료 후 동작. SceneTransitionDialogueEnd가 읽는다.</summary>
    public DialogueEndAction EndAction => dialogSequence != null ? dialogSequence.onEnd : default(DialogueEndAction);
}