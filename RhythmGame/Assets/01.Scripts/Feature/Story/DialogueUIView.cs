using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// DialogueManager의 이벤트를 구독해서 실제 대화창 UI(이미지, 텍스트, 캐릭터 밝기, 선택지 버튼)를
/// 갱신하는 역할을 담당한다.
/// 캐릭터 이름/표정 스프라이트는 SpeakerDataSO에서 가져오고, 여기서는 "화면 어느 위치에 그릴지"만 관리한다.
/// 오토 모드: 타이핑이 끝난 뒤 지정된 시간이 지나면 자동으로 다음 대사로 진행한다.
///
/// [위치 슬롯]  speakerSlots 배열은 [0]=Left(왼쪽), [1]=Right(오른쪽), [2]=Center(가운데) 순서로 구성한다.
///             (DialogueSlots 참고. 가운데 슬롯이 필요 없으면 요소를 2개만 둬도 된다.)
///
/// [무대(cast)] 시퀀스의 cast에 등록된 인물은 계속 화면에 서 있고, 말하는 슬롯만 밝게(Bright), 나머지는 어둡게(Dim).
///             cast에 없는 슬롯은 말할 때만 보인다. 연출 명령으로 도중에 넣고 뺄 수 있다.
///               hide:left            해당 슬롯 인물을 내린다
///               show:right           해당 슬롯 인물을 무대에 올린다 (그 슬롯의 현재 스프라이트 그대로)
///               show:right:Red:Happy 인물(speakerId)과 표정까지 지정해서 올린다 (표정 생략 가능)
///
/// [대사 스타일] DialogLine.style 이름에 맞춰 글꼴 스타일/색을 적용한다.
///             textStyles에 정의한 것이 우선이고, 없으면 기본 제공: thought(기울임), shout(굵게), whisper(기울임+흐리게).
///
/// [표시 이름]  DialogueLineInfo.displayName(변수 치환 완료)을 이름칸에 그대로 쓴다.
/// [표정]      expression이 비어 있으면 경고 없이 첫 번째 표정을 쓴다.
/// [내레이션]  speaker가 없는 줄은 narrationBoxRoot/narrationText에 표시한다.
/// </summary>
public class DialogueUIView : MonoBehaviour
{
    private enum PortraitState { Hidden, Dim, Bright }

    [Header("연동할 로직 컴포넌트")]
    [SerializeField] private DialogueManager dialogueManager;

    [Header("화면 슬롯: [0]=Left, [1]=Right, [2]=Center 순서 (DialogLine.screenSlot 인덱스와 대응)")]
    [SerializeField] private SpeakerSlotUI[] speakerSlots;

    [Header("무대(cast) 연출")]
    [Tooltip("말하지 않는 인물(cast)의 색. 알파는 무시되고 항상 불투명으로 표시된다.")]
    [SerializeField] private Color dimTint = new Color(0.55f, 0.55f, 0.55f, 1f);

    [Header("내레이션 (화자 없는 대사). 연결하지 않으면 내레이션 줄은 표시되지 않는다.")]
    [SerializeField] private GameObject narrationBoxRoot;
    [SerializeField] private TextMeshProUGUI narrationText;

    [Header("대사 스타일 (DialogLine.style 이름과 연결)")]
    [Tooltip("여기에 정의한 이름이 우선한다. 없는 이름은 기본 제공(thought, shout, whisper)을 쓴다.")]
    [SerializeField] private DialogueTextStyle[] textStyles;

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

    // 현재 말하는 슬롯 (-1: 내레이션 또는 아직 없음)
    private int _speakingSlot = -1;

    // 무대에 서 있는 슬롯들 (cast 등록 인물 + show 명령으로 올린 인물)
    private readonly HashSet<int> _castSlots = new HashSet<int>();

    private Coroutine _typingCoroutine;
    private TextMeshProUGUI _typingTarget;
    private bool _isTyping = false;
    private string _currentFullText = "";

