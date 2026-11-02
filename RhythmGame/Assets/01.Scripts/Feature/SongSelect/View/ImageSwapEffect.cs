using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 이미지의 그림(Sprite)이 바뀔 때마다 살짝 작은 크기에서 커지며 나타나는 연출만 담당한다.
/// 곡 선택창의 큰 커버(Song_Cover)에 붙인다. 그림을 바꾸는 쪽(SongDetailView)은 수정할 필요 없다.
/// </summary>
[RequireComponent(typeof(Image))]
public class ImageSwapEffect : MonoBehaviour
{
    [Tooltip("나타나는 데 걸리는 시간(초)")]
    [SerializeField] private float duration = 0.18f;

    [Tooltip("나타나기 시작할 때의 크기 배율")]
    [SerializeField] private float startScale = 0.95f;

    private Image image;
    private Sprite lastSprite;
    private Vector3 baseScale;
    private Color baseColor;
    private float elapsed = -1f; // 0 이상이면 연출 진행 중

    private void Awake()
    {
        image = GetComponent<Image>();
        lastSprite = image.sprite;
        baseScale = transform.localScale;
        baseColor = image.color;
    }

    private void LateUpdate()
    {
        if (image.sprite != lastSprite)
        {
            lastSprite = image.sprite;
            elapsed = 0f;
        }

        if (elapsed < 0f) return;

        elapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        float eased = 1f - (1f - t) * (1f - t); // 처음엔 빠르게, 끝에서 부드럽게 멈춤

        transform.localScale = baseScale * Mathf.Lerp(startScale, 1f, eased);
        var color = baseColor;
        color.a = baseColor.a * eased;
        image.color = color;

        if (t >= 1f) elapsed = -1f;
    }
}
