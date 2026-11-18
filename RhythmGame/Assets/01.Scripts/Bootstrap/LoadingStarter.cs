using UnityEngine;

/// <summary>
/// 게임을 시작하면(Loading 씬) 첫 화면으로 자동 이동한다.
/// SceneChanger와 같은 오브젝트(Managers)에 붙여서 사용.
///
/// Loading 씬에 LoadingBar가 있으면: 화면을 가리지 않은 채 첫 화면을 미리 불러오면서 로딩바를 채우고,
/// 로딩바가 끝까지 찬 뒤에 페이드아웃하고 넘어간다. LoadingBar가 없으면 예전처럼 바로 이동한다.
/// </summary>
public class LoadingStarter : MonoBehaviour
{
    [Tooltip("Loading 다음에 처음 보여줄 씬")]
    [SerializeField] private SceneChanger.SceneType firstScene = SceneChanger.SceneType.Lobby;

    [Tooltip("로딩바. 비우면 Loading 씬에서 찾는다.")]
    [SerializeField] private LoadingBar loadingBar;

    private void Start()
    {
        if (SceneChanger.Instance == null)
        {
            Debug.LogError("[LoadingStarter] SceneChanger가 없습니다.");
            return;
        }

        if (loadingBar == null) loadingBar = FindFirstObjectByType<LoadingBar>();

        if (loadingBar == null || !loadingBar.isActiveAndEnabled)
        {
            SceneChanger.Instance.LoadScene(firstScene);
            return;
        }

        // 로딩바가 다 차기 전에는 첫 화면을 띄우지 않는다 (로딩바가 사라졌으면 기다리지 않는다)
        LoadingBar bar = loadingBar;
        SceneChanger.Instance.LoadSceneInBackground(firstScene, () => bar == null || bar.IsComplete);
    }
}
