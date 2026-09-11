using System;
using UnityEngine;

/// <summary>
/// 대화 진행 로직만 담당한다. 실제 UI(Image, Text)는 건드리지 않고
/// 이벤트를 통해 현재 대사 정보를 알린다. 화면 표시는 DialogUIView가 담당.
/// 대사 데이터는 DialogueData 에셋으로부터 받아온다.
/// </summary>
public class DialogueManager : MonoBehaviour
{
    [Tooltip("재생할 대화 시퀀스 에셋 (DialogueData)")]
    [SerializeField] private DialogueData dialogData;

    private int currentDialogIndex = -1;
    private bool isFinished = false;
    private bool isWaitingForChoice = false;

    /// <summary>새 대사가 표시될 때: (스피커 인덱스, 스피커 이름, 대사 텍스트)</summary>
    public event Action<int, string, string> OnDialogUpdated;

    /// <summary>현재 줄이 선택지를 가지고 있을 때 발행. UI는 이 배열로 버튼을 그린다.</summary>
    public event Action<DialogChoice[]> OnChoicesPresented;

    /// <summary>대화가 모두 끝났을 때</summary>
    public event Action OnDialogEnd;

    private void Start()
    {
        if (HasLines())
        {
            ShowDialog(0);
        }
    }

    /// <summary>
    /// 다른 챕터/컷씬의 대화로 교체하고 싶을 때 (예: SceneManager로부터 payload로 전달받은 경우)
    /// </summary>
    public void SetDialogSequence(DialogueData sequence, int startIndex = 0)
    {
        dialogData = sequence;
        currentDialogIndex = -1;
        isFinished = false;
        isWaitingForChoice = false;

        if (HasLines())
        {
            ShowDialog(startIndex);
        }
    }

    /// <summary>
    /// 외부(입력 처리 스크립트, PlayerInput 등)에서 "다음으로 진행" 요청 시 호출.
    /// 입력 처리와 대화 로직을 분리하기 위해 외부에서 트리거하는 방식으로 구성.
    /// 선택지 대기 중일 때는 무시된다 (선택지는 SelectChoice로만 진행).
    /// </summary>
    public void AdvanceDialog()
    {
        if (isFinished || isWaitingForChoice || !HasLines()) return;

        int nextIndex = currentDialogIndex + 1;

        if (nextIndex < dialogData.lines.Length)
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

        var currentLine = dialogData.lines[currentDialogIndex];
        if (choiceIndex < 0 || choiceIndex >= currentLine.choices.Length) return;

        var choice = currentLine.choices[choiceIndex];
        isWaitingForChoice = false;

        if (choice.storyFlagValue >= 0 && GameManager.Instance != null)
        {
            GameManager.Instance.ProgressData.storyFlag = choice.storyFlagValue;
        }

        if (choice.targetSequence != null)
        {
            // 다른 시퀀스(챕터 분기)로 전환
            SetDialogSequence(choice.targetSequence, choice.targetIndex);
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

        var line = dialogData.lines[index];
        OnDialogUpdated?.Invoke(line.speakerIndex, line.speakerName, line.dialogText);

        if (line.choices != null && line.choices.Length > 0)
        {
            isWaitingForChoice = true;
            OnChoicesPresented?.Invoke(line.choices);
        }
    }

    /// <summary>씬 진입 시 처음부터 다시 재생</summary>
    public void ResetDialog()
    {
        if (HasLines())
        {
            SetDialogSequence(dialogData, 0);
        }
    }

    private bool HasLines()
    {
        return dialogData != null && dialogData.lines != null && dialogData.lines.Length > 0;
    }

    public bool IsFinished => isFinished;
    public bool IsWaitingForChoice => isWaitingForChoice;
}