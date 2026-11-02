using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 곡 목록의 한 칸. 곡 정보 표시와 선택 표시만 담당한다.
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

        var color = selected ? selectedColor : normalColor;
        if (titleText) titleText.color = color;
        if (composerText) composerText.color = color;
    }
}
