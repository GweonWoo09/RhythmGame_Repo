using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// DialogueManager의 이벤트를 구독해서 실제 대화창 UI(이미지, 텍스트, 캐릭터 알파)를
/// 갱신하는 역할만 담당한다. 대화 진행 로직은 갖지 않는다.
/// </summary>
public class DialogueUIView : MonoBehaviour
{
    [Header("연동할 로직 컴포넌트")]
    [SerializeField] private DialogueManager dialogueManager;

    [Header("스피커별 UI 참조 (dialogs의 speakerIndex와 순서 일치)")]
    [SerializeField] private SpeakerData[] speakers;

    [Header("대화 종료 시 활성화/비활성화할 오브젝트 (선택)")]
    [SerializeField] private GameObject dialogBoxRoot;

    private int _activeSpeakerIndex = -1;

    private void OnEnable()
    {
        if (dialogueManager == null) return;

        dialogueManager.OnDialogUpdated += HandleDialogUpdated;
        dialogueManager.OnDialogEnd += HandleDialogEnd;
    }

    private void OnDisable()
    {
        if (dialogueManager == null) return;

        dialogueManager.OnDialogUpdated -= HandleDialogUpdated;
        dialogueManager.OnDialogEnd -= HandleDialogEnd;
    }

    private void Start()
    {
        foreach (var speaker in speakers)
        {
            SetActiveObject(speaker, false);
        }
    }

    private void Update()
    {
        // 입력 처리는 View 쪽에서 받아 로직에 "다음으로" 신호만 전달
        if (Input.GetKeyDown(KeyCode.Space))
        {
            dialogueManager.AdvanceDialog();
        }
    }

    private void HandleDialogUpdated(int speakerIndex, string speakerName, string dialogText)
    {
        if (speakerIndex < 0 || speakerIndex >= speakers.Length)
        {
            Debug.LogWarning($"[DialogUIView] speakerIndex({speakerIndex})가 speakers 배열 범위를 벗어났습니다.");
            return;
        }

        // 이전에 말하던 화자는 비활성화 (다른 화자로 넘어간 경우에만)
        if (_activeSpeakerIndex != -1 && _activeSpeakerIndex != speakerIndex)
        {
            SetActiveObject(speakers[_activeSpeakerIndex], false);
        }

        _activeSpeakerIndex = speakerIndex;

        var speaker = speakers[speakerIndex];
        SetActiveObject(speaker, true);
        speaker.textName.text = speakerName;
        speaker.textDialog.text = dialogText;
    }

    private void HandleDialogEnd()
    {
        // 대화 종료: 모든 화자 UI를 정리하고 대화창을 닫는다
        foreach (var speaker in speakers)
        {
            SetActiveObject(speaker, false);
        }

        if (dialogBoxRoot != null)
        {
            dialogBoxRoot.SetActive(false);
        }
    }

    private void SetActiveObject(SpeakerData speaker, bool active)
    {
        speaker.dialogImage.gameObject.SetActive(active);
        speaker.textName.gameObject.SetActive(active);
        speaker.textDialog.gameObject.SetActive(active);

        // 말하고 있는 캐릭터 알파값 변경 (0: 투명, 1: 불투명)
        Color color = speaker.spriteRenderer.color;
        color.a = active ? 1f : 0f;
        speaker.spriteRenderer.color = color;
    }
}

[System.Serializable]
public struct SpeakerData
{
    public Image spriteRenderer;
    public TextMeshProUGUI textName;
    public TextMeshProUGUI textDialog;
    public Image dialogImage;
}