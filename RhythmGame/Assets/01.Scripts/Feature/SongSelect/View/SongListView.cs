using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 곡 목록 칸들에 곡을 채우고, 선택한 곡이 항상 보이도록 목록을 넘기는 것만 담당한다.
/// </summary>
public class SongListView : MonoBehaviour
{
    [SerializeField] private SongSelectController controller;
    [Tooltip("위에서부터 순서대로 넣는다")]
    [SerializeField] private SongListSlot[] slots;

    private IReadOnlyList<SongDataSO> songs;
    private int firstVisible; // 맨 위 칸에 보이는 곡의 번호

    private void OnEnable()
    {
        controller.ListChanged += OnListChanged;
        controller.SongIndexChanged += OnSongIndexChanged;
    }

    private void OnDisable()
    {
        controller.ListChanged -= OnListChanged;
        controller.SongIndexChanged -= OnSongIndexChanged;
    }

    private void OnListChanged(IReadOnlyList<SongDataSO> list)
    {
        songs = list;
        firstVisible = 0;
    }

    private void OnSongIndexChanged(int index)
    {
        if (songs == null || slots == null) return;

        // 선택한 곡이 보이는 범위를 벗어나면 범위를 옮긴다
        if (index < firstVisible) firstVisible = Mathf.Max(0, index);
        else if (index >= firstVisible + slots.Length) firstVisible = index - slots.Length + 1;

        for (int i = 0; i < slots.Length; i++)
        {
            int songIndex = firstVisible + i;
            var song = songIndex < songs.Count ? songs[songIndex] : null;

            slots[i].Show(song);
            slots[i].SetSelected(songIndex == index);
        }
    }
}
