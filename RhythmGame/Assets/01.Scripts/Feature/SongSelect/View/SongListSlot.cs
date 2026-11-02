using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 곡 목록의 한 칸. 곡 정보 표시와 선택 연출(커버 강조, 칸 밀기)만 담당한다.
/// </summary>
public class SongListSlot : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text composerText;
    [Tooltip("곡 표지를 넣을 이미지 (없으면 비워둠)")]
    [SerializeField] private Image coverImage;
    [Tooltip("선택되었을 때만 켜지는 오브젝트 (없으면 비워둠)")]
    [SerializeField] private GameObject selectedMark;

    [Header("글자 색")]
    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.5f);
    [SerializeField] private Color selectedColor = Color.white;

    [Header("선택 연출")]
    [Tooltip("선택된 칸의 커버 크기 배율")]
    [SerializeField] private float selectedCoverScale = 1.08f;
    [Tooltip("선택되지 않은 칸의 커버 색 (어둡게)")]
    [SerializeField] private Color dimmedCoverColor = new Color(0.55f, 0.55f, 0.55f, 1f);
    [Tooltip("선택된 칸을 오른쪽으로 미는 거리 (부모 기준 단위)")]
    [SerializeField] private float selectedShiftX = 14f;
    [Tooltip("연출 빠르기. 클수록 빠름")]
    [SerializeField] private float animationSpeed = 18f;

    private RectTransform rectTransform;
    private Vector2 basePosition;
    private Vector3 baseCoverScale = Vector3.one;
    private float current;   // 0 = 선택 안 됨, 1 = 선택됨 (연출 진행도)
    private float target;
    private bool initialized;

    private void Awake()
    {
        rectTransform = (RectTransform)transform;
        basePosition = rectTransform.anchoredPosition;
        if (coverImage) baseCoverScale = coverImage.rectTransform.localScale;
    }

    /// <summary>곡을 표시한다. null이면 칸을 숨긴다</summary>
    public void Show(SongDataSO song)
    {
        gameObject.SetActive(song != null);
        if (song == null) return;

        if (titleText) titleText.text = song.title;
        if (composerText) composerText.text = song.composer;
        if (coverImage)
        {
            coverImage.sprite = song.cover;
            coverImage.enabled = song.cover != null;
        }
    }

    public void SetSelected(bool selected)
    {
        if (selectedMark) selectedMark.SetActive(selected);

        target = selected ? 1f : 0f;

        // 처음 표시할 때는 연출 없이 바로 적용
        if (!initialized)
        {
            current = target;
            initialized = true;
        }
        Apply();
    }

    private void Update()
    {
        if (Mathf.Approximately(current, target)) return;

        float t = 1f - Mathf.Exp(-animationSpeed * Time.unscaledDeltaTime);
        current = Mathf.Abs(target - current) < 0.001f ? target : Mathf.Lerp(current, target, t);
        Apply();
    }

    private void Apply()
    {
        if (rectTransform == null) return;

        rectTransform.anchoredPosition = basePosition + new Vector2(selectedShiftX * current, 0f);

        var textColor = Color.Lerp(normalColor, selectedColor, current);
        if (titleText) titleText.color = textColor;
        if (composerText) composerText.color = textColor;

        if (coverImage)
        {
            coverImage.rectTransform.localScale = baseCoverScale * Mathf.Lerp(1f, selectedCoverScale, current);
            coverImage.color = Color.Lerp(dimmedCoverColor, Color.white, current);
        }
    }
}
