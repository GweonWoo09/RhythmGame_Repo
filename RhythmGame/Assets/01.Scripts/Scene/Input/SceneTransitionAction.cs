/// <summary>
/// 다른 스크립트나 UnityEvent(MenuItem의 On Submit 등)에서 호출해 지정한 씬으로 전환한다.
/// 사용법: 오브젝트에 붙이고 targetScene을 고른 뒤, 이벤트에 Go를 연결한다.
/// </summary>
public class SceneTransitionAction : SceneTransitionTrigger
{
    public void Go() => GoToTargetScene();
}
