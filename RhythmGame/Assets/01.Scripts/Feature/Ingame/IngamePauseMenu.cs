using RhythmGameStarter;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 리듬게임 중 ESC로 여는 일시정지 메뉴. 열기/닫기와 각 항목이 할 일만 담당한다.
/// 곡 멈춤/재개는 IngamePauseController, 항목 이동(↑↓ Enter)은 KeyboardMenu가 맡는다.
/// </summary>
public class IngamePauseMenu : MonoBehaviour
{
    [SerializeField] private IngamePauseController pauseController;
    [SerializeField] private SongManager songManager;
    [SerializeField] private GameObject panel;
    [SerializeField] private KeyboardMenu menu;
    [SerializeField] private KeyCode toggleKey = KeyCode.Escape;

    private static bool isOpen;
    private static int closedFrame = -1;
    private bool leaving;

    /// <summary>메뉴가 열려 있거나 방금 닫힌 프레임이면 true (ESC 한 번에 두 가지가 실행되지 않게)</summary>
    public static bool BlocksInput => isOpen || Time.frameCount == closedFrame;

    private void Awake()
    {
        isOpen = false;
        if (panel != null) panel.SetActive(false);
    }

    private void OnDestroy() => isOpen = false;

    private void Update()
    {
        if (leaving || SettingsPopup.BlocksInput) return;
        if (!Input.GetKeyDown(toggleKey)) return;

        if (isOpen) Resume();
        else Open();
    }

    private void Open()
    {
        if (!songManager.SongHasStarted) return;

        isOpen = true;
        panel.SetActive(true);
        menu.ResetSelection(); // 항상 '계속하기'부터
        pauseController.Hold(this);
    }

    private void Close()
    {
        isOpen = false;
        closedFrame = Time.frameCount;
        panel.SetActive(false);
    }

    // ===== 메뉴 항목 (MenuItem의 On Submit에 연결) =====

    public void Resume()
    {
        if (!isOpen) return;
        Close();
        pauseController.Release(this); // 카운트다운 후 재개
    }

    public void Restart() => Leave(SceneChanger.SceneType.RhythmPlay);

    public void GoToSongSelect() => Leave(SceneChanger.SceneType.SongSelect);

    public void GoToLobby() => Leave(SceneChanger.SceneType.Lobby);

    // 결과 화면으로 가지 않고 기록도 저장하지 않은 채 나간다
    private void Leave(SceneChanger.SceneType target)
    {
        if (leaving) return;
        leaving = true;

        songManager.StopSong(true); // true: 곡 종료 이벤트(결과 화면 이동)를 실행하지 않는다

        if (SceneChanger.Instance != null)
        {
            SceneChanger.Instance.LoadScene(target);
        }
        else if (target == SceneChanger.SceneType.RhythmPlay)
        {
            // 에디터에서 InGame 씬만 켜고 테스트할 때 (Loading 씬 없이)
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
        else
        {
            Debug.LogWarning("[IngamePauseMenu] SceneChanger가 없습니다. Loading 씬부터 실행해야 다른 화면으로 이동합니다.", this);
        }
    }
}
