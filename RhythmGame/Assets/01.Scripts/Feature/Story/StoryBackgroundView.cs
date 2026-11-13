using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스토리 씬의 배경 이미지를 DialogueManager.OnBackgroundChanged에 맞춰 바꾼다.
/// 배경 Image(예: Story 씬의 BG 오브젝트)에 붙이면 된다. 대사 데이터에서 배경을 지정하는 방법:
///   - 시트 meta 탭 key = bg      : 시퀀스가 시작될 때의 배경 (JSON "bg")
///   - 시트 lines 탭 bg 칸         : 그 줄부터 배경 교체 (JSON "bgOverride"). none이면 배경을 비운다(emptyColor).
///   값은 Resources 기준 스프라이트 경로다. 예: Backgrounds/studio_night  (= Assets/Resources/Backgrounds/studio_night.png)
///
/// 전환 방식: 원래 Image 바로 위에 같은 크기의 덮개 Image를 런타임에 하나 만들어,
/// 덮개에 이전 배경을 그려 둔 채 원래 Image를 새 배경으로 바꾸고 덮개를 서서히 투명하게 만든다 (크로스페이드).
/// 저장 위치/연주에서 돌아와 복원할 때와 이 씬의 첫 배경은 페이드 없이 바로 바꾼다.
/// </summary>
[DisallowMultipleComponent]
public class StoryBackgroundView : MonoBehaviour
{
    [Tooltip("배경 변경을 알려줄 DialogueManager. 비우면 씬에서 찾는다.")]
    [SerializeField] private DialogueManager dialogueManager;

    [Tooltip("배경을 그릴 Image. 비우면 이 오브젝트의 Image를 쓴다.")]
    [SerializeField] private Image backgroundImage;

    [Tooltip("배경이 바뀔 때 크로스페이드 시간 (초). 0이면 바로 바뀐다.")]
    [SerializeField] private float fadeDuration = 0.6f;

    [Tooltip("bg 칸에 none을 적었을 때 배경 자리에 보여줄 색 (알파 0이면 완전히 투명)")]
    [SerializeField] private Color emptyColor = Color.black;

    private Image _overlay;
    private Color _spriteColor = Color.white; // 스프라이트를 그릴 때의 Image 색 (씬에 설정된 원래 색)
    private Coroutine _fadeRoutine;
    private bool _hasShown;

    private void Awake()
    {
        if (backgroundImage == null) backgroundImage = GetComponent<Image>();
        if (dialogueManager == null) dialogueManager = FindFirstObjectByType<DialogueManager>();

        if (backgroundImage == null)
        {
            Debug.LogError("[StoryBackgroundView] 배경을 그릴 Image가 없습니다. Image가 있는 오브젝트에 붙이거나 backgroundImage를 연결하세요.", this);
            enabled = false;
            return;
        }

        _spriteColor = backgroundImage.color;
    }

    private void OnEnable()
    {
        if (dialogueManager == null) return;

        dialogueManager.OnBackgroundChanged += HandleBackgroundChanged;

        // DialogueManager가 이미 배경을 정한 뒤에 켜졌다면 지금 상태를 맞춘다
        if (dialogueManager.HasBackground)
        {
            Apply(dialogueManager.CurrentBackground, true);
        }
    }

    private void OnDisable()
    {
        if (dialogueManager == null) return;

        dialogueManager.OnBackgroundChanged -= HandleBackgroundChanged;
    }

    private void HandleBackgroundChanged(Sprite sprite, bool instant)
    {
        // 씬에 들어와 처음 정하는 배경은 화면 전환(ScreenFader)이 이미 가려 주므로 바로 바꾼다
        Apply(sprite, instant || !_hasShown || fadeDuration <= 0f);
    }

    /// <summary>배경을 바꾼다. sprite가 null이면 emptyColor로 비운다.</summary>
    public void Apply(Sprite sprite, bool instant)
    {
        if (backgroundImage == null) return;

        _hasShown = true;

        // 이미 같은 배경이면 아무것도 하지 않는다
        if (backgroundImage.sprite == sprite && (sprite != null || backgroundImage.color == emptyColor)) return;

        StopFade();

        if (instant)
        {
            SetImage(backgroundImage, sprite);
            return;
        }

        // 덮개에 지금 배경을 그려 두고, 원래 Image를 새 배경으로 바꾼 뒤 덮개를 걷어낸다
        Image overlay = GetOverlay();
        overlay.sprite = backgroundImage.sprite;
        overlay.color = backgroundImage.color;
        overlay.gameObject.SetActive(true);

        SetImage(backgroundImage, sprite);

        _fadeRoutine = StartCoroutine(FadeOutOverlay(overlay));
    }

    private void SetImage(Image image, Sprite sprite)
    {
        image.sprite = sprite;
        image.color = sprite != null ? _spriteColor : emptyColor;
    }

    private IEnumerator FadeOutOverlay(Image overlay)
    {
        Color start = overlay.color;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            Color c = start;
            c.a = Mathf.Lerp(start.a, 0f, elapsed / fadeDuration);
            overlay.color = c;
            yield return null;
        }

        overlay.gameObject.SetActive(false);
        _fadeRoutine = null;
    }

    private void StopFade()
    {
        if (_fadeRoutine != null)
        {
            StopCoroutine(_fadeRoutine);
            _fadeRoutine = null;
        }

        if (_overlay != null) _overlay.gameObject.SetActive(false);
    }

    /// <summary>원래 배경 Image 바로 위(형제 순서 다음)에 같은 크기의 덮개 Image를 만든다.</summary>
    private Image GetOverlay()
    {
        if (_overlay != null) return _overlay;

        RectTransform source = backgroundImage.rectTransform;

        var go = new GameObject(backgroundImage.name + "_Fade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = backgroundImage.gameObject.layer;

        var rect = (RectTransform)go.transform;
        rect.SetParent(source.parent, false);
        rect.anchorMin = source.anchorMin;
        rect.anchorMax = source.anchorMax;
        rect.pivot = source.pivot;
        rect.anchoredPosition3D = source.anchoredPosition3D;
        rect.sizeDelta = source.sizeDelta;
        rect.localRotation = source.localRotation;
        rect.localScale = source.localScale;
        rect.SetSiblingIndex(source.GetSiblingIndex() + 1);

        _overlay = go.GetComponent<Image>();
        _overlay.raycastTarget = false;
        _overlay.type = backgroundImage.type;
        _overlay.preserveAspect = backgroundImage.preserveAspect;
        _overlay.material = backgroundImage.material;
        go.SetActive(false);

        return _overlay;
    }
}
