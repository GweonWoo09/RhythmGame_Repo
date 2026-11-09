using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 대화 진행 로직 담당.
/// 이벤트를 통해 현재 대사/선택지 정보를 DialogueUIView에게 알린다.
/// 대사 데이터는 DialogueDataSO 에셋으로부터 받아오며, BGM 전환은 AudioManager에 위임한다.
///
/// [v2 진행 규칙]
/// 1) 줄을 보여주기 전에 condition이 거짓이면 그 줄을 통째로 건너뛴다 (setFlags/goto도).
/// 2) 줄이 표시될 때: bgmOverride -> setFlags 적용 -> commands 발행 -> OnDialogUpdated -> 선택지.
/// 3) 줄을 읽고 넘어갈 때: branches(처음 참인 것) -> gotoLabel -> 다음 줄 순서로 목적지를 정한다.
///    목적지가 "end"이거나 마지막 줄을 지나면 대화 종료.
/// 4) 선택지는 condition이 참인 것만 UI로 전달한다 (SelectChoice 인덱스도 전달된 목록 기준).
/// 5) 점프 목적지는 gotoLabel(권장)이 우선, 없으면 targetIndex. 잘못된 목적지면 에러만 남기고
///    현재 상태(선택 대기 포함)를 그대로 유지한다.
/// 6) 대화 종료 시 onEnd의 clearChapter/unlockChapter를 GameManager에 반영하고 OnDialogEnd를 발행한다.
///    씬 이동은 SceneTransitionDialogueEnd가 EndAction을 읽어 처리한다.
/// 7) 대사, 이름, 선택지 문구의 {변수}(예: {player})는 표시 직전에 DialogueVariables로 치환한다.
/// 8) 줄의 commands에 play:곡ID[:난이도]가 있으면, 그 줄을 넘기는 순간 진행을 멈추고(IsSuspended)
///    OnPlayRequested로 연주를 요청한다. 연주 후 씬이 다시 열리면 PrepareResume으로 받은 스냅샷에서 이어간다.
///    - 연주를 마쳤으면 play 줄의 "다음 줄"(branch -> goto -> 다음 줄)부터,
///      마치지 못했으면 play 줄을 다시 보여준다 (넘기면 다시 연주).
///    - 무대 상태(cast, show/hide, 인물 표정)는 기록해 두었다가 돌아올 때 조용히 복원한다.
/// </summary>
public class DialogueManager : MonoBehaviour
{
    [Tooltip("재생할 대화 시퀀스 에셋 (DialogueDataSO)")]
    [SerializeField] private DialogueDataSO dialogSequence;

    [Tooltip("dialogSequence가 비어있을 때 JSON(Resources/Dialogue/{언어}/{ID}.json)에서 불러올 시퀀스 ID")]
    [SerializeField] private string startSequenceId;

    private int currentDialogIndex = -1;
    private bool isFinished = false;
    private bool isWaitingForChoice = false;

    // 조건을 통과해 UI로 전달된 선택지 목록. SelectChoice의 인덱스는 이 배열 기준이다.
    private DialogChoice[] _visibleChoices;

    // play 명령으로 연주하러 나간 동안 true. 이 동안 AdvanceDialog는 아무것도 하지 않는다.
    private bool isSuspended = false;
    private bool _ignorePlayOnce = false;

    // 연주에서 돌아와 이어가기 위한 정보 (PrepareResume으로 받고 Start에서 사용)
    private DialoguePlaySnapshot _resume;
    private bool _resumeSongFinished;

    // 지금까지의 무대 상태 변화 기록 (연주에서 돌아올 때 UI 무대를 복원하는 데 쓴다)
    private readonly List<DialogueStageRecord> _stageLog = new List<DialogueStageRecord>();
    private AudioClip _currentBgm;
    private int _currentBgmLine = -1; // 지금 BGM을 바꾼 줄 (-1: 시퀀스 기본 BGM). 대사 위치 저장용

    // 씬에 들어왔을 때의 시퀀스. 이 대화를 끝까지 보면 저장된 대사 위치를 지운다.
    private string _enteredSequenceId;

    /// <summary>
    /// 새 대사가 표시될 때: (화면 슬롯, 화자 데이터(null이면 내레이션), 표정 이름, 대사 텍스트).
    /// 기존 구독자와의 호환용이며 텍스트는 변수 치환이 끝난 값이다.
    /// 표시 이름, 스타일까지 필요하면 OnLineShown을 구독한다 (DialogueUIView는 OnLineShown만 쓴다).
    /// </summary>
    public event Action<int, SpeakerDataSO, string, string> OnDialogUpdated;

