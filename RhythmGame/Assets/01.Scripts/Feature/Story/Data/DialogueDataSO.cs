using UnityEngine;

/// <summary>
/// 하나의 대화 시퀀스(챕터, 컷씬 등)를 담는 데이터 에셋.
/// Project 창에서 우클릭 -> Create -> Dialog -> Dialogue Sequence 로 생성.
///
/// [v2 추가]
/// - chapterId : 이 시퀀스가 속한 챕터 (씬 이동 시 payload로 전달)
/// - cast      : 시퀀스 동안 화면에 계속 서 있을 인물 (말하는 쪽만 밝게, 나머지는 어둡게)
/// - onEnd     : 시퀀스가 끝났을 때의 후속 동작 (씬 이동, 챕터 클리어/해금)
/// - 줄 id(라벨) / goto / 조건(if) / setFlags / branch / commands
/// </summary>
[CreateAssetMenu(fileName = "NewDialogueSequence", menuName = "Dialog/Dialogue Sequence")]
public class DialogueDataSO : ScriptableObject
{
    /// <summary>goto 대상으로 쓰면 "여기서 대화 종료"를 뜻하는 예약어</summary>
    public const string EndLabel = "end";

    [Tooltip("이 시퀀스가 속한 챕터 번호. 0이면 지정 안 함.")]
    public int chapterId; // 챕터 ID

    [Tooltip("이 시퀀스 재생 중 기본으로 깔릴 BGM. 비워두면 이전 BGM을 그대로 유지한다.")]
    public AudioClip bgm; // BGM

    [Tooltip("시퀀스 동안 화면에 계속 서 있을 인물. 비어있으면 말하는 사람만 보인다(기존 방식).")]
    public DialogueCast[] cast; // 인물 목록

    [Tooltip("시퀀스가 끝났을 때의 후속 동작. 전부 비우면 아무것도 하지 않는다.")]
    public DialogueEndAction onEnd; // 챕터 끝난 뒤 시행할 동작

    [Tooltip("이 대화 시퀀스에 포함된 대사 목록 (순서대로 재생)")]
    public DialogLine[] lines; // 대사 목록

    /// <summary>줄의 id(라벨)로 인덱스를 찾는다. 없으면 -1.</summary>
    public int FindLabelIndex(string label)
    {
        if (string.IsNullOrEmpty(label) || lines == null) return -1;

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].id == label) return i;
        }

        return -1;
    }
}

/// <summary>시퀀스 동안 무대에 서 있을 인물 한 명</summary>
[System.Serializable]
public struct DialogueCast
{
    [Tooltip("화면 슬롯 번호 (DialogueUIView.speakerSlots 인덱스)")]
    public int slot; // 캐릭터 위치 슬롯

    public SpeakerDataSO speaker; // 캐릭터 정보

    [Tooltip("말하기 전 대기 중에 보여줄 표정. 비우면 첫 번째 표정.")]
    public string expressionName; // 캐릭터 표정
}

/// <summary>시퀀스 종료 후 동작. 문자열/숫자만 써서 JSON에서도 그대로 표현할 수 있다.</summary>
[System.Serializable]
public struct DialogueEndAction
{
    [Tooltip("이동할 씬 (SceneChanger.SceneType 이름, 예: \"SongSelect\"). 비우면 SceneTransitionDialogueEnd의 기본 targetScene 사용.")]
    public string scene; // 이동할 씬

    [Tooltip("0보다 크면 이 챕터를 클리어 처리한다.")]
    public int clearChapter; // 해당 챕터 클리어

    [Tooltip("0보다 크면 이 챕터를 해금한다.")]
    public int unlockChapter; // 해당 챕터 해금

    [Tooltip("씬 이동 시 함께 넘길 곡 ID (선택)")]
    public string songId; // 곡 ID (선택)

    [Tooltip("씬 이동 시 함께 넘길 난이도 (선택)")]
    public string difficulty; // 곡 난이도 (선택)
}

/// <summary>조건부 분기 한 건. 줄의 branches 중 처음으로 조건이 참인 것을 따라간다.</summary>
[System.Serializable]
public struct DialogBranch
{
    [Tooltip("조건식. 비우면 항상 참. 예: \"ch1_eager==1\"")]
    public string condition; // 조건식

    [Tooltip("이동할 줄의 id. \"end\"면 대화 종료.")]
    public string gotoLabel; // 이동할 줄 ID
}

