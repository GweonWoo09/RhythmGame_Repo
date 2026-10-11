using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 버튼에 붙여서 클릭 시 SceneTransitionTrigger의 공통 로직으로 지정한 씬으로 전환한다.
/// 사용법: 버튼 오브젝트에 이 스크립트를 붙이고 Inspector에서 targetScene만 선택하면 끝.
/// </summary>
[RequireComponent(typeof(Button))]
public class SceneTransitionButton : SceneTransitionTrigger
{
    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        _button.onClick.AddListener(GoToTargetScene);
    }

    private void OnDisable()
    {
        _button.onClick.RemoveListener(GoToTargetScene);
    }
}