using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면의 한 위치(슬롯)에 대응하는 씬 UI 참조 묶음.
/// 어떤 캐릭터가 여기에 표시될지는 DialogLine.speaker(SpeakerDataSO)가 매 대사마다 결정한다.
/// </summary>
[System.Serializable]
public struct SpeakerSlotUI
{
    [Tooltip("표정 스프라이트가 그려질 캐릭터 이미지")]
    public Image characterPortrait;
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
/// 캐릭터 이름/표정 스프라이트는 SpeakerDataSO에서 가져오고, 여기서는 "화면 어느 슬롯에 그릴지"만 관리한다.
/// </summary>
public class DialogueUIView : MonoBehaviour
{
    [Header("연동할 로직 컴포넌트")]
    [SerializeField] private DialogueManager dialogueManager;

    [Header("화면 슬롯 (위치별 UI 참조, DialogLine.screenSlot 인덱스와 대응)")]
    [SerializeField] private SpeakerSlotUI[] speakerSlots;

    [Header("대화 종료 시 활성화/비활성화할 오브젝트 (선택)")]
    [SerializeField] private GameObject dialogBoxRoot;

    [Header("선택지 UI")]
    [SerializeField] private GameObject choicePanelRoot;
    [SerializeField] private ChoiceButtonRef[] choiceButtons; // 최대 선택지 개수만큼 씬에 미리 배치

    [Header("타이핑 효과")]
    [Tooltip("한 글자를 출력하는 간격 (초)")]
    [SerializeField] private float charInterval = 0.03f;

    [Header("오토 모드")]
    [SerializeField] private Button autoButton;
    [Tooltip("타이핑이 끝난 뒤 자동으로 다음 대사로 넘어가기까지의 대기 시간 (초)")]
    [SerializeField] private float autoAdvanceDelay = 5f;
    [Tooltip("오토 모드가 켜졌을 때 표시할 오브젝트 (버튼 강조 표시 등, 선택)")]
    [SerializeField] private GameObject autoModeIndicator;

    private int _activeSlotIndex = -1;
    private Coroutine _typingCoroutine;
    private bool _isTyping = false;
    private string _currentFullText = "";

    private bool _isAutoMode = false;
    private Coroutine _autoAdvanceCoroutine;

    private void OnEnable()
    {
        if (dialogueManager == null) return;

        dialogueManager.OnDialogUpdated += HandleDialogUpdated;
        dialogueManager.OnChoicesPresented += HandleChoicesPresented;
        dialogueManager.OnDialogEnd += HandleDialogEnd;
    }

    private void OnDisable()
    {
        if (dialogueManager == null) return;

        dialogueManager.OnDialogUpdated -= HandleDialogUpdated;
        dialogueManager.OnChoicesPresented -= HandleChoicesPresented;
        dialogueManager.OnDialogEnd -= HandleDialogEnd;
    }

    private void Start()
    {
        foreach (var slot in speakerSlots)
        {
            SetSlotActive(slot, false);
        }

        if (choicePanelRoot != null)
        {
            choicePanelRoot.SetActive(false);
        }

        if (autoButton != null)
        {
            autoButton.onClick.AddListener(ToggleAutoMode);
        }

        SetAutoModeIndicator(false);
    }

    private void Update()
    {
        // 선택지 대기 중에는 Space로 아무것도 하지 않는다
        if (dialogueManager.IsWaitingForChoice) return;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (_isTyping)
            {
                SkipTyping();
            }
            else
            {
                // 플레이어가 직접 넘겼으니 진행 중이던 오토 타이머는 취소
                CancelAutoAdvance();
                dialogueManager.AdvanceDialog();
            }
        }
    }

    /// <summary>
    /// 오토 모드: 타이핑이 끝난 뒤 지정된 시간이 지나면 자동으로 다음 대사로 진행한다.
    /// </summary>
    #region Auto Mode
    private void ToggleAutoMode()
    {
        _isAutoMode = !_isAutoMode;
        SetAutoModeIndicator(_isAutoMode);

        if (!_isAutoMode)
        {
            CancelAutoAdvance();
        }
        else if (!_isTyping && !dialogueManager.IsWaitingForChoice)
        {
            // 이미 타이핑이 끝난 상태에서 오토 모드를 켰다면 바로 타이머 시작
            StartAutoAdvanceTimer();
        }
    }

    private void StartAutoAdvanceTimer()
    {
        CancelAutoAdvance();
        _autoAdvanceCoroutine = StartCoroutine(AutoAdvanceRoutine());
    }

    private void CancelAutoAdvance()
    {
        if (_autoAdvanceCoroutine != null)
        {
            StopCoroutine(_autoAdvanceCoroutine);
            _autoAdvanceCoroutine = null;
        }
    }