    /// <summary>새 대사가 표시될 때, 화면 표시에 필요한 정보를 한 번에 전달한다. (변수 치환 완료)</summary>
    public event Action<DialogueLineInfo> OnLineShown;

    /// <summary>현재 줄이 선택지를 가지고 있을 때 발행. 조건을 통과한 선택지만 담겨 있다.</summary>
    public event Action<DialogChoice[]> OnChoicesPresented;

    /// <summary>대화가 모두 끝났을 때</summary>
    public event Action OnDialogEnd;

    /// <summary>시퀀스가 시작/교체될 때 그 시퀀스의 cast(무대에 서 있을 인물)를 알린다. 없으면 null.</summary>
    public event Action<DialogueCast[]> OnCastUpdated;

    /// <summary>줄의 commands를 "이름", "인자"로 쪼개 발행한다. 예: "hide:0" -> ("hide", "0"). 처리는 구독자 몫.</summary>
    public event Action<string, string> OnCommand;

    /// <summary>
    /// play 명령이 붙은 줄을 넘기는 순간 발행. 스냅샷에 곡 ID/난이도와 돌아와서 이어갈 정보가 담겨 있다.
    /// 구독자(FlowStoryEntry)가 연주를 시작시켜야 하고, 시작하지 못하면 CancelPlay()를 불러야 한다.
    /// 구독자가 없으면 에러 로그를 남기고 play를 건너뛴다.
    /// </summary>
    public event Action<DialoguePlaySnapshot> OnPlayRequested;

    private void Start()
    {
        // 연주 후(또는 연주하다 나왔다가) 돌아온 경우: 연주 직전 대사 위치에서 이어간다
        if (_resume != null && TryResumeFromSnapshot())
        {
            _enteredSequenceId = dialogSequence.name;
            return;
        }

        if (dialogSequence == null && !string.IsNullOrEmpty(startSequenceId))
        {
            dialogSequence = DialogueJsonLoader.Load(startSequenceId);
        }

        if (dialogSequence != null) _enteredSequenceId = dialogSequence.name;

        // 나가기로 저장해 둔 대사 위치가 이 시퀀스라면 그 줄부터 이어간다
        if (TryResumeFromBookmark()) return;

        if (HasLines())
        {
            BeginSequence(0);
        }
    }

    /// <summary>
    /// Start()가 시작할 JSON 시퀀스를 바꾼다. 인스펙터에 지정된 dialogSequence 에셋은 무시된다.
    /// 반드시 Start() 이전(Awake)에 호출해야 한다. (이야기 흐름이 칸마다 다른 시퀀스를 넘길 때 FlowStoryEntry가 쓴다)
    /// 이미 시작된 뒤에 시퀀스를 바꾸려면 LoadSequenceFromJson / SetDialogSequence를 쓴다.
    /// </summary>
    public void OverrideStartSequence(string sequenceId)
    {
        dialogSequence = null;
        startSequenceId = sequenceId;
    }

    /// <summary>
    /// 다른 챕터/컷씬의 대화로 교체하고 싶을 때 (예: SceneChanger의 payload로 전달받은 경우).
    /// 시퀀스가 비어있거나 startIndex가 범위를 벗어나면 에러만 남기고 현재 상태를 바꾸지 않는다.
    /// </summary>
    public void SetDialogSequence(DialogueDataSO sequence, int startIndex = 0)
    {
        if (!IsValidIndex(sequence, startIndex))
        {
            Debug.LogError($"[DialogueManager] 시퀀스를 시작할 수 없습니다. " +
                           $"'{(sequence != null ? sequence.name : "null")}' 시퀀스에 {startIndex}번 줄이 없습니다.");
            return;
        }

        dialogSequence = sequence;
        currentDialogIndex = -1;
        isFinished = false;
        isWaitingForChoice = false;
        isSuspended = false;
        _visibleChoices = null;

        BeginSequence(startIndex);
    }

