using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 메뉴 항목 하나. 선택 표시(SelectedMark 켜기/끄기)와 Enter를 눌렀을 때 할 일만 담당한다.
/// </summary>
public class MenuItem : MonoBehaviour
{
    [Tooltip("선택되었을 때만 켜지는 이미지")]
    [SerializeField] private GameObject selectedMark;

    [Tooltip("Enter를 눌렀을 때 실행할 일")]
    [SerializeField] private UnityEvent onSubmit;

    public void SetSelected(bool selected)
    {
        if (selectedMark != null) selectedMark.SetActive(selected);
    }

    public void Submit() => onSubmit.Invoke();
}
