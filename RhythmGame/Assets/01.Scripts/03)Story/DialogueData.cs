using UnityEngine;

/// <summary>
/// 하나의 대화 시퀀스(챕터, 컷씬 등)를 담는 데이터 에셋.
/// 씬에 종속되지 않으므로 여러 씬/챕터에서 재사용하거나 기획자가 독립적으로 관리 가능.
/// </summary>
[CreateAssetMenu(fileName = "NewdialogData", menuName = "Dialog/Dialog Sequence")]
public class DialogueData : ScriptableObject
{
    [Tooltip("이 대화 시퀀스에 포함된 대사 목록 (순서대로 재생)")]
    public DialogLine[] lines;
}

/// <summary>
/// 대사 한 줄에 대한 데이터.
/// </summary>
[System.Serializable]
public struct DialogLine
{
    public int speakerIndex;
    public string speakerName;

    [TextArea(1, 3)]
    public string dialogText;

    [Tooltip("비어있으면 일반 대사(다음 줄로 진행). 채워지면 이 줄에서 멈추고 선택지를 보여준다.")]
    public DialogChoice[] choices;
}

/// <summary>
/// 하나의 선택지. 선택 시 같은 시퀀스 내 다른 인덱스로 점프하거나,
/// targetSequence가 지정된 경우 아예 다른 대화 시퀀스(챕터 분기)로 이동한다.
/// </summary>
[System.Serializable]
public struct DialogChoice
{
    public string choiceText;

    [Tooltip("같은 시퀀스 내에서 점프할 대사 인덱스. targetSequence가 지정되면 무시되고 targetIndex는 그 시퀀스 안의 인덱스로 쓰인다.")]
    public int targetIndex;

    [Tooltip("선택), 이 값이 지정되면 다른 대화 시퀀스(예: 다른 스토리 분기)로 전환한다.")]
    public DialogueData targetSequence;

    [Tooltip("이 선택으로 스토리 플래그를 설정하고 싶을 때 사용. -1이면 설정하지 않음.")]
    public int storyFlagValue;
}