    /// <summary>
    /// 외부(입력 처리 스크립트 등)에서 "다음으로 진행" 요청 시 호출.
    /// 선택지 대기 중일 때는 무시된다 (선택지는 SelectChoice로만 진행).
    /// </summary>
    public void AdvanceDialog()
    {
        if (isFinished || isWaitingForChoice || isSuspended || !HasLines()) return;

        var line = dialogSequence.lines[currentDialogIndex];

        // 줄에 play 명령이 있으면, 이 줄을 넘기는 순간 연주하러 나간다
        if (TryRequestPlay(line)) return;

        if (!TryGetNextIndex(line, out int nextIndex))
        {
            // 목적지 라벨을 찾지 못함: 에러는 이미 남겼고, 엉뚱한 줄로 넘어가지 않도록 현재 줄에 머문다
            return;
        }

        ShowDialog(nextIndex);
    }

    /// <summary>UI에서 선택지 버튼을 눌렀을 때 호출. choiceIndex는 OnChoicesPresented로 전달된 배열의 인덱스.</summary>
    public void SelectChoice(int choiceIndex)
    {
        if (!isWaitingForChoice) return;
        if (_visibleChoices == null || choiceIndex < 0 || choiceIndex >= _visibleChoices.Length) return;

        var choice = _visibleChoices[choiceIndex];

        // 다른 시퀀스로 분기하는 선택지라면, 상태를 바꾸기 전에 대상 시퀀스를 먼저 확보한다.
        // (직접 참조가 없고 ID만 있으면 JSON에서 로드)
        DialogueDataSO nextSequence = choice.targetSequence;
        if (nextSequence == null && !string.IsNullOrEmpty(choice.targetSequenceId))
        {
            nextSequence = DialogueJsonLoader.Load(choice.targetSequenceId);
            if (nextSequence == null)
            {
                // 로드 실패 시 선택 대기 상태를 유지해서 엉뚱한 줄로 넘어가지 않게 한다
                return;
            }
        }

        DialogueDataSO targetSequence = nextSequence != null ? nextSequence : dialogSequence;

        // 이동할 줄 결정 (gotoLabel 우선, 없으면 targetIndex). 검증에 실패하면 플래그도 기록하지 않고 선택 대기 유지.
        bool endsDialogue = choice.gotoLabel == DialogueDataSO.EndLabel;
        int targetIndex = choice.targetIndex;

        if (!endsDialogue)
        {
            if (!string.IsNullOrEmpty(choice.gotoLabel))
            {
                if (!TryResolveLabel(targetSequence, choice.gotoLabel, out targetIndex)) return;
            }

            if (!IsValidIndex(targetSequence, targetIndex))
            {
                Debug.LogError($"[DialogueManager] 선택지 '{choice.choiceText}'의 이동 위치({targetIndex})가 " +
                               $"'{targetSequence.name}' 시퀀스의 범위를 벗어났습니다. " +
                               $"(줄 수: {(targetSequence.lines != null ? targetSequence.lines.Length : 0)})");
                return;
            }
        }

        isWaitingForChoice = false;
        _visibleChoices = null;

        // 플래그 기록 (하위 호환 단일 플래그 + 새 setFlags)
        if (!string.IsNullOrEmpty(choice.flagKey))
        {
            DialogueExpression.SetFlag(choice.flagKey, choice.storyFlagValue);
        }
        DialogueExpression.ApplyAll(choice.setFlags);

        if (endsDialogue)
        {
            FinishDialog();
        }
        else if (nextSequence != null)
        {
            // 다른 시퀀스(챕터 분기)로 전환 (BGM, cast도 새 시퀀스 기준으로 교체됨)
            SetDialogSequence(nextSequence, targetIndex);
        }
        else
        {
            // 같은 시퀀스 내에서 점프
            ShowDialog(targetIndex);
        }
    }

    #region Flow
    private void BeginSequence(int startIndex)
    {
        PlaySequenceBGM();

        // cast 이벤트가 UI의 무대를 초기화하므로, 무대 상태 기록도 시퀀스가 시작될 때마다 새로 쌓는다
        _stageLog.Clear();
        _stageLog.Add(new DialogueStageRecord { kind = DialogueStageRecordKind.Cast, cast = dialogSequence.cast });

        OnCastUpdated?.Invoke(dialogSequence.cast);
        ShowDialog(startIndex);
    }

