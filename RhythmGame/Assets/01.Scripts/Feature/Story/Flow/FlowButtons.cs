using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 챕터 선택 / 타이틀 화면의 버튼에 붙여서 이야기 흐름을 시작한다.
///   NewGame  : 흐름의 처음(튜토리얼)부터
///   Chapter  : chapterId 챕터의 시작 칸부터 (해금되지 않았으면 시작하지 않는다)
///   Continue : 저장된 위치에서 이어서 (저장된 위치가 없으면 버튼이 비활성화된다)
///
/// SceneTransitionButton과 같은 방식으로, 버튼에 붙이고 설정만 고르면 된다.
/// </summary>
[RequireComponent(typeof(Button))]
public class FlowStartButton : MonoBehaviour
{
    public enum StartMode { NewGame, Chapter, Continue }

    [SerializeField] private StartMode mode = StartMode.NewGame;

    [Tooltip("시작할 흐름 파일 이름 (Resources/Flow/{flowId}.json)")]
    [SerializeField] private string flowId = StoryFlow.DefaultFlowId;

    [Tooltip("Chapter 모드에서 시작할 챕터 번호")]
    [SerializeField] private int chapterId = 1;

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(OnClick);
    }

    private void OnDestroy()
    {
        if (_button != null) _button.onClick.RemoveListener(OnClick);
    }

    private void OnEnable()
    {
        // 화면이 열릴 때마다 저장 위치 유무를 다시 확인한다
        if (mode == StartMode.Continue && _button != null)
        {
            _button.interactable = StoryFlow.HasSavedProgress;
        }
    }

    private void OnClick()
    {
        switch (mode)
        {
            case StartMode.NewGame:
                StoryFlow.StartFlow(flowId);
                break;

            case StartMode.Chapter:
                StoryFlow.StartChapter(chapterId, flowId);
                break;

            case StartMode.Continue:
                StoryFlow.ResumeSaved();
                break;
        }
    }
}

/// <summary>
/// 결과 화면의 "계속" 버튼에 붙인다.
/// 이야기 흐름이 돌고 있으면 다음 곡 / 다음 스토리로 진행하고,
/// 흐름 밖(자유 플레이)이면 fallbackScene으로 이동한다.
/// </summary>
[RequireComponent(typeof(Button))]
public class FlowResultContinueButton : MonoBehaviour
{
    [Tooltip("이야기 흐름이 돌고 있지 않을 때 이동할 씬")]
    [SerializeField] private SceneChanger.SceneType fallbackScene = SceneChanger.SceneType.Lobby;

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(OnClick);
    }

    private void OnDestroy()
    {
        if (_button != null) _button.onClick.RemoveListener(OnClick);
    }

    private void OnClick()
    {
        // 흐름이 돌고 있거나, 대사 도중 play 명령으로 연주하러 나온 경우
        if (StoryFlow.IsRunning || StoryFlow.HasInlinePending)
        {
            StoryFlow.ContinueAfterResult();
            return;
        }

        if (SceneChanger.Instance != null)
        {
            SceneChanger.Instance.LoadScene(fallbackScene);
        }
    }
}
