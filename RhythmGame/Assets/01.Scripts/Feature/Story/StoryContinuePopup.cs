using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 저장된 스토리 진행(대사 위치)이 있을 때 "이어서 하기 / 처음부터" 를 고르는 창 (StoryPopup).
///
///   ChapterSelect : startButton(StartBtn)을 누르면 저장된 위치가 있을 때만 창을 띄우고, 없으면 바로 Story로 간다.
///                   (StartBtn에 SceneTransitionButton이 붙어 있으면 이 컴포넌트가 끄고 대신 처리한다)
///   Lobby         : openOnStartIfInterrupted를 켜 두면, 스토리 도중 게임을 종료해 저장된 위치가 있을 때 씬이 열리자마자 창을 띄운다.
///                   한 번 띄운 뒤에는 종료 표시를 지워서 Lobby에 올 때마다 다시 뜨지 않는다.
///
/// 이어서 하기(loadButton) : 저장된 위치 그대로 Story 씬으로 간다 (DialogueManager가 그 줄부터 이어간다).
/// 처음부터(newButton)     : 저장된 위치를 지우고 Story 씬으로 간다.
/// ESC / 취소(cancelButton) : 창을 닫는다. 저장된 위치는 남으므로 StartBtn을 다시 누르면 창이 또 뜬다.
/// 창 안에서는 ←→/↑↓ 로 버튼을 고르고 Enter로 누른다 (EventSystem 기본 네비게이션).
/// </summary>
public class StoryContinuePopup : MonoBehaviour
{
    [Tooltip("열고 닫을 창 (StoryPopup). 비워 두면 이 오브젝트")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Button loadButton;
    [SerializeField] private Button newButton;
    [Tooltip("창을 닫는 버튼. 비워 두면 panel 안에서 이름이 'CancelBtn'인 버튼을 찾는다. 닫아도 저장된 위치는 남는다.")]
    [SerializeField] private Button cancelButton;

    [Header("ChapterSelect: 누르면 이 창을 거쳐 Story로 가는 버튼 (선택)")]
    [SerializeField] private Button startButton;

    [Header("Lobby: 게임 종료로 저장된 진행이 있으면 시작하자마자 창을 띄운다")]
    [SerializeField] private bool openOnStartIfInterrupted;

    [SerializeField] private KeyCode closeKey = KeyCode.Escape;
    [SerializeField] private SceneChanger.SceneType storyScene = SceneChanger.SceneType.Story;

    private static bool isOpen;
    private static bool isLeaving;
    private static int closedFrame = -1;

    /// <summary>창이 열려 있거나(닫힌 그 프레임 포함) Story로 떠나는 중이면 true. 다른 메뉴 입력은 이때 멈춘다.</summary>
    public static bool BlocksInput => isOpen || isLeaving || Time.frameCount == closedFrame;

    /// <summary>이어서 할 스토리 진행이 저장되어 있는가</summary>
    public static bool HasSavedStory => GameManager.Instance != null && GameManager.Instance.GetStoryBookmark() != null;

    private void Awake()
    {
        if (panel == null) panel = gameObject;

        isOpen = false;
        isLeaving = false;
        closedFrame = -1;

        if (loadButton != null) loadButton.onClick.AddListener(ContinueStory);
        if (newButton != null) newButton.onClick.AddListener(StartNewStory);

        if (cancelButton == null) cancelButton = FindButtonInPanel("CancelBtn");
        if (cancelButton != null) cancelButton.onClick.AddListener(Close);

        if (startButton != null)
        {
            // 씬 이동은 이 컴포넌트가 맡는다 (SceneTransitionButton이 같이 있으면 창 없이 바로 이동해 버린다)
            var transition = startButton.GetComponent<SceneTransitionButton>();
            if (transition != null) transition.enabled = false;

            startButton.onClick.AddListener(RequestStart);
        }
    }

    private void Start()
    {
        // panel이 이 오브젝트 자신이면 꺼 버리면 Update가 돌지 않으므로, 자식 창을 연결해 두는 것을 권장한다
        if (panel != null) panel.SetActive(false);

        if (!openOnStartIfInterrupted || GameManager.Instance == null) return;

        var bookmark = GameManager.Instance.GetStoryBookmark();
        if (bookmark == null || !bookmark.interrupted) return;

        // 한 번만 띄운다 (이어서 하기를 고르지 않아도 위치는 남아 ChapterSelect에서 다시 고를 수 있다)
        GameManager.Instance.ClearStoryInterrupted();
        Open();
    }

    private void OnDestroy()
    {
        if (loadButton != null) loadButton.onClick.RemoveListener(ContinueStory);
        if (newButton != null) newButton.onClick.RemoveListener(StartNewStory);
        if (cancelButton != null) cancelButton.onClick.RemoveListener(Close);
        if (startButton != null) startButton.onClick.RemoveListener(RequestStart);

        isOpen = false;
        isLeaving = false;
    }

    private void Update()
    {
        if (!isOpen || isLeaving || SettingsPopup.BlocksInput) return;

        if (Input.GetKeyDown(closeKey)) Close();
    }

    /// <summary>스토리 시작 요청: 저장된 진행이 있으면 창을 띄우고, 없으면 바로 Story로 간다.</summary>
    public void RequestStart()
    {
        if (isOpen || isLeaving) return;

        if (HasSavedStory) Open();
        else LoadStory();
    }

    public void Open()
    {
        if (isOpen || isLeaving) return;

        isOpen = true;
        if (panel != null) panel.SetActive(true);

        // 기본은 '이어서 하기'
        Select(loadButton != null ? loadButton.gameObject : null);
    }

    public void Close()
    {
        if (!isOpen) return;

        isOpen = false;
        closedFrame = Time.frameCount;
        if (panel != null) panel.SetActive(false);

        // 창을 연 버튼(StartBtn)으로 선택을 돌려준다. 없으면 비운다 (남아 있으면 Enter가 숨은 버튼을 누른다)
        Select(startButton != null ? startButton.gameObject : null);
    }

    private void ContinueStory()
    {
        if (!isOpen || isLeaving) return;
        LoadStory();
    }

    private void StartNewStory()
    {
        if (!isOpen || isLeaving) return;

        if (GameManager.Instance != null) GameManager.Instance.SetStoryBookmark(null);
        LoadStory();
    }

    private void LoadStory()
    {
        if (SceneChanger.Instance == null)
        {
            Debug.LogError("[StoryContinuePopup] SceneChanger가 없습니다. Loading 씬부터 실행해야 합니다.", this);
            return;
        }

        isLeaving = true;
        Select(null);
        SceneChanger.Instance.LoadScene(storyScene);
    }

    private Button FindButtonInPanel(string buttonName)
    {
        if (panel == null) return null;

        foreach (var button in panel.GetComponentsInChildren<Button>(true))
        {
            if (button.name == buttonName) return button;
        }

        return null;
    }

    private static void Select(GameObject target)
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(target);
    }
}