    // 스타일 적용 전의 원래 글꼴 스타일/색 (스타일 없는 줄에서 되돌리기 위해 기억)
    private readonly Dictionary<TextMeshProUGUI, FontStyles> _defaultFontStyles = new Dictionary<TextMeshProUGUI, FontStyles>();
    private readonly Dictionary<TextMeshProUGUI, Color> _defaultColors = new Dictionary<TextMeshProUGUI, Color>();
    private readonly HashSet<string> _warnedStyles = new HashSet<string>();

    // 타이핑이 끝나기를 기다리는 선택지 (타이핑 중에는 버튼을 띄우지 않는다)
    private DialogChoice[] _pendingChoices;

    private bool _isAutoMode = false;
    private Coroutine _autoAdvanceCoroutine;

    // 초기화는 Awake에서 한다. 모든 Awake는 어떤 Start보다 먼저 끝나므로,
    // DialogueManager.Start()가 첫 대사를 띄우는 시점에는 UI 초기화가 이미 끝나 있다.
    private void Awake()
    {
        foreach (var slot in speakerSlots)
        {
            CaptureTextDefaults(slot.textDialogue);
            SetPortraitState(slot, PortraitState.Hidden);
            SetDialogBoxActive(slot, false);
        }

        CaptureTextDefaults(narrationText);

        if (choicePanelRoot != null)
        {
            choicePanelRoot.SetActive(false);
        }

        if (narrationBoxRoot != null)
        {
            narrationBoxRoot.SetActive(false);
        }

        if (autoButton != null)
        {
            autoButton.onClick.AddListener(ToggleAutoMode);
        }

        SetAutoModeIndicator(false);
    }

    private void OnDestroy()
    {
        if (autoButton != null)
        {
            autoButton.onClick.RemoveListener(ToggleAutoMode);
        }
    }

    private void OnEnable()
    {
        if (dialogueManager == null) return;

        dialogueManager.OnLineShown += HandleLineShown;
        dialogueManager.OnChoicesPresented += HandleChoicesPresented;
        dialogueManager.OnDialogEnd += HandleDialogEnd;
        dialogueManager.OnCastUpdated += HandleCastUpdated;
        dialogueManager.OnCommand += HandleCommand;
    }

    private void OnDisable()
    {
        if (dialogueManager == null) return;

        dialogueManager.OnLineShown -= HandleLineShown;
        dialogueManager.OnChoicesPresented -= HandleChoicesPresented;
        dialogueManager.OnDialogEnd -= HandleDialogEnd;
        dialogueManager.OnCastUpdated -= HandleCastUpdated;
        dialogueManager.OnCommand -= HandleCommand;
    }

    private void Update()
    {
        if (dialogueManager == null) return;
        if (!Input.GetKeyDown(KeyCode.Space)) return;

        // 타이핑 중이면 선택지 줄이든 일반 줄이든 항상 스킵 가능
        if (_isTyping)
        {
            SkipTyping();
            return;
        }

        // 타이핑이 끝난 뒤 선택지 대기 중이면 Space로는 진행하지 않는다 (버튼으로만 진행)
        if (dialogueManager.IsWaitingForChoice) return;

        // 플레이어가 직접 넘겼으니 진행 중이던 오토 타이머는 취소
        CancelAutoAdvance();
        dialogueManager.AdvanceDialog();
    }

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

    #region Cast / Commands
    /// <summary>시퀀스가 시작/교체될 때: 무대에 서 있을 인물을 새로 배치한다 (이전 cast는 내린다).</summary>
    private void HandleCastUpdated(DialogueCast[] cast)
    {
        foreach (int slotIndex in _castSlots)
        {
            if (IsValidSlot(slotIndex))
            {
                SetPortraitState(speakerSlots[slotIndex], PortraitState.Hidden);
            }
        }

        _castSlots.Clear();

        if (cast == null) return;

        foreach (var member in cast)
        {
            if (!IsValidSlot(member.slot))
            {
                Debug.LogWarning($"[DialogueUIView] cast의 {SlotWarning(member.slot)}");
                continue;
            }

            if (member.speaker == null) continue;

            speakerSlots[member.slot].characterPortrait.sprite = GetPortraitSprite(member.speaker, member.expressionName);
            _castSlots.Add(member.slot);
        }

        RefreshPortraits();
    }

