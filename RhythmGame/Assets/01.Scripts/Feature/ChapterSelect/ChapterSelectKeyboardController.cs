using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// ChapterSelect의 챕터 버튼 선택을 키보드로 조작한다.
/// 좌우 키는 버튼 목록을 이동하고, Enter는 EventSystem의 Submit 입력으로 현재 버튼을 실행한다.
/// </summary>
public class ChapterSelectKeyboardController : MonoBehaviour
{
    [Tooltip("좌우 키로 이동할 챕터 버튼을 화면의 순서대로 연결합니다.")]
    [SerializeField] private Button[] chapterButtons;
    [SerializeField] private KeyCode previousKey = KeyCode.LeftArrow;
    [SerializeField] private KeyCode nextKey = KeyCode.RightArrow;
    [SerializeField] private KeyCode backKey = KeyCode.Escape;

    private int selectedIndex;
    private bool isLeaving;

    private void Start()
    {
        // 좌우 이동은 이 스크립트가 관리하고, Submit(Enter)은 EventSystem이 처리한다.
        // 기본 자동 네비게이션까지 켜 두면 한 번 입력에 두 칸 이동할 수 있다.
        if (chapterButtons != null)
        {
            foreach (var button in chapterButtons)
            {
                if (button == null) continue;
                var navigation = button.navigation;
                navigation.mode = Navigation.Mode.None;
                button.navigation = navigation;
            }
        }

        selectedIndex = FindNextInteractable(-1, 1);
        SetEventSystemSelection();
    }

    private void Update()
    {
        if (isLeaving || SettingsPopup.BlocksInput || StoryContinuePopup.BlocksInput) return;

        if (Input.GetKeyDown(previousKey))
        {
            MoveSelection(-1);
        }
        else if (Input.GetKeyDown(nextKey))
        {
            MoveSelection(1);
        }
        else if (Input.GetKeyDown(backKey))
        {
            GoToLobby();
        }
    }

    private void MoveSelection(int direction)
    {
        if (chapterButtons == null || chapterButtons.Length == 0) return;

        int next = FindNextInteractable(selectedIndex, direction);
        if (next < 0) return;

        selectedIndex = next;
        SetEventSystemSelection();
    }

    private int FindNextInteractable(int startIndex, int direction)
    {
        if (chapterButtons == null || chapterButtons.Length == 0) return -1;

        for (int step = 1; step <= chapterButtons.Length; step++)
        {
            int index = (startIndex + direction * step) % chapterButtons.Length;
            if (index < 0) index += chapterButtons.Length;

            if (chapterButtons[index] != null && chapterButtons[index].isActiveAndEnabled && chapterButtons[index].interactable)
                return index;
        }

        return -1;
    }

    private void SetEventSystemSelection()
    {
        if (selectedIndex < 0 || chapterButtons == null || selectedIndex >= chapterButtons.Length) return;

        var button = chapterButtons[selectedIndex];
        if (button == null || EventSystem.current == null) return;

        EventSystem.current.SetSelectedGameObject(button.gameObject);
    }

    private void GoToLobby()
    {
        if (SceneChanger.Instance == null)
        {
            Debug.LogError("[ChapterSelectKeyboardController] SceneChanger가 없습니다. Loading 씬부터 실행해야 합니다.", this);
            return;
        }

        isLeaving = true;
        SceneChanger.Instance.LoadScene(SceneChanger.SceneType.Lobby);
    }
}
