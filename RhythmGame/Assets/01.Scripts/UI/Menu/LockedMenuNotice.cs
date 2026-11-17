using System.Collections;
using UnityEngine;

/// <summary>
/// 잠긴 메뉴 항목을 골랐을 때 안내 문구(LockedTxt)를 잠깐 보여 준다.
/// Show() : 문구를 켜고 holdTime 동안 그대로 두었다가 fadeTime 동안 서서히 투명해진 뒤 끈다.
/// 사라지는 도중 다시 누르면 처음부터 다시 보여 준다.
/// </summary>
public class LockedMenuNotice : MonoBehaviour
{
    [Tooltip("보여 줄 문구")]
    [SerializeField] private GameObject lockedText;
    [Tooltip("켜진 뒤 사라지기 시작할 때까지 기다리는 시간(초)")]
    [SerializeField] private float holdTime = 0.5f;
    [Tooltip("서서히 투명해지는 시간(초)")]
    [SerializeField] private float fadeTime = 0.5f;

    private CanvasGroup group;
    private Coroutine routine;

    private void Awake()
    {

        group = lockedText.GetComponent<CanvasGroup>();
        if (group == null) group = lockedText.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;

        lockedText.SetActive(false);
    }

    public void Show()
    {
        if (lockedText == null) return;

        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(ShowRoutine());
    }

    private IEnumerator ShowRoutine()
    {
        group.alpha = 1f;
        lockedText.SetActive(true);

        yield return new WaitForSecondsRealtime(holdTime);

        for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
        {
            group.alpha = 1f - t / fadeTime;
            yield return null;
        }

        group.alpha = 0f;
        lockedText.SetActive(false);
        routine = null;
    }
}