    /// <summary>줄에 적힌 명령 중 화면 표시와 관련된 것(show, hide)만 처리한다. 나머지(bg, sfx 등)는 다른 구독자 몫.</summary>
    private void HandleCommand(string name, string arg)
    {
        switch (name)
        {
            case "hide":
                HandleHideCommand(arg);
                break;

            case "show":
                HandleShowCommand(arg);
                break;
        }
    }

    private void HandleHideCommand(string arg)
    {
        int slotIndex;
        if (!DialogueSlots.TryParse(arg, out slotIndex) || !IsValidSlot(slotIndex))
        {
            Debug.LogWarning($"[DialogueUIView] 'hide:{arg}' 명령의 슬롯을 해석할 수 없습니다. (예: hide:left)");
            return;
        }

        _castSlots.Remove(slotIndex);
        RefreshPortraits();
    }

    /// <summary>show:슬롯  또는  show:슬롯:speakerId[:expression]</summary>
    private void HandleShowCommand(string arg)
    {
        string[] parts = arg.Split(':');

        int slotIndex;
        if (!DialogueSlots.TryParse(parts[0], out slotIndex) || !IsValidSlot(slotIndex))
        {
            Debug.LogWarning($"[DialogueUIView] 'show:{arg}' 명령의 슬롯을 해석할 수 없습니다. (예: show:right)");
            return;
        }

        if (parts.Length >= 2 && parts[1].Trim().Length > 0)
        {
            string speakerId = parts[1].Trim();
            SpeakerDataSO speaker = DialogueJsonLoader.FindSpeaker(speakerId);

            if (speaker == null)
            {
                Debug.LogWarning($"[DialogueUIView] 'show:{arg}' 명령의 speaker '{speakerId}' 에셋을 찾을 수 없습니다.");
            }
            else
            {
                string expression = parts.Length >= 3 ? parts[2].Trim() : "";
                speakerSlots[slotIndex].characterPortrait.sprite = GetPortraitSprite(speaker, expression);
            }
        }

        _castSlots.Add(slotIndex);
        RefreshPortraits();
    }

    /// <summary>현재 말하는 슬롯 / 무대에 선 슬롯 / 그 외에 따라 모든 초상화의 상태를 다시 맞춘다.</summary>
    private void RefreshPortraits()
    {
        for (int i = 0; i < speakerSlots.Length; i++)
        {
            PortraitState state;

            if (i == _speakingSlot) state = PortraitState.Bright;
            else if (_castSlots.Contains(i)) state = PortraitState.Dim;
            else state = PortraitState.Hidden;

            SetPortraitState(speakerSlots[i], state);
        }
    }
    #endregion

    private void HandleLineShown(DialogueLineInfo info)
    {
        CancelAutoAdvance();

        // 새 대사가 나오면 이전 선택지는 모두 정리한다
        _pendingChoices = null;
        StopTyping();

        if (choicePanelRoot != null)
        {
            choicePanelRoot.SetActive(false);
        }

        // 화자가 없으면 내레이션
        if (info.speaker == null)
        {
            ShowNarration(info.text, info.style);
            return;
        }

        if (!IsValidSlot(info.screenSlot))
        {
            Debug.LogWarning($"[DialogueUIView] 대사의 {SlotWarning(info.screenSlot)}");
            return;
        }

        if (narrationBoxRoot != null)
        {
            narrationBoxRoot.SetActive(false);
        }

        // 말풍선은 말하는 슬롯 것만 켠다
        for (int i = 0; i < speakerSlots.Length; i++)
        {
            SetDialogBoxActive(speakerSlots[i], i == info.screenSlot);
        }

        _speakingSlot = info.screenSlot;
        RefreshPortraits();

        var slot = speakerSlots[info.screenSlot];
        slot.textName.text = info.displayName;
        slot.characterPortrait.sprite = GetPortraitSprite(info.speaker, info.expressionName);

        ApplyTextStyle(slot.textDialogue, info.style);
        StartTyping(slot.textDialogue, info.text);
    }

