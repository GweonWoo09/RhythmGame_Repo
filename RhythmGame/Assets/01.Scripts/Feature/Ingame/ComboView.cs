using TMPro;
using UnityEngine;

// 콤보 숫자를 표시한다. 1콤보부터 보이고, 끊기면 숨긴다.
public class ComboView : MonoBehaviour
{
    [SerializeField] private TMP_Text countLabel;

    private void Awake()
    {
        SetVisible(false);
    }

    public void SetCount(string count)
    {
        countLabel.text = count;
    }

    public void SetVisible(bool visible)
    {
        countLabel.enabled = visible;
    }
}
