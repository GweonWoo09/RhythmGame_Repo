using UnityEngine;

/// <summary>설정이 바뀌면 화면 해상도를 적용한다. (에디터에서는 적용되지 않고 빌드에서만 바뀐다)</summary>
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
        Screen.SetResolution(size.x, size.y, Screen.fullScreenMode);
    }
}