    /// <summary>index부터 시작해 condition이 참인 첫 줄을 보여준다. 없으면 대화 종료.</summary>
    /// <param name="replaying">
    /// 연주를 마치지 못하고 돌아와 play 줄을 다시 보여주는 경우 true.
    /// 조건 확인, BGM, 플래그, 명령은 이미 한 번 처리됐으므로(플래그가 += 이면 두 번 더해지므로) 건너뛴다.
    /// </param>
    private void ShowDialog(int index, bool replaying = false)
    {
        if (!replaying)
        {
            index = FindPlayableIndex(index);
            if (index < 0)
            {
                FinishDialog();
                return;
            }
        }

        currentDialogIndex = index;

        var line = dialogSequence.lines[index];

        if (!replaying)
        {
            // 이 줄에서 BGM을 바꾸도록 지정되어 있으면 교체
            if (line.bgmOverride != null && AudioManager.Instance != null)
            {
                _currentBgm = line.bgmOverride;
                _currentBgmLine = index;
                AudioManager.Instance.PlayBGM(line.bgmOverride);
            }

            // 플래그 기록 -> 명령 발행 (선택지 조건은 이 기록이 반영된 뒤에 평가된다)
            DialogueExpression.ApplyAll(line.setFlags);
            RaiseCommands(line.commands);
        }

        var info = BuildLineInfo(line);

        OnDialogUpdated?.Invoke(line.screenSlot, line.speaker, line.expressionName, info.text);
        OnLineShown?.Invoke(info);

        // 인물 표정이 어떻게 바뀌어 왔는지 기록 (연주에서 돌아올 때 / 저장한 위치에서 이어갈 때 복원용)
        if (!replaying && line.speaker != null)
        {
            _stageLog.Add(new DialogueStageRecord { kind = DialogueStageRecordKind.Line, line = info, lineIndex = index });
        }

        if (line.choices != null && line.choices.Length > 0)
        {
            var visible = FilterChoices(line.choices);

            if (visible.Length > 0)
            {
                _visibleChoices = visible;
                isWaitingForChoice = true;
                OnChoicesPresented?.Invoke(visible);
            }
            else
            {
                Debug.LogWarning($"[DialogueManager] {dialogSequence.name} {index}번 줄: 조건을 통과한 선택지가 없어 일반 대사로 진행합니다.");
            }
        }
    }

    /// <summary>줄을 화면 표시용 정보로 바꾼다 ({변수} 치환 포함)</summary>
    private static DialogueLineInfo BuildLineInfo(DialogLine line)
    {
        // 표시 이름: 줄의 displayName > 화자의 speakerName. 둘 다 {변수}를 쓸 수 있다.
        string rawName = !string.IsNullOrEmpty(line.displayName)
            ? line.displayName
            : (line.speaker != null ? line.speaker.speakerName : "");

        return new DialogueLineInfo
        {
            screenSlot = line.screenSlot,
            speaker = line.speaker,
            expressionName = line.expressionName,
            displayName = DialogueVariables.Resolve(rawName),
            style = line.style,
            text = DialogueVariables.Resolve(line.dialogText)
        };
    }

    private void FinishDialog()
    {
        isFinished = true;
        isWaitingForChoice = false;
        _visibleChoices = null;

        if (dialogSequence != null && GameManager.Instance != null)
        {
            var end = dialogSequence.onEnd;

            if (end.clearChapter > 0) GameManager.Instance.ClearChapter(end.clearChapter);
            if (end.unlockChapter > 0) GameManager.Instance.UnlockChapter(end.unlockChapter);

            // 이 대화를 끝까지 봤으니 저장해 둔 대사 위치는 지운다. (챕터 해금/클리어도 함께 저장된다)
            var bookmark = GameManager.Instance.GetStoryBookmark();
            if (bookmark != null && (bookmark.sequenceId == dialogSequence.name || bookmark.sequenceId == _enteredSequenceId))
            {
                GameManager.Instance.SetStoryBookmark(null);
            }
            else
            {
                GameManager.Instance.SaveGame();
            }
        }

        OnDialogEnd?.Invoke();
    }

    /// <summary>현재 줄을 읽고 난 뒤 이동할 인덱스를 정한다. 마지막 줄을 지나거나 "end"면 lines.Length를 돌려준다.</summary>
    private bool TryGetNextIndex(DialogLine line, out int nextIndex)
    {
        string target = null;

        if (line.branches != null)
        {
            foreach (var branch in line.branches)
            {
                if (DialogueExpression.Evaluate(branch.condition))
                {
                    target = branch.gotoLabel;
                    break;
                }
            }
        }

        if (string.IsNullOrEmpty(target)) target = line.gotoLabel;

        if (string.IsNullOrEmpty(target))
        {
            nextIndex = currentDialogIndex + 1;
            return true;
        }

        if (target == DialogueDataSO.EndLabel)
        {
            nextIndex = dialogSequence.lines.Length;
            return true;
        }

        return TryResolveLabel(dialogSequence, target, out nextIndex);
    }

