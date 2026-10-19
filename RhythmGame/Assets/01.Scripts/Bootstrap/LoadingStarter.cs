using UnityEngine;

/// <summary>
/// 게임을 시작하면(Loading 씬) 첫 화면으로 자동 이동한다.
/// SceneChanger와 같은 오브젝트(Managers)에 붙여서 사용.
/// </summary>
public class LoadingStarter : MonoBehaviour
{
    [Tooltip("Loading 다음에 처음 보여줄 씬")]
    [SerializeField] private SceneChanger.SceneType firstScene = SceneChanger.SceneType.Lobby;

    private void Start()
    {
        if (SceneChanger.Instance == null)
        {
            Debug.LogError("[LoadingStarter] SceneChanger가 없습니다.");
            return;
        }

        SceneChanger.Instance.LoadScene(firstScene);
    }
}
