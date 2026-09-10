using TMPro;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public struct SpeakerData
{
    public Image spriteRenderer;
    public TextMeshProUGUI textName;
    public TextMeshProUGUI textDialogue;
    public Image dialogImage;
}

/// <summary>선택지 버튼 하나에 필요한 UI 참조 묶음</summary>
[System.Serializable]
public struct ChoiceButtonRef
{
    public GameObject root;   // 버튼 전체를 감싸는 오브젝트 (활성/비활성 제어용)
    public Button button;
    public TextMeshProUGUI label;
}

/// <summary>
/// DialogueManager의 이벤트를 구독해서 실제 대화창 UI(이미지, 텍스트, 캐릭터 알파, 선택지 버튼)를
/// 갱신하는 역할만 담당한다. 대화 진행/분기 로직은 갖지 않는다.
/// </summary>
public class DialogueUIView : MonoBehaviour
{
    [Header("연동할 로직 컴포넌트")]
    [SerializeField] private DialogueManager DialogueManager;

    [Header("스피커별 UI 참조 (dialogs의 speakerIndex와 순서 일치)")]
    [SerializeField] private SpeakerData[] speakers;

    [Header("대화 종료 시 활성화/비활성화할 오브젝트 (선택)")]
    [SerializeField] private GameObject dialogBoxRoot;

    [Header("선택지 UI")]
    [SerializeField] private GameObject choicePanelRoot;
    [SerializeField] private ChoiceButtonRef[] choiceButtons; // 최대 선택지 개수만큼 씬에 미리 배치

    private int _activeSpeakerIndex = -1;

    private void OnEnable()
    {
        if (DialogueManager == null) return;

        DialogueManager.OnDialogUpdated += HandleDialogUpdated;
        DialogueManager.OnChoicesPresented += HandleChoicesPresented;
        DialogueManager.OnDialogEnd += HandleDialogEnd;
    }

    private void OnDisable()
    {
        if (DialogueManager == null) return;

        DialogueManager.OnDialogUpdated -= HandleDialogUpdated;
        DialogueManager.OnChoicesPresented -= HandleChoicesPresented;
        DialogueManager.OnDialogEnd -= HandleDialogEnd;
    }

    private void Start()
    {
        foreach (var speaker in speakers)
        {
            SetActiveObject(speaker, false);
        }

        if (choicePanelRoot != null)
        {
            choicePanelRoot.SetActive(false);
        }
    }

    private void Update()
    {
        // 선택지 대기 중에는 Space로 진행하지 않는다 (AdvanceDialog 쪽에서도 막히지만 이중 방지)
        if (DialogueManager.IsWaitingForChoice) return;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            DialogueManager.AdvanceDialog();
        }
    }

    private void HandleDialogUpdated(int speakerIndex, string speakerName, string dialogText)
    {
        // 새 대사가 나오면 이전 선택지 패널은 닫아둔다
        if (choicePanelRoot != null)
        {
            choicePanelRoot.SetActive(false);
        }

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
        speaker.textDialogue.text = dialogText;
    }

    private void HandleChoicesPresented(DialogChoice[] choices)
    {
        if (choicePanelRoot == null || choiceButtons == null) return;

        choicePanelRoot.SetActive(true);

        for (int i = 0; i < choiceButtons.Length; i++)
        {
            if (i < choices.Length)
            {
                int choiceIndex = i; // 클로저 캡처용 지역 변수
                choiceButtons[i].root.SetActive(true);
                choiceButtons[i].label.text = choices[i].choiceText;

                choiceButtons[i].button.onClick.RemoveAllListeners();
                choiceButtons[i].button.onClick.AddListener(() => DialogueManager.SelectChoice(choiceIndex));
            }
            else
            {
                choiceButtons[i].root.SetActive(false);
            }
        }
    }

    private void HandleDialogEnd()
    {
        // 대화 종료: 모든 화자 UI를 정리하고 대화창을 닫는다
        foreach (var speaker in speakers)
        {
            SetActiveObject(speaker, false);
        }

        if (choicePanelRoot != null)
        {
            choicePanelRoot.SetActive(false);
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
        speaker.textDialogue.gameObject.SetActive(active);

        // 말하고 있는 캐릭터 알파값 변경 (0: 투명, 1: 불투명)
        Color color = speaker.spriteRenderer.color;
        color.a = active ? 1f : 0f;
        speaker.spriteRenderer.color = color;
    }
}