using UnityEngine;

/// <summary>
/// 게임이 시작되면 Resources/UI/SettingsPopup 프리팹을 한 번 만들고 씬이 바뀌어도 유지한다.
/// 씬마다 설정창을 배치할 필요가 없다.
/// </summary>
public static class SettingsPopupBootstrap
{
    private const string PrefabPath = "UI/SettingsPopup";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null) return;

        var popup = Object.Instantiate(prefab);
        popup.name = prefab.name;
        Object.DontDestroyOnLoad(popup);
    }
}