/// <summary>
/// 대사 한 줄에 대한 데이터.
/// choices가 비어있으면 다음 줄(또는 gotoLabel/branches가 가리키는 줄)로 진행,
/// 채워져 있으면 해당 줄에서 멈추고 선택 대기.
/// </summary>
[System.Serializable]
public struct DialogLine
{
    [Tooltip("이 줄의 이름표(라벨). goto 대상이 될 줄에만 적는다. \"end\"는 예약어.")]
    public string id; // 줄 ID

    [Tooltip("조건식. 거짓이면 이 줄을 통째로 건너뛴다. 비우면 항상 재생. 예: \"ch1_eager==1\"")]
    public string condition;

    [Tooltip("이 대사를 말하는 캐릭터. 비워두면 화자 없는 내레이션으로 처리한다.")]
    public SpeakerDataSO speaker;

    [Tooltip("speaker.expressions 중 어떤 표정을 보여줄지. 예: \"Happy\"")]
    public string expressionName;

    [Tooltip("화면 어느 위치(슬롯)에 표시할지. DialogueUIView의 speakerSlots 인덱스와 대응.")]
    public int screenSlot;

    [Tooltip("이 줄에서만 화자 이름 대신 보여줄 이름. 예: \"???\". {player} 같은 변수를 쓸 수 있다. 비우면 speaker.speakerName.")]
    public string displayName;

    [Tooltip("대사 표시 방식의 이름. 예: thought(속마음), shout(외침), whisper(속삭임). DialogueUIView의 textStyles에서 정의한다. 비우면 일반 대사.")]
    public string style;

    [TextArea(1, 3)]
    public string dialogText;

    [Tooltip("이 줄부터 BGM을 바꾸고 싶을 때만 지정. 비워두면 기존 BGM 유지.")]
    public AudioClip bgmOverride;

    [Tooltip("이 줄이 표시될 때 기록할 플래그. 예: \"ch1_eager=1\", \"affinity+=1\"")]
    public string[] setFlags;

    [Tooltip("이 줄이 표시될 때 발행할 명령. \"이름:인자\" 형식. 예: \"hide:0\", \"bg:studio_night\"")]
    public string[] commands;

    [Tooltip("이 줄을 읽고 다음으로 넘어갈 때 조건부로 이동할 곳. 위에서부터 처음 참인 것을 따른다.")]
    public DialogBranch[] branches;

    [Tooltip("branches에 해당하는 게 없을 때(또는 branches가 없을 때) 이동할 줄의 id. 비우면 다음 줄. \"end\"면 종료.")]
    public string gotoLabel;

    [Tooltip("비어있으면 일반 대사. 채워지면 이 줄에서 멈추고 선택지를 보여준다.")]
    public DialogChoice[] choices;
}

/// <summary>
/// 하나의 선택지. 선택 시 같은 시퀀스 내 다른 줄로 점프하거나,
/// targetSequence/targetSequenceId가 지정된 경우 다른 대화 시퀀스로 이동한다.
/// 이동 위치는 gotoLabel(권장)이 우선이고, 없으면 targetIndex를 쓴다.
/// </summary>
[System.Serializable]
public struct DialogChoice
{
    public string choiceText; // 버튼 문구

    [Tooltip("조건식. 거짓이면 이 선택지는 표시되지 않는다. 비우면 항상 표시.")]
    public string condition; // 조건식

    [Tooltip("이동할 줄의 id(권장). 다른 시퀀스로 분기하는 경우 그 시퀀스 안의 id. \"end\"면 대화 종료.")]
    public string gotoLabel; // 이동할 줄 ID

    [Tooltip("gotoLabel이 비었을 때 쓰는 줄 번호(하위 호환용). 다른 시퀀스로 분기하면 그 시퀀스 안의 번호.")]
    public int targetIndex; // 이동할 줄 인덱스 (구버전)

    [Tooltip("이 값이 지정되면 다른 대화 시퀀스(에셋)로 전환한다.")]
    public DialogueDataSO targetSequence; // 이동할 대화 시퀀스 에셋

    [Tooltip("JSON 시퀀스로 분기할 때 파일 이름(확장자 제외). targetSequence가 비어있을 때만 쓰인다.")]
    public string targetSequenceId; // 이동할 대화 시퀀스 JSON

    [Tooltip("이 선택을 고르면 기록할 플래그들. 예: \"ch1_eager=1\"")]
    public string[] setFlags; // 선택 기록 플래그

    [Tooltip("(하위 호환) 단일 플래그 키. 새 작업은 setFlags를 권장.")]
    public string flagKey; // 플래그 (구버전)

    [Tooltip("(하위 호환) flagKey에 저장할 값.")]
    public int storyFlagValue; // 플래그 값 (구버전)
}