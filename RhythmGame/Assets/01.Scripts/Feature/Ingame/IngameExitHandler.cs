using RhythmGameStarter;
using UnityEngine;

/// <summary>
/// 설정창의 '곡 나가기'를 받아서 곡을 멈추고 곡 선택(들어오기 전 화면)으로 돌아간다.
/// 결과 화면으로 가지 않고 기록도 저장하지 않는다.
/// </summary>
public class IngameExitHandler : MonoBehaviour
{
    [SerializeField] private SongManager songManager;

    private bool exiting;

    private void OnEnable() => SongExitRequest.Register(Exit);

    private void OnDisable() => SongExitRequest.Unregister(Exit);

    private void Exit()
    {
        if (exiting) return;
        exiting = true;

        songManager.StopSong(true); // true: 곡 종료 이벤트(결과 화면 이동)를 실행하지 않는다
        SettingsPopup.Close();

        if (SceneChanger.Instance != null)
            SceneChanger.Instance.LoadScene(PlaySession.ReturnScene);
    }
}
