using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 전체를 덮는 페이드(검은 화면 등) 인/아웃만 전담하는 범용 컴포넌트.
/// SceneChanger의 씬 전환 연출뿐 아니라, 스토리 씬에서 배경 이미지를 바꿀 때도
/// (페이드아웃 -> 배경 교체 -> 페이드인) 형태로 재사용할 수 있다.
/// </summary>
public class ScreenFader : MonoBehaviour
{
    #region Singleton
    public static ScreenFader Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (fadeCanvasGroup != null)
        {
            var panelTransform = fadeCanvasGroup.transform as RectTransform;
            if (panelTransform != null)
            {
                // Fade 패널을 SceneFader 오브젝트 안에 넣도록하기 (SceneFader의 자식으로 옮기기)
                panelTransform.SetParent(transform, false);

                // 패널을 캔버스 화면에 맞추도록 초기화
                panelTransform.anchorMin = Vector2.zero;
                panelTransform.anchorMax = Vector2.one;
                panelTransform.anchoredPosition = Vector2.zero;
                panelTransform.sizeDelta = Vector2.zero;

                var canvas = fadeCanvasGroup.GetComponent<Canvas>();
                if (canvas == null) canvas = fadeCanvasGroup.gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.overrideSorting = true;
                canvas.sortingOrder = short.MaxValue;

                if (fadeCanvasGroup.GetComponent<GraphicRaycaster>() == null)
                    fadeCanvasGroup.gameObject.AddComponent<GraphicRaycaster>();
            }

            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
            fadeCanvasGroup.gameObject.SetActive(false);
        }
    }
    #endregion

    [Tooltip("화면 전체를 덮는 검은 패널 등에 붙은 CanvasGroup")]
    [SerializeField] private CanvasGroup fadeCanvasGroup;

    [SerializeField] private float defaultFadeDuration = 0.4f;

    /// <summary>
    /// 화면을 서서히 가린다 (alpha 0 -> 1). 다른 코루틴에서 yield return으로 직접 사용 가능.
    /// </summary>
    public IEnumerator FadeOut(float duration = -1f)
    {
        yield return Fade(0f, 1f, duration);
    }

    /// <summary>
    /// 가려진 화면을 서서히 걷어낸다 (alpha 1 -> 0).
    /// </summary>
    public IEnumerator FadeIn(float duration = -1f)
    {
        yield return Fade(1f, 0f, duration);
    }

    /// <summary>
    /// 페이드아웃 -> 중간 콜백(배경 교체, 씬 전환 등) -> 페이드인을 한 번에 처리하는 편의 메서드.
    /// 호출하는 쪽에서 코루틴을 직접 작성할 필요 없이 바로 쓸 수 있다.
    /// 예: 스토리 씬에서 배경을 바꿀 때
    ///   ScreenFader.Instance.FadeOutIn(0.3f, () => backgroundImage.sprite = newBackground);
    /// </summary>
    public void FadeOutIn(float duration, Action onMidpoint, Action onComplete = null)
    {
        StartCoroutine(FadeOutInRoutine(duration, onMidpoint, onComplete));
    }

    private IEnumerator FadeOutInRoutine(float duration, Action onMidpoint, Action onComplete)
    {
        yield return FadeOut(duration);
        onMidpoint?.Invoke();
        yield return FadeIn(duration);
        onComplete?.Invoke();
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (fadeCanvasGroup == null) yield break;
        if (duration < 0f) duration = defaultFadeDuration;

        fadeCanvasGroup.gameObject.SetActive(true);
        fadeCanvasGroup.blocksRaycasts = true;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeCanvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }

        fadeCanvasGroup.alpha = to;
        fadeCanvasGroup.blocksRaycasts = (to > 0f);
        if (to <= 0f)
        {
            fadeCanvasGroup.gameObject.SetActive(false);
        }
    }
}
