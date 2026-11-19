using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 설정창 열기/닫기만 담당한다. F8로 열고, F8이나 ESC로 닫는다.
/// 설정창이 열려 있는 동안 다른 입력(메뉴, 나가기)은 BlocksInput으로 막고, 뒤 화면 버튼의 Enter/방향키(EventSystem)도 멈춘다.
/// </summary>
public class SettingsPopup : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private KeyCode toggleKey = KeyCode.F8;
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;

    private static bool isOpen;
    private static int closedFrame = -1;
    private static SettingsPopup current; // [수정] 코드에서 닫기용

    // 닫힌 그 프레임에도 막아야, ESC 한 번에 '설정 닫기'와 '나가기'가 같이 실행되지 않는다
    public static bool BlocksInput => isOpen || Time.frameCount == closedFrame;

    // [수정] 열고 닫을 때 알림 (인게임 일시정지 등에서 구독)
    public static event Action Opened;
    public static event Action Closed;

    private void Awake()
    {
        isOpen = false;
        current = this; // [수정]
        if (panel != null) panel.SetActive(false);
    }

    private void Update()
    {
        if (CalibrationPanel.BlocksInput) return; // [수정] 자동 보정 중에는 ESC/F8로 설정창을 닫지 않는다

        if (Input.GetKeyDown(toggleKey)) SetOpen(!isOpen);
        else if (isOpen && Input.GetKeyDown(closeKey)) SetOpen(false);
    }

    /// <summary>[수정] 열려 있으면 닫는다 (인게임에서 곡 나가기 등)</summary>
    public static void Close()
    {
        if (current != null && isOpen) current.SetOpen(false);
    }

    private void SetOpen(bool open)
    {
        isOpen = open;
        if (panel != null) panel.SetActive(open);
        if (!open) closedFrame = Time.frameCount;

        // [수정] EventSystem은 BlocksInput을 모르므로, 열려 있는 동안 뒤 화면 버튼이 Enter(Submit)를 받지 않게 멈춘다
        if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = !open;

        if (open) Opened?.Invoke(); // [수정]
        else Closed?.Invoke();
    }
}
