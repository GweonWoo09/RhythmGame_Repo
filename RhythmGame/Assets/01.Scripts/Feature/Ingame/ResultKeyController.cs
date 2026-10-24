using UnityEngine;

// 결과 화면 키 입력: F5 = 같은 곡 다시 플레이, Enter = 곡 선택(돌아갈 씬)으로 이동
public class ResultKeyController : MonoBehaviour
{
    [SerializeField] private KeyCode retryKey = KeyCode.F5;
    [SerializeField] private KeyCode exitKey = KeyCode.Return;

    private void Update()
    {
        if (SettingsPopup.BlocksInput) return;

        if (Input.GetKeyDown(retryKey)) Retry();
        else if (Input.GetKeyDown(exitKey)) Exit();
    }

    private void Retry()
    {
        if (PlaySession.Song == null) return;
        PlaySession.Start(PlaySession.Song, PlaySession.ReturnScene);
    }

    private void Exit()
    {
        SceneChanger.Instance.LoadScene(PlaySession.ReturnScene);
    }
}
