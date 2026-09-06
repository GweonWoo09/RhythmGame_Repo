using System;
using UnityEngine;

public struct DialogueData
{
    [HideInInspector] public int speakerIndex;
    public string speakerName;
    [TextArea(1, 3)]
    public string dialogText;
}

/// <summary>
/// 대화 진행 로직만 담당한다. 실제 UI(Image, Text)는 건드리지 않고
/// 이벤트를 통해 현재 대사 정보를 알린다. 화면 표시는 DialogUIView가 담당.
/// </summary>
public class DialogueManager : MonoBehaviour
{
    public DialogueData[] dialogs;

    private int currentDialogIndex = -1;
    private bool isFinished = false;

    /// <summary>새 대사가 표시될 때: (스피커 인덱스, 스피커 이름, 대사 텍스트)</summary>
    public event Action<int, string, string> OnDialogUpdated;

    /// <summary>대화가 모두 끝났을 때</summary>
    public event Action OnDialogEnd;

    private void Start()
    {
        // 대화 시작 시 첫 대사 표시
        if (dialogs != null && dialogs.Length > 0)
        {
            ShowDialog(0);
        }
    }

    /// <summary>
    /// 외부(입력 처리 스크립트, PlayerInput 등)에서 "다음으로 진행" 요청 시 호출.
    /// 원본 코드는 내부에서 Input.GetKeyDown을 직접 체크했지만,
    /// 입력 처리와 대화 로직을 분리하기 위해 외부에서 트리거하는 방식으로 변경.
    /// </summary>
    public void AdvanceDialog()
    {
        if (isFinished) return;

        int nextIndex = currentDialogIndex + 1;

        if (nextIndex < dialogs.Length)
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

        var dialog = dialogs[index];
        OnDialogUpdated?.Invoke(dialog.speakerIndex, dialog.speakerName, dialog.dialogText);
    }

    /// <summary>씬 진입 시 특정 대사부터 이어서 보고 싶을 때 (세이브 데이터 연동 등)</summary>
    public void ResetDialog()
    {
        currentDialogIndex = -1;
        isFinished = false;

        if (dialogs != null && dialogs.Length > 0)
        {
            ShowDialog(0);
        }
    }

    public bool IsFinished => isFinished;
}

