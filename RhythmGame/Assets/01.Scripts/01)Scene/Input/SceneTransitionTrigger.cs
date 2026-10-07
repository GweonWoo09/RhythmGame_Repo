using UnityEngine;

/// <summary>
/// "무엇을 계기로 씬을 전환할지"(버튼 클릭, 키 입력 등)는 자식 클래스가 결정하고,
/// 이 클래스는 "실제로 씬을 어떻게 전환할지"만 공통으로 제공한다.
/// SceneTransitionButton, SceneTransitionKey가 이 클래스를 상속받는다.
/// </summary>
public abstract class SceneTransitionTrigger : MonoBehaviour
{
    [Tooltip("이동할 씬")]
    [SerializeField] protected SceneChanger.SceneType targetScene;

    [Header("챕터/곡 선택처럼 데이터를 함께 넘겨야 할 때 (선택)")]
    [SerializeField] protected bool sendPayload = false;
    [SerializeField] protected int chapterId;
    [SerializeField] protected string songId;
    [SerializeField] protected string difficulty;

    /// <summary>targetScene으로 이동한다 (sendPayload가 켜져 있으면 데이터도 함께 전달)</summary>
    protected void GoToTargetScene()
    {
        if (SceneChanger.Instance == null)
        {
            Debug.LogError("[SceneTransitionTrigger] 씬에 SceneChanger 인스턴스가 없습니다.");
            return;
        }

        if (sendPayload)
        {
            var payload = new SceneChanger.ScenePayload
            {
                chapterId = chapterId,
                songId = songId,
                difficulty = difficulty
            };
            SceneChanger.Instance.LoadScene(targetScene, payload);
        }
        else
        {
            SceneChanger.Instance.LoadScene(targetScene);
        }
    }

    /// <summary>바로 이전에 있던 씬으로 되돌아간다</summary>
    protected void GoToPreviousScene()
    {
        if (SceneChanger.Instance == null)
        {
            Debug.LogError("[SceneTransitionTrigger] 씬에 SceneChanger 인스턴스가 없습니다.");
            return;
        }

        SceneChanger.Instance.LoadPreviousScene();
    }
}