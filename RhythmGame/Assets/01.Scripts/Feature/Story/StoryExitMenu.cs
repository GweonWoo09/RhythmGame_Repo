using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Story 씬의 나가기 창.
///   ESC         : 나가기 창(ExitUI)을 열고 닫는다.
///   나가기 버튼 : 지금 보고 있는 대사까지 저장(DialogueManager.SaveBookmark)하고 ChapterSelect로 돌아간다.
///   계속 버튼   : 창을 닫고 대사를 이어간다.
/// 창이 열려 있는 동안에는 BlocksInput이 true라 대사 진행/선택지/오토 모드 입력이 막힌다.
/// 창 안에서는 ←→ 로 버튼을 고르고 Enter로 누른다 (EventSystem 기본 네비게이션).
/// </summary>
public class StoryExitMenu : MonoBehaviour
{
    [SerializeField] private DialogueManager dialogueManager;
    [SerializeField] private GameObject exitPanel;
    [SerializeField] private Button exitButton;
    [SerializeField] private Button resumeButton;
    [SerializeField] private KeyCode toggleKey = KeyCode.Escape;
    [SerializeField] private SceneChanger.SceneType exitScene = SceneChanger.SceneType.ChapterSelect;

    private static bool isOpen;
    private static bool isLeaving;
    private static int closedFrame = -1;

    /// <summary>나가기 창이 열려 있거나(닫힌 그 프레임 포함) 씬을 떠나는 중이면 true</summary>
    public static bool BlocksInput => isOpen || isLeaving || Time.frameCount == closedFrame;

    /// <summary>나가기 버튼으로 이미 저장하고 씬을 떠나는 중인가</summary>
    public static bool IsLeaving => isLeaving;

    private void Awake()
    {
        isOpen = false;
        isLeaving = false;
        closedFrame = -1;

        if (exitPanel != null) exitPanel.SetActive(false);
        if (exitButton != null) exitButton.onClick.AddListener(ExitToChapterSelect);
        if (resumeButton != null) resumeButton.onClick.AddListener(Close);
    }

    private void OnDestroy()
    {
        if (exitButton != null) exitButton.onClick.RemoveListener(ExitToChapterSelect);
        if (resumeButton != null) resumeButton.onClick.RemoveListener(Close);

        isOpen = false;
        isLeaving = false;
    }

    private void Update()
    {
        if (isLeaving || SettingsPopup.BlocksInput) return;

        if (Input.GetKeyDown(toggleKey))
        {
            if (isOpen) Close();
            else Open();
        }
    }

    public void Open()
    {
        if (isOpen || isLeaving) return;

        isOpen = true;
        if (exitPanel != null) exitPanel.SetActive(true);

        // 실수로 나가지 않도록 '계속'을 먼저 고른다. ←→ 와 Enter는 EventSystem이 처리한다.
        Select(resumeButton != null ? resumeButton.gameObject : null);
    }

    public void Close()
    {
        if (!isOpen) return;

        isOpen = false;
        closedFrame = Time.frameCount;
        if (exitPanel != null) exitPanel.SetActive(false);

        // 버튼이 선택된 채로 남으면 이후 Enter/Space가 그 버튼을 눌러 버린다
        Select(null);
    }

    private void ExitToChapterSelect()
    {
        if (isLeaving) return;

        if (SceneChanger.Instance == null)
        {
            Debug.LogError("[StoryExitMenu] SceneChanger가 없습니다. Loading 씬부터 실행해야 합니다.", this);
            return;
        }

        isLeaving = true;

        if (dialogueManager != null) dialogueManager.SaveBookmark();

        // 이야기 흐름이 돌고 있었다면 멈춘다 (흐름의 저장 위치는 그대로 남는다)
        if (StoryFlow.IsRunning) StoryFlow.Stop();

        Select(null);
        SceneChanger.Instance.LoadScene(exitScene);
    }

    private static void Select(GameObject target)
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(target);
    }
}