    private void ShowNarration(string text, string styleName)
    {
        if (narrationText == null)
        {
            Debug.LogWarning("[DialogueUIView] 화자가 없는 줄(내레이션)인데 narrationText가 연결되지 않았습니다.");
            return;
        }

        foreach (var slot in speakerSlots)
        {
            SetDialogBoxActive(slot, false);
        }

        if (narrationBoxRoot != null)
        {
            narrationBoxRoot.SetActive(true);
        }
        narrationText.gameObject.SetActive(true);

        _speakingSlot = -1;
        RefreshPortraits();

        ApplyTextStyle(narrationText, styleName);
        StartTyping(narrationText, text);
    }

    #region Text Style
    private void CaptureTextDefaults(TextMeshProUGUI target)
    {
        if (target == null || _defaultFontStyles.ContainsKey(target)) return;

        _defaultFontStyles[target] = target.fontStyle;
        _defaultColors[target] = target.color;
    }

    /// <summary>스타일 이름에 맞춰 글꼴 스타일/색을 적용한다. 이름이 비어 있으면 원래 모습으로 되돌린다.</summary>
    private void ApplyTextStyle(TextMeshProUGUI target, string styleName)
    {
        if (target == null) return;

        CaptureTextDefaults(target);

        FontStyles baseFontStyle = _defaultFontStyles[target];
        target.fontStyle = baseFontStyle;
        target.color = _defaultColors[target];

        if (string.IsNullOrEmpty(styleName)) return;

        DialogueTextStyle style;
        if (!TryGetStyle(styleName, out style))
        {
            if (_warnedStyles.Add(styleName))
            {
                Debug.LogWarning($"[DialogueUIView] 알 수 없는 대사 스타일 '{styleName}'입니다. " +
                                 "textStyles에 정의하거나 기본 제공(thought, shout, whisper)을 쓰세요.");
            }
            return;
        }

        target.fontStyle = baseFontStyle | style.fontStyle;

        if (style.overrideColor)
        {
            target.color = style.color;
        }
    }

    private bool TryGetStyle(string styleName, out DialogueTextStyle style)
    {
        if (textStyles != null)
        {
            foreach (var candidate in textStyles)
            {
                if (candidate.name == styleName)
                {
                    style = candidate;
                    return true;
                }
            }
        }

        // 기본 제공 스타일
        switch (styleName)
        {
            case "thought":
                style = new DialogueTextStyle { name = styleName, fontStyle = FontStyles.Italic };
                return true;

            case "shout":
                style = new DialogueTextStyle { name = styleName, fontStyle = FontStyles.Bold };
                return true;

            case "whisper":
                style = new DialogueTextStyle
                {
                    name = styleName,
                    fontStyle = FontStyles.Italic,
                    overrideColor = true,
                    color = new Color(0.75f, 0.75f, 0.75f, 1f)
                };
                return true;
        }

        style = default(DialogueTextStyle);
        return false;
    }
    #endregion

    #region Typing
    private void StartTyping(TextMeshProUGUI textComponent, string fullText)
    {
        StopTyping();

        _typingTarget = textComponent;
        _currentFullText = fullText;
        _typingCoroutine = StartCoroutine(TypeTextRoutine(textComponent, fullText));
    }

    private void StopTyping()
    {
        if (_typingCoroutine != null)
        {
            StopCoroutine(_typingCoroutine);
            _typingCoroutine = null;
        }

        _isTyping = false;
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

        OnTypingFinished();
    }

    /// <summary>Space로 스킵했을 때, 타이핑 중이던 텍스트를 즉시 전체 출력으로 완성</summary>
    private void SkipTyping()
    {
        StopTyping();

        if (_typingTarget != null)
        {
            _typingTarget.text = _currentFullText;
        }

        OnTypingFinished();
    }

    /// <summary>
    /// 타이핑이 끝난 시점(자연 종료 / 스킵 공통)에 해야 할 일.
    /// 선택지가 대기 중이면 버튼을 띄우고, 아니면 오토 모드 타이머를 시작한다.
    /// </summary>
    private void OnTypingFinished()
    {
        if (_pendingChoices != null)
        {
            ShowPendingChoices();
            return;
        }

        if (_isAutoMode && !dialogueManager.IsWaitingForChoice)
        {
            StartAutoAdvanceTimer();
        }
    }
    #endregion