    private int FindPlayableIndex(int startIndex)
    {
        for (int i = Mathf.Max(0, startIndex); i < dialogSequence.lines.Length; i++)
        {
            if (DialogueExpression.Evaluate(dialogSequence.lines[i].condition))
            {
                return i;
            }
        }

        return -1;
    }

    private static DialogChoice[] FilterChoices(DialogChoice[] all)
    {
        var visible = new List<DialogChoice>(all.Length);

        foreach (var choice in all)
        {
            if (DialogueExpression.Evaluate(choice.condition))
            {
                // struct 복사본의 문구만 치환한다 (원본 데이터는 그대로)
                var shown = choice;
                shown.choiceText = DialogueVariables.Resolve(choice.choiceText);
                visible.Add(shown);
            }
        }

        return visible.ToArray();
    }

    private void RaiseCommands(string[] commands)
    {
        if (commands == null) return;

        foreach (string command in commands)
        {
            if (string.IsNullOrWhiteSpace(command)) continue;

            string[] parts = command.Split(new[] { ':' }, 2);
            string name = parts[0].Trim();
            string arg = parts.Length > 1 ? parts[1].Trim() : string.Empty;

            // play는 줄이 표시될 때가 아니라 줄을 넘길 때 처리한다 (TryRequestPlay)
            if (name == "play") continue;

            // 무대에 영향을 주는 명령만 기록한다. (bg, sfx처럼 부작용이 있는 명령은 복원 때 다시 실행하면 안 된다)
            if (name == "show" || name == "hide")
            {
                _stageLog.Add(new DialogueStageRecord { kind = DialogueStageRecordKind.Command, name = name, arg = arg });
            }

            OnCommand?.Invoke(name, arg);
        }
    }

    private static bool TryResolveLabel(DialogueDataSO sequence, string label, out int index)
    {
        index = sequence.FindLabelIndex(label);

        if (index < 0)
        {
            Debug.LogError($"[DialogueManager] '{sequence.name}' 시퀀스에서 id '{label}'인 줄을 찾을 수 없습니다.");
            return false;
        }

        return true;
    }
    #endregion

    #region Play (연주하러 나갔다 돌아오기)
    /// <summary>
    /// 연주에서 돌아온 Story 씬에서, DialogueManager.Start()보다 먼저(Awake) 호출한다. (FlowStoryEntry가 한다)
    /// songFinished가 true면 play 줄의 다음 줄부터, false면 play 줄을 다시 보여주며 이어간다.
    /// </summary>
    public void PrepareResume(DialoguePlaySnapshot snapshot, bool songFinished)
    {
        _resume = snapshot;
        _resumeSongFinished = songFinished;
    }

    /// <summary>OnPlayRequested 구독자가 연주를 시작하지 못했을 때 부른다. play를 건너뛰고 다음으로 진행한다.</summary>
    public void CancelPlay()
    {
        if (!isSuspended) return;

        isSuspended = false;
        _ignorePlayOnce = true;
        AdvanceDialog();
        _ignorePlayOnce = false;
    }

    /// <summary>줄에 play 명령이 있으면 진행을 멈추고 연주를 요청한다. 요청했으면 true.</summary>
    private bool TryRequestPlay(DialogLine line)
    {
        string songId, difficulty;
        if (_ignorePlayOnce || !TryGetPlayCommand(line, out songId, out difficulty)) return false;

        if (OnPlayRequested == null)
        {
            Debug.LogError($"[DialogueManager] {dialogSequence.name} {currentDialogIndex}번 줄의 play:{songId}를 처리할 곳이 없어 건너뜁니다. " +
                           "Story 씬에 FlowStoryEntry가 있는지 확인하세요.");
            return false;
        }

        isSuspended = true;

        OnPlayRequested.Invoke(new DialoguePlaySnapshot
        {
            sequenceId = dialogSequence.name,
            playLineIndex = currentDialogIndex,
            songId = songId,
            difficulty = difficulty,
            stageLog = new List<DialogueStageRecord>(_stageLog),
            bgm = _currentBgm,
            bgmLineIndex = _currentBgmLine
        });

        return true;
    }

