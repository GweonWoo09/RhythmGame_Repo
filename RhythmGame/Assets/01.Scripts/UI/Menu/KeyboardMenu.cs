using UnityEngine;

/// <summary>
/// 키보드로 메뉴를 고르는 기능만 담당한다. (↑↓ 선택, Enter 실행)
/// 항목의 모양과 실행 내용은 각 MenuItem이 가진다.
/// </summary>
public class KeyboardMenu : MonoBehaviour
{
    [Tooltip("위에서부터 순서대로 넣는다")]
    [SerializeField] private MenuItem[] items;
    [SerializeField] private KeyCode upKey = KeyCode.UpArrow;
    [SerializeField] private KeyCode downKey = KeyCode.DownArrow;
    [SerializeField] private KeyCode submitKey = KeyCode.Return;

    private int index;

    private void Start() => Refresh();

    private void Update()
    {
        if (items == null || items.Length == 0) return;
        if (SettingsPopup.BlocksInput) return; // 설정창이 열려 있으면 메뉴 조작 안 함

        if (Input.GetKeyDown(upKey)) Move(-1);
        else if (Input.GetKeyDown(downKey)) Move(1);
        else if (Input.GetKeyDown(submitKey) || Input.GetKeyDown(KeyCode.KeypadEnter))
            items[index].Submit();
    }

    private void Move(int direction)
    {
        index = (index + direction + items.Length) % items.Length; // 끝에서 처음으로 돌아감
        Refresh();
    }

    private void Refresh()
    {
        for (int i = 0; i < items.Length; i++)
            items[i].SetSelected(i == index);
    }
}