    private IEnumerator AutoAdvanceRoutine()
    {
        yield return new WaitForSeconds(autoAdvanceDelay);

        _autoAdvanceCoroutine = null;

        if (_isAutoMode && !dialogueManager.IsWaitingForChoice)
        {
            dialogueManager.AdvanceDialog();
        }
    }

    private void SetAutoModeIndicator(bool active)
    {
        if (autoModeIndicator != null)
        {
            autoModeIndicator.SetActive(active);
        }
    }
    #endregion

    private void HandleDialogUpdated(int screenSlot, SpeakerDataSO speaker, string expressionName, string dialogText)
    {
        CancelAutoAdvance();

        // 새 대사가 나오면 이전 선택지 패널은 닫아둔다
        if (choicePanelRoot != null)
        {
            choicePanelRoot.SetActive(false);
        }

        if (screenSlot < 0 || screenSlot >= speakerSlots.Length)
        {
            Debug.LogWarning($"[DialogueUIView] screenSlot({screenSlot})이 speakerSlots 배열 범위를 벗어났습니다.");
            return;
        }

        if (speaker == null)
        {
            Debug.LogWarning("[DialogueUIView] DialogLine에 speaker(SpeakerDataSO)가 지정되지 않았습니다.");
            return;
        }

        // 이전에 말하던 슬롯은 비활성화 (다른 슬롯으로 넘어간 경우에만)
        if (_activeSlotIndex != -1 && _activeSlotIndex != screenSlot)
        {
            SetSlotActive(speakerSlots[_activeSlotIndex], false);
        }

        _activeSlotIndex = screenSlot;

        var slot = speakerSlots[screenSlot];
        SetSlotActive(slot, true);

        slot.textName.text = speaker.speakerName;
        slot.characterPortrait.sprite = speaker.GetSprite(expressionName);

        StartTyping(slot.textDialogue, dialogText);
    }

    private void StartTyping(TextMeshProUGUI textComponent, string fullText)
    {
        if (_typingCoroutine != null)
        {
            StopCoroutine(_typingCoroutine);
        }

        _currentFullText = fullText;
        _typingCoroutine = StartCoroutine(TypeTextRoutine(textComponent, fullText));
    }

    private IEnumerator TypeTextRoutine(TextMeshProUGUI textComponent, string fullText)
    {
        _isTyping = true;
        textComponent.text = "";

        foreach (char c in fullText)
        {
            textComponent.text += c;
            yield return new WaitForSeconds(charInterval);
        }

        _isTyping = false;
        _typingCoroutine = null;

        // 타이핑이 끝난 시점부터 오토 진행 카운트 시작
        if (_isAutoMode && !dialogueManager.IsWaitingForChoice)
        {
            StartAutoAdvanceTimer();
        }
    }

    /// <summary>Space로 스킵했을 때, 타이핑 중이던 텍스트를 즉시 전체 출력으로 완성</summary>
    private void SkipTyping()
    {
        if (_typingCoroutine != null)
        {
            StopCoroutine(_typingCoroutine);
            _typingCoroutine = null;
        }

        _isTyping = false;

        if (_activeSlotIndex != -1)
        {
            speakerSlots[_activeSlotIndex].textDialogue.text = _currentFullText;
        }

        // 스킵도 "다 읽었다"고 보고 오토 타이머 시작
        if (_isAutoMode && !dialogueManager.IsWaitingForChoice)
        {
            StartAutoAdvanceTimer();
        }
    }

    private void HandleChoicesPresented(DialogChoice[] choices)
    {
        CancelAutoAdvance();

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
                choiceButtons[i].button.onClick.AddListener(() =>
                {
                    CancelAutoAdvance();
                    dialogueManager.SelectChoice(choiceIndex);
                });
            }
            else
            {
                choiceButtons[i].root.SetActive(false);
            }
        }
    }

    private void HandleDialogEnd()
    {
        CancelAutoAdvance();

        if (_typingCoroutine != null)
        {
            StopCoroutine(_typingCoroutine);
            _typingCoroutine = null;
            _isTyping = false;
        }

        // 대화 종료: 모든 슬롯 UI를 정리하고 대화창을 닫는다
        foreach (var slot in speakerSlots)
        {
            SetSlotActive(slot, false);
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

    private void SetSlotActive(SpeakerSlotUI slot, bool active)
    {
        slot.dialogImage.gameObject.SetActive(active);
        slot.textName.gameObject.SetActive(active);
        slot.textDialogue.gameObject.SetActive(active);

        // 캐릭터 스프라이트 알파값 변경 (0: 투명, 1: 불투명)
        Color color = slot.characterPortrait.color;
        color.a = active ? 1f : 0f;
        slot.characterPortrait.color = color;
    }
}