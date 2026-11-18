using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Loading 씬의 로딩바. SceneChanger.OnLoadProgress로 실제 로딩 진행률을 받아 채움 막대를 늘린다.
/// LoadingBar 오브젝트에 붙이고 fill(LoadingBarFill)을 연결한다. 비워 두면 자식 중 이름에 "Fill"이 들어간 Image를 찾는다.
///
/// [표시 방식]
///   - fill Image가 Filled 타입이면 fillAmount로 채운다.
///   - 그 밖(Sliced 등)이면 왼쪽 끝을 고정한 채 너비를 늘린다. 둥근 양 끝(9-slice 테두리)이 그대로 유지된다.
///     씬에 배치한 너비가 100%일 때의 너비이고, 0%일 때는 양 끝 테두리만 보이는 최소 너비다.
///
/// [부드럽게 채우기]
///   실제 로딩은 순식간에 끝나는 경우가 많으므로, 표시값은 목표값을 향해 최소 minFillDuration초에 걸쳐 따라간다.
///   100%가 된 뒤 completeHoldTime초 동안 꽉 찬 모습을 보여준 다음 IsComplete가 true가 된다.
///   LoadingStarter는 IsComplete가 될 때까지 다음 씬 활성화를 미룬다.
/// </summary>
[DisallowMultipleComponent]
public class LoadingBar : MonoBehaviour
{
    [Tooltip("채워지는 막대 이미지 (LoadingBarFill). 비우면 자식에서 이름에 Fill이 들어간 Image를 찾는다.")]
    [SerializeField] private Image fill;

    [Tooltip("진행률을 글자로 보여줄 텍스트 (선택). 예: 75%")]
    [SerializeField] private TextMeshProUGUI percentText;

    [Tooltip("0%에서 100%까지 채우는 데 걸리는 최소 시간 (초). 로딩이 빨라도 이 시간 동안은 차오르는 모습을 보여준다.")]
    [SerializeField] private float minFillDuration = 1.2f;

    [Tooltip("100%가 된 뒤 다음 화면으로 넘어가기 전에 꽉 찬 상태를 보여주는 시간 (초)")]
    [SerializeField] private float completeHoldTime = 0.25f;

    private RectTransform _fillRect;
    private bool _useFillAmount;
    private float _fullWidth;
    private float _minWidth;
    private float _leftEdge; // 부모 기준 막대 왼쪽 끝 위치 (anchoredPosition.x 계산용)

    private float _target;
    private float _shown;
    private float _fullSince = -1f;
    private bool _subscribed;

    /// <summary>화면에 보이는 진행률 (0~1)</summary>
    public float DisplayedProgress => _shown;

    /// <summary>막대가 끝까지 찼고, 꽉 찬 모습을 completeHoldTime 동안 보여줬다.</summary>
    public bool IsComplete => _fullSince >= 0f && Time.unscaledTime - _fullSince >= completeHoldTime;

    private void Awake()
    {
        if (fill == null) fill = FindFillImage();

        if (fill == null)
        {
            Debug.LogError("[LoadingBar] 채울 막대 Image(fill)가 없습니다. LoadingBarFill을 연결해 주세요.", this);
            enabled = false;
            return;
        }

        _fillRect = fill.rectTransform;
        _useFillAmount = fill.type == Image.Type.Filled;

        if (!_useFillAmount)
        {
            _fullWidth = _fillRect.rect.width;
            _minWidth = Mathf.Min(_fullWidth, GetBorderWidth(fill));

            // 왼쪽 끝 = 중심 위치 - 피벗만큼의 너비 (anchoredPosition은 피벗 기준)
            _leftEdge = _fillRect.anchoredPosition.x - _fullWidth * _fillRect.pivot.x;
        }

        Apply(0f);
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void Start()
    {
        // SceneChanger가 이 오브젝트보다 늦게 준비된 경우를 대비해 한 번 더 시도한다
        TrySubscribe();
    }

    private void OnDisable()
    {
        if (_subscribed && SceneChanger.Instance != null)
        {
            SceneChanger.Instance.OnLoadProgress -= SetProgress;
        }

        _subscribed = false;
    }

    private void Update()
    {
        if (_shown < _target)
        {
            float speed = minFillDuration > 0f ? 1f / minFillDuration : float.MaxValue;
            _shown = Mathf.MoveTowards(_shown, _target, speed * Time.unscaledDeltaTime);
            Apply(_shown);
        }

        if (_shown >= 1f && _fullSince < 0f)
        {
            _fullSince = Time.unscaledTime;
        }
    }

    /// <summary>실제 진행률(0~1)을 알려준다. 표시는 이 값을 향해 부드럽게 따라간다. 줄어드는 값은 무시한다.</summary>
    public void SetProgress(float progress)
    {
        _target = Mathf.Max(_target, Mathf.Clamp01(progress));
    }

    private void TrySubscribe()
    {
        if (_subscribed || SceneChanger.Instance == null || !enabled) return;

        SceneChanger.Instance.OnLoadProgress += SetProgress;
        _subscribed = true;
    }

    private void Apply(float progress)
    {
        if (_useFillAmount)
        {
            fill.fillAmount = progress;
        }
        else
        {
            float width = Mathf.Lerp(_minWidth, _fullWidth, progress);
            _fillRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);

            // 왼쪽 끝을 고정한 채 오른쪽으로 늘어나도록 위치를 맞춘다
            Vector2 position = _fillRect.anchoredPosition;
            position.x = _leftEdge + width * _fillRect.pivot.x;
            _fillRect.anchoredPosition = position;
        }

        if (percentText != null)
        {
            percentText.text = Mathf.RoundToInt(progress * 100f) + "%";
        }
    }

    /// <summary>9-slice 양 끝 테두리가 차지하는 너비. 이보다 좁히면 둥근 끝이 찌그러진다.</summary>
    private static float GetBorderWidth(Image image)
    {
        if (image.sprite == null || image.type != Image.Type.Sliced) return 0f;

        Vector4 border = image.sprite.border;
        float pixelsPerUnit = image.pixelsPerUnit > 0f ? image.pixelsPerUnit : 1f;
        return (border.x + border.z) / pixelsPerUnit;
    }

    private Image FindFillImage()
    {
        foreach (var image in GetComponentsInChildren<Image>(true))
        {
            if (image.gameObject != gameObject && image.name.Contains("Fill")) return image;
        }

        return null;
    }
}
