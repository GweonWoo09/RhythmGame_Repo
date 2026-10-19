using UnityEngine;

/// <summary>
/// 설정창 열기/닫기만 담당한다. F8로 열고, F8이나 ESC로 닫는다.
/// 설정창이 열려 있는 동안 다른 입력(메뉴, 나가기)은 BlocksInput으로 막는다.
/// </summary>
public class SettingsPopup : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private KeyCode toggleKey = KeyCode.F8;
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;

    private static bool isOpen;
    private static int closedFrame = -1;

    // 닫힌 그 프레임에도 막아야, ESC 한 번에 '설정 닫기'와 '나가기'가 같이 실행되지 않는다
    public static bool BlocksInput => isOpen || Time.frameCount == closedFrame;

    private void Awake()
    {
        isOpen = false;
        if (panel != null) panel.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey)) SetOpen(!isOpen);
        else if (isOpen && Input.GetKeyDown(closeKey)) SetOpen(false);
    }

    private void SetOpen(bool open)
    {
        isOpen = open;
        if (panel != null) panel.SetActive(open);
        if (!open) closedFrame = Time.frameCount;
    }
}
