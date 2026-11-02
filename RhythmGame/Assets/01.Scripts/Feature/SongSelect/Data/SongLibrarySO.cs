using System.Collections.Generic;
using UnityEngine;

/// <summary>곡 선택창 탭 종류</summary>
public enum SongCategory
{
    All,
    Story
}

/// <summary>
/// 곡 선택창에 나올 곡 목록만 담는 데이터.
/// </summary>
[CreateAssetMenu(fileName = "SongLibrary", menuName = "Rhythm/Song Library")]
public class SongLibrarySO : ScriptableObject
{
    [Tooltip("곡 선택창에 표시할 순서대로 넣는다")]
    [SerializeField] private List<SongDataSO> songs = new List<SongDataSO>();

    /// <summary>탭에 맞는 곡만 골라서 돌려준다 (빈 칸은 제외)</summary>
    public List<SongDataSO> GetSongs(SongCategory category)
    {
        var result = new List<SongDataSO>();
        foreach (var song in songs)
        {
            if (song == null) continue;
            if (category == SongCategory.Story && !song.isStorySong) continue;
            result.Add(song);
        }
        return result;
    }
}
