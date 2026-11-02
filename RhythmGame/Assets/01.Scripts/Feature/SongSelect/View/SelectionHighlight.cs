using UnityEngine;

/// <summary>
/// 선택 표시 이미지(노란 선택 막대 등)가 선택된 곡 칸을 따라 위아래로 움직이게 하는 것만 담당한다.
/// 움직일 오브젝트에 붙인다. 자식(예: Music_Selection_Fade)도 같이 움직인다.
/// </summary>
public class SelectionHighlight : MonoBehaviour
{
    [SerializeField] private SongListView listView;

    [Tooltip("씬에 배치된 상태에서 이 이미지가 겹쳐 있는 칸 번호 (맨 위 칸 = 0)")]
    [SerializeField] private int placedSlotIndex = 2;

    [Tooltip("따라가는 빠르기. 0이면 바로 이동")]
    [SerializeField] private float followSpeed = 20f;

    private float offsetY;      // 칸 위치와 이 이미지 위치의 차이
    private bool hasOffset;
    private float goalY;
    private bool hasGoal;

    private void OnEnable() => listView.SelectedSlotChanged += OnSelectedSlotChanged;
    private void OnDisable() => listView.SelectedSlotChanged -= OnSelectedSlotChanged;

    private void OnSelectedSlotChanged(SongListSlot slot)
    {
        if (slot == null) return;

        // 처음 한 번, 움직이기 전 위치로 칸과의 차이를 기억한다
        if (!hasOffset)
        {
            var placedSlot = listView.GetSlot(placedSlotIndex);
            offsetY = placedSlot != null ? transform.position.y - placedSlot.transform.position.y : 0f;
            hasOffset = true;
        }

        goalY = slot.transform.position.y + offsetY;

        // 처음 표시할 때는 미끄러지지 않고 바로 그 자리에 둔다
        if (!hasGoal || followSpeed <= 0f) SetY(goalY);
        hasGoal = true;
    }

    private void Update()
    {
        if (!hasGoal || followSpeed <= 0f) return;

        float t = 1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime);
        SetY(Mathf.Lerp(transform.position.y, goalY, t));
    }

    private void SetY(float y)
    {
        var position = transform.position;
        position.y = y;
        transform.position = position;
    }
}
