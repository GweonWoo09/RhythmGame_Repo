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

    /// <summary>새 대사가 표시될 때: (스피커 인덱스, 스피커 이름, 대사 텍스트)</summary>
    public event Action<int, string, string> OnDialogUpdated;

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
    public void SetdialogData(DialogueData sequence)
    {
        dialogData = sequence;
        ResetDialog();
    }

    /// <summary>
    /// 외부(입력 처리 스크립트, PlayerInput 등)에서 "다음으로 진행" 요청 시 호출.
    /// 입력 처리와 대화 로직을 분리하기 위해 외부에서 트리거하는 방식으로 구성.
    /// </summary>
    public void AdvanceDialog()
    {
        if (isFinished || !HasLines()) return;

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

    private void ShowDialog(int index)
    {
        currentDialogIndex = index;

        var line = dialogData.lines[index];
        OnDialogUpdated?.Invoke(line.speakerIndex, line.speakerName, line.dialogText);
    }

    /// <summary>씬 진입 시 처음부터 다시 재생 (세이브 데이터로 특정 지점부터 시작하도록 확장 가능)</summary>
    public void ResetDialog()
    {
        currentDialogIndex = -1;
        isFinished = false;

        if (HasLines())
        {
            ShowDialog(0);
        }
    }

    private bool HasLines()
    {
        return dialogData != null && dialogData.lines != null && dialogData.lines.Length > 0;
    }

    public bool IsFinished => isFinished;
}