    #region Choices
    private void HandleChoicesPresented(DialogChoice[] choices)
    {
        CancelAutoAdvance();

        _pendingChoices = choices;

        // 대사가 아직 타이핑 중이면 끝날 때까지 기다렸다가 띄운다 (OnTypingFinished에서 처리)
        if (!_isTyping)
        {
            ShowPendingChoices();
        }
    }

    private void ShowPendingChoices()
    {
        var choices = _pendingChoices;
        _pendingChoices = null;

        if (choices == null || choicePanelRoot == null || choiceButtons == null) return;

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

        if (choices.Length > choiceButtons.Length)
        {
            Debug.LogWarning($"[DialogueUIView] 선택지({choices.Length}개)가 choiceButtons({choiceButtons.Length}개)보다 많아 일부가 표시되지 않습니다.");
        }
    }
    #endregion

    private void HandleDialogEnd()
    {
        CancelAutoAdvance();
        StopTyping();
        _pendingChoices = null;

        // 대화 종료: 모든 슬롯 UI를 정리하고 대화창을 닫는다
        _castSlots.Clear();
        _speakingSlot = -1;

        foreach (var slot in speakerSlots)
        {
            SetDialogBoxActive(slot, false);
        }

        RefreshPortraits();

        if (narrationBoxRoot != null)
        {
            narrationBoxRoot.SetActive(false);
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

    #region Slot Helpers
    private bool IsValidSlot(int slotIndex)
    {
        return speakerSlots != null && slotIndex >= 0 && slotIndex < speakerSlots.Length;
    }

    private string SlotWarning(int slotIndex)
    {
        return $"슬롯 {DialogueSlots.Describe(slotIndex)}에 해당하는 speakerSlots 요소가 없습니다. " +
               $"speakerSlots는 [0]=Left, [1]=Right, [2]=Center 순서로 구성해야 합니다. (현재 {(speakerSlots != null ? speakerSlots.Length : 0)}개)";
    }

    /// <summary>말풍선(이름, 대사, 배경)의 표시 여부. 캐릭터 초상화는 건드리지 않는다.</summary>
    private void SetDialogBoxActive(SpeakerSlotUI slot, bool active)
    {
        slot.dialogImage.gameObject.SetActive(active);
        slot.textName.gameObject.SetActive(active);
        slot.textDialogue.gameObject.SetActive(active);
    }

    /// <summary>캐릭터 초상화의 표시 상태. Hidden: 투명, Dim: 어둡게, Bright: 원래 색.</summary>
    private void SetPortraitState(SpeakerSlotUI slot, PortraitState state)
    {
        Color color = slot.characterPortrait.color;

        switch (state)
        {
            case PortraitState.Hidden:
                color.a = 0f;
                break;
            case PortraitState.Dim:
                color = new Color(dimTint.r, dimTint.g, dimTint.b, 1f);
                break;
            case PortraitState.Bright:
                color = Color.white;
                break;
        }

        slot.characterPortrait.color = color;
    }

    /// <summary>표정 이름이 비어있으면 경고 없이 첫 번째 표정을 사용한다.</summary>
    private static Sprite GetPortraitSprite(SpeakerDataSO speaker, string expressionName)
    {
        if (!string.IsNullOrEmpty(expressionName))
        {
            return speaker.GetSprite(expressionName);
        }

        return (speaker.expressions != null && speaker.expressions.Length > 0)
            ? speaker.expressions[0].sprite
            : null;
    }
    #endregion
}

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

/// <summary>대사 스타일 한 가지. DialogLine.style의 이름과 연결된다.</summary>
[System.Serializable]
public struct DialogueTextStyle
{
    [Tooltip("DialogLine.style에 적는 이름. 예: thought")]
    public string name;

    [Tooltip("적용할 글꼴 스타일 (기본 글꼴 스타일에 더해진다)")]
    public FontStyles fontStyle;

    [Tooltip("체크하면 아래 색으로 대사 글자색을 바꾼다")]
    public bool overrideColor;

    public Color color;
}