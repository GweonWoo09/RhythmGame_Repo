using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 버튼에 붙여서 클릭 시 SceneChanger를 통해 지정한 씬으로 전환한다.
/// Unity Button의 OnClick() 리스트는 커스텀 enum(SceneChanger.SceneType)을 직접
/// 드롭다운으로 넘길 수 없기 때문에, 이 컴포넌트가 그 역할을 대신한다.
///
/// 사용법: 버튼 오브젝트에 이 스크립트를 붙이고 Inspector에서 targetScene만 선택하면 끝.
/// (같은 오브젝트에 Button 컴포넌트가 있으면 자동으로 연결됨)
/// </summary>
[RequireComponent(typeof(Button))]
public class SceneTransitionButton : MonoBehaviour
{
    [Tooltip("클릭 시 이동할 씬")]
    [SerializeField] private SceneChanger.SceneType targetScene;

    [Header("챕터/곡 선택 버튼처럼 데이터를 함께 넘겨야 할 때 (선택)")]
    [SerializeField] private bool sendPayload = false;
    [SerializeField] private int chapterId;
    [SerializeField] private string songId;
    [SerializeField] private string difficulty;

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        _button.onClick.AddListener(HandleClick);
    }

    private void OnDisable()
    {
        _button.onClick.RemoveListener(HandleClick);
    }

    private void HandleClick()
    {
        if (SceneChanger.Instance == null)
        {
            Debug.LogError("[SceneTransitionButton] 씬에 SceneChanger 인스턴스가 없습니다.");
            return;
        }

        if (sendPayload)
        {
            var payload = new SceneChanger.ScenePayload
            {
                chapterId = chapterId,
                songId = songId,
                difficulty = difficulty
            };
            SceneChanger.Instance.LoadScene(targetScene, payload);
        }
        else
        {
            SceneChanger.Instance.LoadScene(targetScene);
        }
    }
}