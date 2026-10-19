using UnityEngine;

/// <summary>
/// 지정한 키(기본 ESC)를 누르면 게임을 종료한다. 로비에서 사용.
/// </summary>
public class QuitGameKey : MonoBehaviour
{
    [SerializeField] private KeyCode quitKey = KeyCode.Escape;

    private void Update()
    {
        if (SettingsPopup.BlocksInput) return;
        if (!Input.GetKeyDown(quitKey)) return;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // 에디터에서는 플레이 중지
#else
        Application.Quit();
#endif
    }
}
