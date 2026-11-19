using UnityEngine;

/// <summary>
/// 설정이 바뀌면 화면 해상도를 적용한다. (에디터에서는 적용되지 않고 빌드에서만 바뀐다)
/// 16:9보다 넓은 해상도는 16:9로 바꿔서, 와이드 모니터에서는 양옆이 검은 여백이 된다.
/// </summary>
public class ResolutionSettingsApplier : MonoBehaviour
{
    private int appliedIndex = -1;

    private void Start()
    {
        var dm = DataManager.Instance;
        if (dm == null) return;

        dm.OnSettingsChanged += Apply;
        Apply(dm.Settings);
    }

    private void OnDestroy()
    {
        if (DataManager.Instance != null) DataManager.Instance.OnSettingsChanged -= Apply;
    }

    private void Apply(SettingsData data)
    {
        if (data.resolutionIndex == appliedIndex) return; // 다른 설정만 바뀐 경우
        appliedIndex = data.resolutionIndex;

        var size = ResolutionOptions.Get(data.resolutionIndex);

        // [수정] UI 그림이 16:9라서 더 넓게 그리면 늘어난다. 높이에 맞춘 16:9로 바꾸면 전체 화면에서 양옆에 검은 여백이 생긴다
        int width16x9 = size.y * 16 / 9;
        if (size.x > width16x9) size.x = width16x9;

        Screen.SetResolution(size.x, size.y, Screen.fullScreenMode);
    }
}
