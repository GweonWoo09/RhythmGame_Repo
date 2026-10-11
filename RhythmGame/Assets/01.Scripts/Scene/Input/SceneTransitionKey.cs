using UnityEngine;

/// <summary>
/// 지정한 키를 누르면 targetScene으로 이동하고, 다른 키를 누르면 이전 씬으로 돌아간다.
/// 기본값은 Space=이동, Esc=이전 씬 복귀 (둘 다 Inspector에서 변경 가능).
/// </summary>
public class SceneTransitionKey : SceneTransitionTrigger
{
    [Header("키 설정")]
    [Tooltip("누르면 targetScene으로 이동")]
    [SerializeField] private KeyCode advanceKey = KeyCode.Space;

    [Tooltip("누르면 이전 씬으로 되돌아감")]
    [SerializeField] private KeyCode backKey = KeyCode.Escape;

    private void Update()
    {
        if (Input.GetKeyDown(advanceKey))
        {
            GoToTargetScene();
        }
        else if (Input.GetKeyDown(backKey))
        {
            GoToPreviousScene();
        }
    }
}