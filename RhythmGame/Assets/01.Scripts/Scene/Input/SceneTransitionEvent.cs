/// <summary>
/// UnityEvent(메뉴 선택, 곡 종료 등)에 연결해서 씬을 이동할 때 사용한다.
/// 실제 이동은 SceneTransitionTrigger의 공통 로직이 처리한다.
/// </summary>
public class SceneTransitionEvent : SceneTransitionTrigger
{
    public void Go() => GoToTargetScene();
    public void GoBack() => GoToPreviousScene();
}
