using UnityEngine;

/// <summary>
/// 하나의 대화 시퀀스(챕터, 컷씬 등)를 담는 데이터 에셋.
/// Project 창에서 우클릭 -> Create -> Dialog -> Dialog Sequence 로 생성.
/// 씬에 종속되지 않으므로 여러 씬/챕터에서 재사용하거나 기획자가 독립적으로 관리 가능.
/// </summary>
[CreateAssetMenu(fileName = "NewdialogData", menuName = "Dialog/Dialog Sequence")]
public class DialogueData : ScriptableObject
{
    [Tooltip("이 대화 시퀀스에 포함된 대사 목록 (순서대로 재생)")]
    public DialogLine[] lines;
}

/// <summary>
/// 대사 한 줄에 대한 데이터. (기존 DialogData를 이름만 변경 - 의미가 더 명확하도록)
/// </summary>
[System.Serializable]
public struct DialogLine
{
    public int speakerIndex;
    public string speakerName;

    [TextArea(1, 3)]
    public string dialogText;
}