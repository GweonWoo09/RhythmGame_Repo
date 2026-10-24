using System.Collections;
using TMPro;
using UnityEngine;

// 판정 글자(Excellent/Good/Bad/Miss)를 잠깐 보여주고 숨긴다.
public class JudgmentView : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private float showSeconds = 0.5f;

    private Coroutine hideRoutine;

    private void Awake()
    {
        label.enabled = false;
    }

    public void Show(string judgment)
    {
        if (string.IsNullOrEmpty(judgment)) return;

        label.text = judgment;
        label.enabled = true;

        if (hideRoutine != null) StopCoroutine(hideRoutine);
        hideRoutine = StartCoroutine(HideAfterDelay());
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(showSeconds);
        label.enabled = false;
        hideRoutine = null;
    }
}
