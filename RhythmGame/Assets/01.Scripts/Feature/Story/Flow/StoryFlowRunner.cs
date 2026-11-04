using UnityEngine;

/// <summary>
/// GameManager.OnPlayResultProcessed(곡이 끝나 결과가 처리됨)를 StoryFlow에 전달하는 얇은 중계기.
/// StoryFlow가 시작될 때 자동으로 만들어지고 씬이 바뀌어도 유지되므로, 씬에 따로 배치할 필요가 없다.
///
/// 흐름이 돌고 있을 때만 결과를 가로챈다. 자유 플레이 등 흐름 밖의 연주는 건드리지 않는다.
/// (Result 씬 이동을 GameManager 쪽에 연결할 때는 StoryFlow.IsRunning일 때 건너뛰어야 이중 이동이 생기지 않는다.)
/// </summary>
public class StoryFlowRunner : MonoBehaviour
{
    private static StoryFlowRunner _instance;
    private bool _subscribed;

    /// <summary>러너가 없으면 만든다 (DontDestroyOnLoad)</summary>
    public static void EnsureExists()
    {
        if (_instance != null)
        {
            _instance.TrySubscribe();
            return;
        }

        var host = new GameObject("StoryFlowRunner");
        DontDestroyOnLoad(host);

        _instance = host.AddComponent<StoryFlowRunner>();
        _instance.TrySubscribe();
    }

    private void Update()
    {
        // GameManager가 나중에 생기는 경우(부트 씬 순서)를 위해 계속 시도한다. 구독이 끝나면 비용은 bool 검사뿐.
        if (!_subscribed) TrySubscribe();
    }

    private void TrySubscribe()
    {
        if (_subscribed || GameManager.Instance == null) return;

        GameManager.Instance.OnPlayResultProcessed += HandlePlayResult;
        _subscribed = true;
    }

    private void HandlePlayResult(GameManager.PlayResult result)
    {
        StoryFlow.HandlePlayResult(result);
    }

    private void OnDestroy()
    {
        if (_subscribed && GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayResultProcessed -= HandlePlayResult;
        }

        if (_instance == this) _instance = null;
    }
}