    /// <summary>"play:곡ID" 또는 "play:곡ID:난이도"를 찾는다.</summary>
    private static bool TryGetPlayCommand(DialogLine line, out string songId, out string difficulty)
    {
        songId = null;
        difficulty = null;

        if (line.commands == null) return false;

        foreach (string command in line.commands)
        {
            if (string.IsNullOrWhiteSpace(command)) continue;

            string[] parts = command.Split(new[] { ':' }, 2);
            if (parts[0].Trim() != "play") continue;

            string[] args = (parts.Length > 1 ? parts[1] : "").Split(':');
            songId = args[0].Trim();
            difficulty = args.Length > 1 ? args[1].Trim() : "";
            return songId.Length > 0;
        }

        return false;
    }

    /// <summary>PrepareResume으로 받은 위치에서 대화를 이어간다. 이어갈 수 없으면 false (처음부터 시작하게 한다).</summary>
    private bool TryResumeFromSnapshot()
    {
        DialoguePlaySnapshot snapshot = _resume;
        bool songFinished = _resumeSongFinished;
        _resume = null;

        DialogueDataSO sequence = (dialogSequence != null && dialogSequence.name == snapshot.sequenceId)
            ? dialogSequence
            : DialogueJsonLoader.Load(snapshot.sequenceId);

        if (!IsValidIndex(sequence, snapshot.playLineIndex))
        {
            Debug.LogError($"[DialogueManager] 연주에서 돌아왔지만 '{snapshot.sequenceId}'의 {snapshot.playLineIndex}번 줄을 찾을 수 없어 처음부터 시작합니다.");
            return false;
        }

        dialogSequence = sequence;
        currentDialogIndex = snapshot.playLineIndex;
        isFinished = false;
        isWaitingForChoice = false;
        isSuspended = false;

        _currentBgmLine = snapshot.bgmLineIndex;
        if (snapshot.bgm != null)
        {
            _currentBgm = snapshot.bgm;
            if (AudioManager.Instance != null) AudioManager.Instance.PlayBGM(snapshot.bgm);
        }

        ReplayStage(snapshot.stageLog);

        // 연주를 마치지 못했다: play 줄을 다시 보여준다 (넘기면 다시 연주)
        if (!songFinished)
        {
            ShowDialog(snapshot.playLineIndex, true);
            return true;
        }

        // 연주를 마쳤다: play 줄의 다음 줄부터. (연주 결과 플래그가 이미 기록돼 있으므로 branch 조건이 그대로 통한다)
        DialogLine playLine = dialogSequence.lines[snapshot.playLineIndex];
        int nextIndex;
        if (TryGetNextIndex(playLine, out nextIndex))
        {
            ShowDialog(nextIndex);
        }
        else
        {
            FinishDialog();
        }

        return true;
    }

    /// <summary>기록해 둔 무대 상태 변화를 UI에 다시 알린다. 이 동안 IsRestoring이 true다.</summary>
    private void ReplayStage(List<DialogueStageRecord> log)
    {
        _stageLog.Clear();
        if (log == null) return;

        IsRestoring = true;

        foreach (DialogueStageRecord record in log)
        {
            switch (record.kind)
            {
                case DialogueStageRecordKind.Cast:
                    OnCastUpdated?.Invoke(record.cast);
                    break;

                case DialogueStageRecordKind.Command:
                    OnCommand?.Invoke(record.name, record.arg);
                    break;

                case DialogueStageRecordKind.Line:
                    OnLineShown?.Invoke(record.line);
                    break;
            }

            _stageLog.Add(record);
        }

        IsRestoring = false;
    }
    #endregion

    #region Bookmark (나가기로 저장한 대사 위치)
    /// <summary>
    /// 지금 보고 있는 줄까지의 진행을 GameManager에 기록하고 세이브 파일에 저장한다. (StoryExitMenu의 나가기 버튼)
    /// 이 줄의 플래그와 명령은 이미 처리됐으므로, 돌아오면 이 줄을 다시 보여주기만 한다. 선택지 대기 중이면 선택지도 다시 뜬다.
    /// 저장할 위치가 없으면(대화가 끝났거나 시작 전) false.
    /// </summary>
    /// <param name="interrupted">게임 종료로 저장하는 경우 true (다음 실행 때 Lobby에서 이어하기 창을 띄운다)</param>
    public bool SaveBookmark(bool interrupted = false)
    {
        if (GameManager.Instance == null)
        {
            Debug.LogWarning("[DialogueManager] GameManager가 없어 대사 위치를 저장하지 못했습니다. (Loading 씬부터 실행했는지 확인)");
            return false;
        }

        if (isFinished || !IsValidIndex(dialogSequence, currentDialogIndex)) return false;

        var bookmark = new GameManager.StoryBookmark
        {
            sequenceId = dialogSequence.name,
            lineIndex = currentDialogIndex,
            bgmLineIndex = _currentBgmLine,
            interrupted = interrupted
        };

        foreach (var record in _stageLog)
        {
            // cast는 시퀀스에서 다시 읽을 수 있으므로 저장하지 않는다
            if (record.kind == DialogueStageRecordKind.Cast) continue;

            bookmark.stage.Add(new GameManager.StoryStageEntry
            {
                kind = (int)record.kind,
                name = record.name,
                arg = record.arg,
                lineIndex = record.lineIndex
            });
        }

        GameManager.Instance.SetStoryBookmark(bookmark);
        return true;
    }

