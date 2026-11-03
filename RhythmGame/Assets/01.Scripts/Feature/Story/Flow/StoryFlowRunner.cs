using UnityEngine;

/// <summary>
/// 스토리와 리듬게임간의 플로우를 연결하기 위해
/// 리듬게임 끝나고 결과창으로 넘어가도록
/// 리듬게임 끝나고 결과창으로 넘어가도록
/// </summary>
public sealed class StoryFlowRunner : MonoBehaviour
{
    private static StoryFlowRunner _instance;
    private GameManager _subscribedManager;

    public static void EnsureExists()
    {
        if (_instance != null) return;
        var go = new GameObject(nameof(StoryFlowRunner));
        _instance = go.AddComponent<StoryFlowRunner>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        SubscribeToManager();
    }

    private void Update()
    {
        if (_subscribedManager != GameManager.Instance) SubscribeToManager();
    }

    private void OnDestroy()
    {
        Unsubscribe();
        if (_instance == this) _instance = null;
    }

    private void SubscribeToManager()
    {
        Unsubscribe();
        _subscribedManager = GameManager.Instance;
        if (_subscribedManager != null) _subscribedManager.OnPlayResultProcessed += HandleResult;
    }

    private void Unsubscribe()
    {
        if (_subscribedManager != null) _subscribedManager.OnPlayResultProcessed -= HandleResult;
        _subscribedManager = null;
    }

    private static void HandleResult(GameManager.PlayResult result)
    {
        StoryFlow.HandlePlayResult(result);
    }
}
