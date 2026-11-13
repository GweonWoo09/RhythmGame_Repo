using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// DialogueManager의 이벤트를 구독해서 실제 대화창 UI(이미지, 텍스트, 캐릭터 밝기, 선택지 버튼)를
/// 갱신하는 역할만 담당한다. 대화 진행/분기 로직은 갖지 않는다.
/// 캐릭터 이름/표정 스프라이트는 SpeakerDataSO에서 가져오고, 여기서는 "화면 어느 위치에 그릴지"만 관리한다.
/// 오토 모드: 타이핑이 끝난 뒤 지정된 시간이 지나면 자동으로 다음 대사로 진행한다.
///
/// [캐릭터 슬롯] characterSlots: 캐릭터 스프라이트를 그리는 이미지 (CharacterHolder 안의 White, Red, Black ...).
///             어떤 슬롯이 어떤 인물을 그릴지는 speaker 칸으로 정한다. 비워 두면 이미지 오브젝트 이름이
///             SpeakerDataSO 에셋 이름과 같은 슬롯을 쓰고, 그것도 없으면 비어 있는 슬롯을 빌려 쓴다.
/// [위치 슬롯]  positionSlots: 화면 위치. [0]=Left(왼쪽), [1]=Right(오른쪽), [2]=Center(가운데) 순서 (DialogueSlots 참고).
///             anchor(CharacterHolder 안의 Left/Right/Center 오브젝트) 좌표로 캐릭터를 옮긴다.
///             캐릭터의 아래쪽 가운데(발 위치)가 anchor에 오도록 놓는다. 대사의 screenSlot이 이 인덱스다.
/// [대화창]    dialogBox: 모든 인물이 위치와 상관없이 함께 쓰는 대화창 하나 (이름, 대사, 배경).
///             한 위치에는 한 인물만 선다. 다른 인물이 그 위치로 오면 먼저 있던 인물은 내려간다.
///             이미 화면에 있는 인물이 다른 위치에서 말하면 그 위치로 부드럽게 이동한다 (moveDuration).
///
/// [무대(cast)] 시퀀스의 cast에 등록된 위치의 인물은 계속 화면에 서 있고, 말하는 인물만 밝게(Bright), 나머지는 어둡게(Dim).
///             cast에 없는 위치의 인물은 말할 때만 보인다. 연출 명령으로 도중에 넣고 뺄 수 있다.
///               hide:left            해당 위치의 인물을 내린다
///               show:right           해당 위치의 인물을 무대에 올린다 (그 위치에 마지막으로 섰던 인물 그대로)
///               show:right:Red:Happy 인물(speakerId)과 표정까지 지정해서 그 위치에 올린다 (표정 생략 가능)
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

    [Header("캐릭터 슬롯: 캐릭터 스프라이트를 그리는 이미지 (인물마다 하나)")]
    [SerializeField] private CharacterSlotUI[] characterSlots;

    [Header("위치 슬롯: [0]=Left, [1]=Right, [2]=Center 순서 (DialogLine.screenSlot 인덱스와 대응)")]
    [FormerlySerializedAs("speakerSlots")]
    [SerializeField] private PositionSlotUI[] positionSlots;

    [Tooltip("인물이 다른 위치로 옮겨갈 때 걸리는 시간 (초). 0이면 바로 옮긴다.")]
    [SerializeField] private float moveDuration = 0.25f;

    [Header("대화창: 모든 인물이 위치와 상관없이 함께 쓴다")]
    [SerializeField] private DialogueBoxUI dialogBox;

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
    [Tooltip("↑↓ 로 고른 선택지 버튼의 색")]
    [SerializeField] private Color choiceHighlightColor = new Color(1f, 0.85f, 0.45f, 1f);
    [Tooltip("↑↓ 로 고른 선택지 버튼의 크기 배율")]
    [SerializeField] private float choiceHighlightScale = 1.05f;

    [Header("타이핑 효과")]
    [Tooltip("한 글자를 출력하는 간격 (초)")]
    [SerializeField] private float charInterval = 0.03f;

    [Header("오토 모드")]
    [SerializeField] private Button autoButton;
    [SerializeField] private KeyCode autoModeKey = KeyCode.A;
    [Tooltip("타이핑이 끝난 뒤 자동으로 다음 대사로 넘어가기까지의 대기 시간 (초)")]
    [SerializeField] private float autoAdvanceDelay = 5f;
    [Tooltip("오토 모드가 켜졌을 때 표시할 오브젝트 (버튼 강조 표시 등, 선택)")]
    [SerializeField] private GameObject autoModeIndicator;

    // 현재 말하는 위치 (-1: 내레이션 또는 아직 없음)
    private int _speakingSlot = -1;

    // 무대에 서 있는 위치들 (cast 등록 위치 + show 명령으로 올린 위치)
    private readonly HashSet<int> _castSlots = new HashSet<int>();

    // 위치마다 서 있는 캐릭터 슬롯 번호 (-1: 비어 있음)
    private int[] _occupant;

    // 인물(SpeakerDataSO) -> 그 인물을 그리는 캐릭터 슬롯 번호
    private readonly Dictionary<SpeakerDataSO, int> _characterOf = new Dictionary<SpeakerDataSO, int>();

    // 캐릭터 슬롯마다 지금 그리고 있는 인물 / 진행 중인 이동 연출
    private SpeakerDataSO[] _drawnSpeaker;
    private Coroutine[] _moveRoutines;
    private readonly HashSet<SpeakerDataSO> _warnedNoCharacter = new HashSet<SpeakerDataSO>();
    private readonly HashSet<int> _warnedChoiceRoots = new HashSet<int>();

    private Coroutine _typingCoroutine;
    private TextMeshProUGUI _typingTarget;
    private bool _isTyping = false;

    // 스타일 적용 전의 원래 글꼴 스타일/색 (스타일 없는 줄에서 되돌리기 위해 기억)
    private readonly Dictionary<TextMeshProUGUI, FontStyles> _defaultFontStyles = new Dictionary<TextMeshProUGUI, FontStyles>();
    private readonly Dictionary<TextMeshProUGUI, Color> _defaultColors = new Dictionary<TextMeshProUGUI, Color>();
    private readonly HashSet<string> _warnedStyles = new HashSet<string>();

    // 타이핑이 끝나기를 기다리는 선택지 (타이핑 중에는 버튼을 띄우지 않는다)
    private DialogChoice[] _pendingChoices;

    // 화면에 떠 있는 선택지 수(0이면 선택지 없음)와 ↑↓ 로 고른 선택지
    private int _shownChoiceCount;
    private int _choiceCursor;

    // 강조 표시 전의 원래 버튼 색/크기
    private Color[] _choiceBaseColors;
    private Vector3[] _choiceBaseScales;

    private bool _isAutoMode = false;
    private Coroutine _autoAdvanceCoroutine;

    // 초기화는 Awake에서 한다. 모든 Awake는 어떤 Start보다 먼저 끝나므로,
    // DialogueManager.Start()가 첫 대사를 띄우는 시점에는 UI 초기화가 이미 끝나 있다.
    private void Awake()
    {
        if (characterSlots == null) characterSlots = new CharacterSlotUI[0];
        if (positionSlots == null) positionSlots = new PositionSlotUI[0];

        FindMissingAnchors();

        _occupant = new int[positionSlots.Length];
        for (int i = 0; i < _occupant.Length; i++) _occupant[i] = -1;

        _drawnSpeaker = new SpeakerDataSO[characterSlots.Length];
        _moveRoutines = new Coroutine[characterSlots.Length];

        foreach (var character in characterSlots)
        {
            SetPortraitState(character, PortraitState.Hidden);
        }

        CaptureTextDefaults(dialogBox.textDialogue);
        SetDialogBoxActive(false);

        CaptureTextDefaults(narrationText);
        CaptureChoiceDefaults();

        HideChoicePanel();

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

        if (SettingsPopup.BlocksInput || StoryExitMenu.BlocksInput) return;

        if (Input.GetKeyDown(autoModeKey))
        {
            ToggleAutoMode();
            return;
        }

        if (dialogueManager.IsWaitingForChoice && TryGetChoiceKeyDown(out int choiceIndex))
        {
            ConfirmChoice(choiceIndex);
            return;
        }

        // 선택지가 화면에 떠 있으면 ↑↓ 로 고르고 Enter로 확정한다
        if (dialogueManager.IsWaitingForChoice && _shownChoiceCount > 0)
        {
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                MoveChoiceCursor(-1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                MoveChoiceCursor(1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                ConfirmChoice(_choiceCursor);
                return;
            }
        }

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

    private bool TryGetChoiceKeyDown(out int choiceIndex)
    {
        choiceIndex = -1;

        // 숫자 1은 첫 번째 표시 선택지, 2는 두 번째 선택지를 선택한다.
        for (int i = 0; i < 9; i++)
        {
            if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))
                || Input.GetKeyDown((KeyCode)((int)KeyCode.Keypad1 + i)))
            {
                choiceIndex = i;
                return true;
            }
        }

        return false;
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

        // 나가기 창이나 설정창이 열려 있는 동안에는 넘기지 않고 닫힐 때까지 기다린다
        while (StoryExitMenu.BlocksInput || SettingsPopup.BlocksInput)
        {
            yield return null;
        }

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
        _castSlots.Clear();

        if (cast != null)
        {
            foreach (var member in cast)
            {
                if (!IsValidSlot(member.slot))
                {
                    Debug.LogWarning($"[DialogueUIView] cast의 {SlotWarning(member.slot)}");
                    continue;
                }

                if (member.speaker == null) continue;

                // 새 장면이므로 미끄러지지 않고 바로 그 자리에 세운다
                if (PlaceCharacter(member.speaker, member.expressionName, member.slot, false))
                {
                    _castSlots.Add(member.slot);
                }
            }
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
            Debug.LogWarning($"[DialogueUIView] 'hide:{arg}' 명령의 위치를 해석할 수 없습니다. (예: hide:left)");
            return;
        }

        _castSlots.Remove(slotIndex);
        RefreshPortraits();
    }

    /// <summary>show:위치  또는  show:위치:speakerId[:expression]</summary>
    private void HandleShowCommand(string arg)
    {
        string[] parts = arg.Split(':');

        int slotIndex;
        if (!DialogueSlots.TryParse(parts[0], out slotIndex) || !IsValidSlot(slotIndex))
        {
            Debug.LogWarning($"[DialogueUIView] 'show:{arg}' 명령의 위치를 해석할 수 없습니다. (예: show:right)");
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
                PlaceCharacter(speaker, expression, slotIndex, true);
            }
        }

        _castSlots.Add(slotIndex);
        RefreshPortraits();
    }

    /// <summary>현재 말하는 위치 / 무대에 선 위치 / 그 외에 따라 모든 캐릭터 슬롯의 상태를 다시 맞춘다.</summary>
    private void RefreshPortraits()
    {
        for (int c = 0; c < characterSlots.Length; c++)
        {
            int position = PositionOf(c);
            PortraitState state;

            if (position < 0) state = PortraitState.Hidden;
            else if (position == _speakingSlot) state = PortraitState.Bright;
            else if (_castSlots.Contains(position)) state = PortraitState.Dim;
            else state = PortraitState.Hidden;

            SetPortraitState(characterSlots[c], state);
        }

        // 말하는 인물을 맨 앞에 그린다 (같은 부모 안에서 가장 나중에 그려지도록)
        if (IsValidSlot(_speakingSlot) && _occupant[_speakingSlot] >= 0)
        {
            Image portrait = characterSlots[_occupant[_speakingSlot]].characterPortrait;
            if (portrait != null) portrait.transform.SetAsLastSibling();
        }
    }
    #endregion

    #region Character placement
    /// <summary>
    /// 인물을 위치에 세운다: 그 인물의 캐릭터 슬롯을 찾아 표정 스프라이트를 넣고 위치 슬롯의 anchor로 옮긴다.
    /// 그 위치에 다른 인물이 서 있었으면 그 인물은 내려간다. 인물이 다른 위치에 서 있었으면 그 자리는 비고,
    /// 그 자리가 무대(cast)였다면 무대도 함께 옮겨간다.
    /// </summary>
    /// <param name="animate">이미 화면에 보이던 인물이면 moveDuration 동안 미끄러지듯 옮긴다</param>
    /// <returns>캐릭터 슬롯을 찾아 세웠으면 true</returns>
    private bool PlaceCharacter(SpeakerDataSO speaker, string expressionName, int position, bool animate)
    {
        if (speaker == null || !IsValidSlot(position)) return false;

        int character = GetCharacterSlot(speaker);
        if (character < 0) return false;

        var slot = characterSlots[character];
        if (slot.characterPortrait == null) return false;

        bool wasVisible = PositionOf(character) >= 0 && slot.characterPortrait.color.a > 0f;

        // 원래 서 있던 자리를 비운다 (무대였으면 새 자리로 옮긴다)
        int previous = PositionOf(character);
        if (previous >= 0 && previous != position)
        {
            _occupant[previous] = -1;
            if (_castSlots.Remove(previous)) _castSlots.Add(position);
        }

        _occupant[position] = character;
        _drawnSpeaker[character] = speaker;
        slot.characterPortrait.sprite = GetPortraitSprite(speaker, expressionName);

        MoveToPosition(character, position, animate && wasVisible && previous != position);
        return true;
    }

    /// <summary>인물을 그릴 캐릭터 슬롯을 정한다. speaker 칸 지정 > 오브젝트 이름 일치 > 비어 있는 슬롯 순.</summary>
    private int GetCharacterSlot(SpeakerDataSO speaker)
    {
        int found;
        if (_characterOf.TryGetValue(speaker, out found)) return found;

        found = -1;

        for (int i = 0; i < characterSlots.Length && found < 0; i++)
        {
            if (characterSlots[i].speaker == speaker) found = i;
        }

        for (int i = 0; i < characterSlots.Length && found < 0; i++)
        {
            var portrait = characterSlots[i].characterPortrait;
            if (characterSlots[i].speaker == null && portrait != null && !_characterOf.ContainsValue(i)
                && (portrait.name == speaker.name || portrait.name == speaker.speakerName))
            {
                found = i;
            }
        }

        // 전용 슬롯이 없는 인물: 다른 인물에게 지정되지 않았고 지금 아무도 그리고 있지 않은 슬롯을 빌린다
        for (int i = 0; i < characterSlots.Length && found < 0; i++)
        {
            if (characterSlots[i].speaker == null && characterSlots[i].characterPortrait != null
                && !_characterOf.ContainsValue(i) && !IsReservedByName(i))
            {
                found = i;
            }
        }

        if (found < 0)
        {
            if (_warnedNoCharacter.Add(speaker))
            {
                Debug.LogWarning($"[DialogueUIView] '{speaker.name}'을(를) 그릴 캐릭터 슬롯이 없습니다. characterSlots에 이미지를 추가해 주세요.");
            }
            return -1;
        }

        _characterOf[speaker] = found;
        return found;
    }

    /// <summary>이 슬롯의 오브젝트 이름이 다른 인물 에셋 이름과 같은가 (그 인물 몫으로 남겨 둔다)</summary>
    private bool IsReservedByName(int character)
    {
        var portrait = characterSlots[character].characterPortrait;
        return portrait != null && DialogueJsonLoader.FindSpeaker(portrait.name) != null;
    }

    /// <summary>캐릭터 슬롯이 서 있는 위치. 없으면 -1.</summary>
    private int PositionOf(int character)
    {
        if (_occupant == null) return -1;

        for (int i = 0; i < _occupant.Length; i++)
        {
            if (_occupant[i] == character) return i;
        }

        return -1;
    }

    /// <summary>캐릭터의 아래쪽 가운데(발 위치)가 위치 슬롯의 anchor에 오도록 옮긴다.</summary>
    private void MoveToPosition(int character, int position, bool animate)
    {
        Transform anchor = positionSlots[position].anchor;
        Image portrait = characterSlots[character].characterPortrait;
        if (anchor == null || portrait == null) return;

        if (_moveRoutines[character] != null)
        {
            StopCoroutine(_moveRoutines[character]);
            _moveRoutines[character] = null;
        }

        RectTransform rect = portrait.rectTransform;
        Vector3 target = anchor.position + (rect.position - BottomCenter(rect));

        if (!animate || moveDuration <= 0f || !isActiveAndEnabled)
        {
            rect.position = target;
            return;
        }

        _moveRoutines[character] = StartCoroutine(MoveRoutine(character, rect, target));
    }

    private IEnumerator MoveRoutine(int character, RectTransform rect, Vector3 target)
    {
        Vector3 start = rect.position;
        float elapsed = 0f;

        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / moveDuration);
            rect.position = Vector3.LerpUnclamped(start, target, t * t * (3f - 2f * t)); // 부드럽게 출발/도착
            yield return null;
        }

        rect.position = target;
        _moveRoutines[character] = null;
    }

    private static Vector3 BottomCenter(RectTransform rect)
    {
        Rect r = rect.rect;
        return rect.TransformPoint(new Vector3(r.center.x, r.yMin, 0f));
    }

    /// <summary>anchor가 비어 있는 위치 슬롯은 캐릭터 이미지의 부모(CharacterHolder)에서 Left/Right/Center 이름으로 찾는다.</summary>
    private void FindMissingAnchors()
    {
        Transform holder = null;
        foreach (var character in characterSlots)
        {
            if (character.characterPortrait != null)
            {
                holder = character.characterPortrait.transform.parent;
                break;
            }
        }

        if (holder == null) return;

        string[] names = { "Left", "Right", "Center" };
        for (int i = 0; i < positionSlots.Length && i < names.Length; i++)
        {
            if (positionSlots[i].anchor == null)
            {
                positionSlots[i].anchor = holder.Find(names[i]);
            }
        }
    }
    #endregion

    private void HandleLineShown(DialogueLineInfo info)
    {
        // 연주에서 돌아와 무대를 복원하는 중: 인물 표정만 맞추고 말풍선/타이핑은 만들지 않는다
        if (dialogueManager != null && dialogueManager.IsRestoring)
        {
            RestoreLine(info);
            return;
        }

        CancelAutoAdvance();

        // 새 대사가 나오면 이전 선택지는 모두 정리한다
        _pendingChoices = null;
        StopTyping();

        HideChoicePanel();

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

        // 대화창은 하나를 모든 인물이 함께 쓴다
        SetDialogBoxActive(true);

        // 말하는 인물을 그 위치에 세운다 (다른 위치에 서 있었으면 옮겨간다)
        PlaceCharacter(info.speaker, info.expressionName, info.screenSlot, true);

        _speakingSlot = info.screenSlot;
        RefreshPortraits();

        if (dialogBox.textName != null) dialogBox.textName.text = info.displayName;

        if (dialogBox.textDialogue == null)
        {
            Debug.LogWarning("[DialogueUIView] dialogBox의 textDialogue가 연결되지 않아 대사를 표시할 수 없습니다.");
            return;
        }

        ApplyTextStyle(dialogBox.textDialogue, info.style);
        StartTyping(dialogBox.textDialogue, info.text);
    }

    /// <summary>복원 중에 지나가는 대사 기록: 그 인물이 마지막으로 보인 표정을 슬롯에 되돌려 놓는다.</summary>
    private void RestoreLine(DialogueLineInfo info)
    {
        if (info.speaker == null || !IsValidSlot(info.screenSlot)) return;

        PlaceCharacter(info.speaker, info.expressionName, info.screenSlot, false);
    }

    private void ShowNarration(string text, string styleName)
    {
        if (narrationText == null)
        {
            Debug.LogWarning("[DialogueUIView] 화자가 없는 줄(내레이션)인데 narrationText가 연결되지 않았습니다.");
            return;
        }

        // 대화창과 내레이션 창이 같은 오브젝트여도 되도록, 먼저 끄고 내레이션 창을 켠다
        SetDialogBoxActive(false);

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
    // 글자 사이 대기. 글자마다 new 하지 않고 재사용한다 (charInterval이 바뀌었을 때만 새로 만든다)
    private WaitForSeconds _charWait;
    private float _charWaitInterval = -1f;

    private void StartTyping(TextMeshProUGUI textComponent, string fullText)
    {
        StopTyping();

        _typingTarget = textComponent;
        _typingCoroutine = StartCoroutine(TypeTextRoutine(textComponent, fullText));
    }

    private void StopTyping()
    {
        if (_typingCoroutine != null)
        {
            StopCoroutine(_typingCoroutine);
            _typingCoroutine = null;
        }

        if (_typingTarget != null) _typingTarget.maxVisibleCharacters = int.MaxValue; // [수정] 중간에 멈춰도 글자가 숨지 않게
        _isTyping = false;
    }

    /// <summary>
    /// 전체 대사를 한 번에 넣고 maxVisibleCharacters만 늘려 한 글자씩 보여준다.
    /// 글자마다 문자열을 새로 만들지 않으므로 쓰레기 메모리가 생기지 않고, &lt;color&gt; 같은 리치 텍스트 태그도 깨지지 않는다.
    /// </summary>
    private IEnumerator TypeTextRoutine(TextMeshProUGUI textComponent, string fullText)
    {
        _isTyping = true;

        textComponent.text = fullText;
        textComponent.maxVisibleCharacters = 0;

        // 태그를 뺀 실제 글자 수 (오브젝트가 꺼져 있어 계산되지 않으면 문자열 길이로 대신한다)
        textComponent.ForceMeshUpdate();
        int total = textComponent.textInfo.characterCount;
        if (total <= 0) total = fullText != null ? fullText.Length : 0;

        WaitForSeconds wait = GetCharWait();

        for (int visible = 1; visible <= total; visible++)
        {
            textComponent.maxVisibleCharacters = visible;
            yield return wait;
        }

        textComponent.maxVisibleCharacters = int.MaxValue;

        _isTyping = false;
        _typingCoroutine = null;

        OnTypingFinished();
    }

    private WaitForSeconds GetCharWait()
    {
        if (_charWait == null || !Mathf.Approximately(_charWaitInterval, charInterval))
        {
            _charWaitInterval = charInterval;
            _charWait = new WaitForSeconds(charInterval);
        }

        return _charWait;
    }

    /// <summary>Space로 스킵했을 때, 타이핑 중이던 텍스트를 즉시 전체 출력으로 완성</summary>
    private void SkipTyping()
    {
        StopTyping();

        // 대사는 처음부터 전부 들어 있으므로 가려 둔 글자만 모두 보이게 한다
        if (_typingTarget != null)
        {
            _typingTarget.maxVisibleCharacters = int.MaxValue;
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
                SetChoiceButtonActive(i, true);
                if (choiceButtons[i].label != null) choiceButtons[i].label.text = choices[i].choiceText;

                if (choiceButtons[i].button != null)
                {
                    choiceButtons[i].button.onClick.RemoveAllListeners();
                    choiceButtons[i].button.onClick.AddListener(() =>
                    {
                        CancelAutoAdvance();
                        dialogueManager.SelectChoice(choiceIndex);
                    });
                }
            }
            else
            {
                SetChoiceButtonActive(i, false);
            }
        }

        if (choices.Length > choiceButtons.Length)
        {
            Debug.LogWarning($"[DialogueUIView] 선택지({choices.Length}개)가 choiceButtons({choiceButtons.Length}개)보다 많아 일부가 표시되지 않습니다.");
        }

        // 마우스로 눌렀던 버튼이 EventSystem에 선택된 채로 남아 있으면 Enter 한 번에
        // EventSystem의 Submit과 아래 ↑↓/Enter 처리가 둘 다 실행되므로 선택을 비운다
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        // ↑↓ 선택은 첫 번째 선택지부터
        _shownChoiceCount = Mathf.Min(choices.Length, choiceButtons.Length);
        _choiceCursor = 0;
        RefreshChoiceHighlight();
    }

    /// <summary>
    /// 선택지 버튼 하나를 켜고 끈다. root가 비어 있거나 선택지 패널(또는 그 부모)로 잘못 연결돼 있으면
    /// 패널 전체가 꺼지지 않도록 버튼 오브젝트를 대신 켜고 끈다.
    /// </summary>
    private void SetChoiceButtonActive(int index, bool active)
    {
        GameObject target = choiceButtons[index].root;
        Button button = choiceButtons[index].button;

        bool rootIsPanel = target != null && choicePanelRoot != null
                           && choicePanelRoot.transform.IsChildOf(target.transform);

        if (target == null || rootIsPanel)
        {
            if (rootIsPanel && _warnedChoiceRoots.Add(index))
            {
                Debug.LogWarning($"[DialogueUIView] choiceButtons[{index}].root가 선택지 패널({target.name})로 연결돼 있어 버튼 오브젝트를 대신 사용합니다. root에는 버튼 오브젝트를 연결해 주세요.");
            }

            target = button != null ? button.gameObject : null;
        }

        if (target != null) target.SetActive(active);
    }

    private void ConfirmChoice(int choiceIndex)
    {
        CancelAutoAdvance();
        dialogueManager.SelectChoice(choiceIndex);
    }

    private void MoveChoiceCursor(int direction)
    {
        if (_shownChoiceCount <= 0) return;

        _choiceCursor = (_choiceCursor + direction + _shownChoiceCount) % _shownChoiceCount;
        RefreshChoiceHighlight();
    }

    /// <summary>고른 선택지만 강조 색/크기로, 나머지는 원래대로</summary>
    private void RefreshChoiceHighlight()
    {
        if (choiceButtons == null || _choiceBaseColors == null) return;

        for (int i = 0; i < choiceButtons.Length; i++)
        {
            bool highlighted = i < _shownChoiceCount && i == _choiceCursor;
            var button = choiceButtons[i].button;
            if (button == null) continue;

            if (button.targetGraphic != null)
            {
                button.targetGraphic.color = highlighted ? choiceHighlightColor : _choiceBaseColors[i];
            }

            button.transform.localScale = highlighted ? _choiceBaseScales[i] * choiceHighlightScale : _choiceBaseScales[i];
        }
    }

    private void CaptureChoiceDefaults()
    {
        if (choiceButtons == null) return;

        _choiceBaseColors = new Color[choiceButtons.Length];
        _choiceBaseScales = new Vector3[choiceButtons.Length];

        for (int i = 0; i < choiceButtons.Length; i++)
        {
            var button = choiceButtons[i].button;
            _choiceBaseColors[i] = (button != null && button.targetGraphic != null) ? button.targetGraphic.color : Color.white;
            _choiceBaseScales[i] = button != null ? button.transform.localScale : Vector3.one;
        }
    }

    /// <summary>선택지 창을 닫고 ↑↓ 선택 상태와 강조 표시를 지운다</summary>
    private void HideChoicePanel()
    {
        _shownChoiceCount = 0;
        _choiceCursor = 0;
        RefreshChoiceHighlight();

        if (choicePanelRoot != null)
        {
            choicePanelRoot.SetActive(false);
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

        SetDialogBoxActive(false);

        RefreshPortraits();

        if (narrationBoxRoot != null)
        {
            narrationBoxRoot.SetActive(false);
        }

        HideChoicePanel();

        if (dialogBoxRoot != null)
        {
            dialogBoxRoot.SetActive(false);
        }
    }

    #region Slot Helpers
    private bool IsValidSlot(int slotIndex)
    {
        return positionSlots != null && slotIndex >= 0 && slotIndex < positionSlots.Length;
    }

    private string SlotWarning(int slotIndex)
    {
        return $"위치 {DialogueSlots.Describe(slotIndex)}에 해당하는 positionSlots 요소가 없습니다. " +
               $"positionSlots는 [0]=Left, [1]=Right, [2]=Center 순서로 구성해야 합니다. (현재 {(positionSlots != null ? positionSlots.Length : 0)}개)";
    }

    /// <summary>공용 대화창(이름, 대사, 배경)의 표시 여부. 캐릭터는 건드리지 않는다.</summary>
    private void SetDialogBoxActive(bool active)
    {
        if (dialogBox.dialogImage != null) dialogBox.dialogImage.gameObject.SetActive(active);
        if (dialogBox.textName != null) dialogBox.textName.gameObject.SetActive(active);
        if (dialogBox.textDialogue != null) dialogBox.textDialogue.gameObject.SetActive(active);
    }

    /// <summary>캐릭터의 표시 상태. Hidden: 투명, Dim: 어둡게, Bright: 원래 색.</summary>
    private void SetPortraitState(CharacterSlotUI slot, PortraitState state)
    {
        if (slot.characterPortrait == null) return;

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

/// <summary>캐릭터 슬롯: 인물 한 명의 표정 스프라이트를 그리는 이미지. 위치는 갖지 않는다 (위치 슬롯으로 옮겨진다).</summary>
[System.Serializable]
public struct CharacterSlotUI
{
    [Tooltip("표정 스프라이트가 그려질 캐릭터 이미지 (CharacterHolder 안의 White, Red, Black ...)")]
    public Image characterPortrait;

    [Tooltip("이 슬롯이 그릴 인물. 비우면 이미지 오브젝트 이름이 SpeakerDataSO 에셋 이름과 같은 인물을 그리고, " +
             "그런 인물이 없으면 전용 슬롯이 없는 인물에게 빌려준다.")]
    public SpeakerDataSO speaker;
}

/// <summary>위치 슬롯: 화면의 한 위치(왼쪽/오른쪽/가운데).</summary>
[System.Serializable]
public struct PositionSlotUI
{
    [Tooltip("캐릭터를 세울 자리 (CharacterHolder 안의 Left/Right/Center). 캐릭터의 아래쪽 가운데가 이 좌표에 온다. " +
             "비우면 CharacterHolder에서 이름으로 찾는다.")]
    public Transform anchor;
}

/// <summary>모든 인물이 함께 쓰는 대화창 (이름, 대사, 배경)</summary>
[System.Serializable]
public struct DialogueBoxUI
{
    public TextMeshProUGUI textName;
    public TextMeshProUGUI textDialogue;
    [Tooltip("대화창 배경. 켜고 끌 때 이 오브젝트를 쓴다.")]
    public Image dialogImage;
}

/// <summary>선택지 버튼 하나에 필요한 UI 참조 묶음</summary>
[System.Serializable]
public struct ChoiceButtonRef
{
    public GameObject root;   // 버튼 하나를 감싸는 오브젝트 (활성/비활성 제어용). 비우면 버튼 오브젝트를 쓴다. 선택지 패널을 넣으면 안 된다.
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