    // 스토리 도중 게임을 종료하면(창 닫기, Alt+F4, 에디터 플레이 중지 포함) 지금 줄을 저장한다.
    // 연주하러 나간 동안(IsSuspended)에는 이 씬이 이미 닫혀 있으므로 여기에 오지 않는다.
    private void OnApplicationQuit()
    {
        // 나가기 버튼으로 이미 저장하고 떠나는 중이면 그 저장을 그대로 둔다
        if (isFinished || isSuspended || StoryExitMenu.IsLeaving) return;
        SaveBookmark(true);
    }

    /// <summary>저장된 대사 위치가 지금 시퀀스의 것이면 그 줄부터 이어간다. 이어갔으면 true.</summary>
    private bool TryResumeFromBookmark()
    {
        if (GameManager.Instance == null || !HasLines()) return false;

        var bookmark = GameManager.Instance.GetStoryBookmark();
        if (bookmark == null || bookmark.sequenceId != dialogSequence.name) return false;

        if (!IsValidIndex(dialogSequence, bookmark.lineIndex))
        {
            Debug.LogWarning($"[DialogueManager] 저장된 대사 위치({bookmark.sequenceId} {bookmark.lineIndex}번 줄)를 찾을 수 없어 처음부터 시작합니다.");
            return false;
        }

        // 저장된 무대 기록을 되살린다 (cast -> show/hide -> 인물이 말한 줄)
        var log = new List<DialogueStageRecord>
        {
            new DialogueStageRecord { kind = DialogueStageRecordKind.Cast, cast = dialogSequence.cast }
        };

        if (bookmark.stage != null)
        {
            foreach (var entry in bookmark.stage)
            {
                var kind = (DialogueStageRecordKind)entry.kind;

                if (kind == DialogueStageRecordKind.Command)
                {
                    log.Add(new DialogueStageRecord { kind = kind, name = entry.name, arg = entry.arg });
                }
                else if (kind == DialogueStageRecordKind.Line && IsValidIndex(dialogSequence, entry.lineIndex))
                {
                    log.Add(new DialogueStageRecord
                    {
                        kind = kind,
                        line = BuildLineInfo(dialogSequence.lines[entry.lineIndex]),
                        lineIndex = entry.lineIndex
                    });
                }
            }
        }

        currentDialogIndex = bookmark.lineIndex;
        isFinished = false;
        isWaitingForChoice = false;
        isSuspended = false;
        _visibleChoices = null;

        // BGM: 저장 당시 BGM을 바꾼 줄이 있으면 그 곡, 없으면 시퀀스 기본 BGM
        AudioClip bgm = dialogSequence.bgm;
        _currentBgmLine = -1;
        if (IsValidIndex(dialogSequence, bookmark.bgmLineIndex) && dialogSequence.lines[bookmark.bgmLineIndex].bgmOverride != null)
        {
            bgm = dialogSequence.lines[bookmark.bgmLineIndex].bgmOverride;
            _currentBgmLine = bookmark.bgmLineIndex;
        }

        if (bgm != null)
        {
            _currentBgm = bgm;
            if (AudioManager.Instance != null) AudioManager.Instance.PlayBGM(bgm);
        }

        ReplayStage(log);
        ShowDialog(bookmark.lineIndex, true);
        return true;
    }
    #endregion

    /// <summary>시퀀스 진입 시 기본 BGM 재생 (지정 안 됐으면 기존 BGM 유지)</summary>
    private void PlaySequenceBGM()
    {
        if (dialogSequence.bgm != null && AudioManager.Instance != null)
        {
            _currentBgmLine = -1;
            _currentBgm = dialogSequence.bgm;
            AudioManager.Instance.PlayBGM(dialogSequence.bgm);
        }
    }

