using TMPro;
using UnityEngine;

/// <summary>
/// 설정창의 한 줄. 값을 어떻게 보여주고(Show) 어떻게 바꾸는지(Step)는 자식 클래스가 정한다.
/// 저장은 하지 않는다 (SettingsMenu 담당).
/// </summary>
public abstract class SettingRow : MonoBehaviour
{
    [SerializeField] private SettingKey key;
    [SerializeField] private TMP_Text label;
    [SerializeField] private GameObject[] selectedOnly; // 선택됐을 때만 보이는 오브젝트 (좌우 화살표 등)
    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.55f);
    [SerializeField] private Color selectedColor = Color.white;

    public SettingKey Key => key;

    /// <summary>지금 값을 바꿀 수 있는지 (잠긴 설정이면 false)</summary>
    public bool IsChangeable => CanChange && !SettingsLock.IsLocked(key);

    /// <summary>설정창에 보일지 (조건에 따라 숨기는 줄은 재정의)</summary>
    public virtual bool IsVisible => true;

    /// <summary>값이 아니라 Enter로 실행하는 줄인지</summary>
    public virtual bool IsAction => false;

    public virtual void Submit() { }

    public abstract void Show(float value);

    /// <summary>direction(-1/+1)만큼 바꾼 값을 돌려준다.</summary>
    public abstract float Step(float value, int direction);

    public virtual void SetSelected(bool selected)
    {
        if (label != null) label.color = selected ? selectedColor : normalColor;
        foreach (var go in selectedOnly)
            if (go != null) go.SetActive(selected && IsChangeable);
    }

    protected virtual bool CanChange => key != SettingKey.None;
}
