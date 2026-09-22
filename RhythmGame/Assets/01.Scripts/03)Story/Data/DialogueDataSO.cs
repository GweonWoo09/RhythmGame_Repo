using UnityEngine;

/// <summary>
/// 하나의 대화 시퀀스(챕터, 컷씬 등)를 담는 데이터 에셋.
/// 씬에 종속되지 않으므로 여러 씬/챕터에서 재사용하거나 기획자가 독립적으로 관리 가능.
/// </summary>
[CreateAssetMenu(fileName = "NewdialogData", menuName = "Dialog/Dialog Sequence")]
public class DialogueDataSO : ScriptableObject
{
    [Tooltip("이 대화 시퀀스에 포함된 대사 목록 (순서대로 재생)")]
    public DialogLine[] lines;
}
 
/// <summary>
/// 대사 한 줄에 대한 데이터.
/// choices가 비어있으면 다음 줄로 순차 진행, 채워져 있으면 해당 줄에서 멈추고 선택 대기.
/// </summary>
[System.Serializable]
public struct DialogLine
{
    [Tooltip("이 대사를 말하는 캐릭터 데이터")]
    public SpeakerDataSO speaker;

    [Tooltip("speaker.expressions 중 어떤 표정을 보여줄지. SpeakerDataSO의 expressionName과 일치해야 함. 예: \"Happy\"")]
    public string expressionName;

    [Tooltip("화면 어느 위치(슬롯)에 표시할지. DialogUIView의 speakerSlots 인덱스와 대응.")]
    public int screenSlot;

    [TextArea(1, 3)]
    public string dialogText;

    [Header("선택지")]
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
 
    [Tooltip("같은 시퀀스 내에서 점프할 대사 인덱스. targetSequence가 지정되면 targetIndex는 그 시퀀스 안의 인덱스로 쓰인다. #비워두면 즉시 대화 끝")]
    public int targetIndex;
 
    [Tooltip("이 값이 지정되면 다른 대화 시퀀스(예: 다른 스토리 분기)로 전환한다.")]
    public DialogueDataSO targetSequence;
 
    [Tooltip("이 선택을 기록할 플래그 키. 비워두면 플래그를 남기지 않음. 예: \"chapter2_helpedA\"")]
    public string flagKey;
 
    [Tooltip("flagKey에 저장할 값. 보통 0/1이지만 임의의 정수도 가능.")]
    public int storyFlagValue;
}