    /// <summary>JSON 파일(Resources/Dialogue/{언어}/{sequenceId}.json)을 불러와 바로 재생</summary>
    public void LoadSequenceFromJson(string sequenceId, int startIndex = 0)
    {
        var sequence = DialogueJsonLoader.Load(sequenceId);
        if (sequence != null)
        {
            SetDialogSequence(sequence, startIndex);
        }
    }

    /// <summary>씬 진입 시 처음부터 다시 재생</summary>
    public void ResetDialog()
    {
        if (HasLines())
        {
            SetDialogSequence(dialogSequence, 0);
        }
    }

    private bool HasLines()
    {
        return dialogSequence != null && dialogSequence.lines != null && dialogSequence.lines.Length > 0;
    }

    /// <summary>sequence가 존재하고 index가 lines 범위 안인지 확인</summary>
    private static bool IsValidIndex(DialogueDataSO sequence, int index)
    {
        return sequence != null
               && sequence.lines != null
               && index >= 0
               && index < sequence.lines.Length;
    }

    public bool IsFinished => isFinished;

    /// <summary>play 명령으로 연주하러 나가 있는 동안 true</summary>
    public bool IsSuspended => isSuspended;

    /// <summary>
    /// 연주에서 돌아와 무대 상태를 조용히 복원하는 동안 true.
    /// 이 동안 발행되는 OnLineShown은 "인물 표정 갱신"일 뿐이라 UI는 타이핑/말풍선을 만들지 않아야 한다.
    /// </summary>
    public bool IsRestoring { get; private set; }

    /// <summary>Start()가 시작할 시퀀스 ID (인스펙터의 시퀀스 에셋 이름 또는 startSequenceId)</summary>
    public string StartSequenceId => dialogSequence != null ? dialogSequence.name : startSequenceId;
    public bool IsWaitingForChoice => isWaitingForChoice;

    /// <summary>현재(마지막) 시퀀스의 챕터 번호</summary>
    public int ChapterId => dialogSequence != null ? dialogSequence.chapterId : 0;

    /// <summary>현재(마지막) 시퀀스의 종료 후 동작. SceneTransitionDialogueEnd가 읽는다.</summary>
    public DialogueEndAction EndAction => dialogSequence != null ? dialogSequence.onEnd : default(DialogueEndAction);
}

/// <summary>한 줄을 화면에 표시하는 데 필요한 정보. DialogueManager.OnLineShown으로 전달된다.</summary>
public struct DialogueLineInfo
{
    /// <summary>화면 위치 슬롯 (0=Left, 1=Right, 2=Center)</summary>
    public int screenSlot;

    /// <summary>화자. null이면 내레이션(화자 없는 줄)</summary>
    public SpeakerDataSO speaker;

    /// <summary>표정 이름. 비어 있으면 첫 번째(기본) 표정</summary>
    public string expressionName;

    /// <summary>화면에 표시할 이름 ({변수} 치환 완료). 줄의 displayName이 있으면 그것, 없으면 speaker.speakerName</summary>
    public string displayName;

    /// <summary>대사 표시 방식 이름 (thought, shout, whisper ...). 비어 있으면 일반 대사</summary>
    public string style;

    /// <summary>대사 텍스트 ({변수} 치환 완료)</summary>
    public string text;
}

/// <summary>무대 상태가 바뀐 기록의 종류</summary>
public enum DialogueStageRecordKind { Cast, Command, Line }

/// <summary>무대 상태가 바뀐 기록 한 건 (cast 배치 / show·hide 명령 / 인물이 말한 줄)</summary>
public struct DialogueStageRecord
{
    public DialogueStageRecordKind kind;
    public DialogueCast[] cast;
    public string name;
    public string arg;
    public DialogueLineInfo line;
    public int lineIndex; // Line: 시퀀스 안의 줄 번호 (대사 위치 저장용)
}

/// <summary>
/// play 명령으로 연주하러 나갈 때 남기는 "돌아와서 이어갈" 정보.
/// 메모리에만 있고 파일로 저장되지 않는다. (앱을 완전히 종료하면 이어하기는 StoryFlow의 저장 위치 = 그 이야기 칸의 처음부터)
/// </summary>
public sealed class DialoguePlaySnapshot
{
    public string sequenceId;
    public int playLineIndex;
    public string songId;
    public string difficulty;
    public List<DialogueStageRecord> stageLog;
    public AudioClip bgm;
    public int bgmLineIndex = -1;
}