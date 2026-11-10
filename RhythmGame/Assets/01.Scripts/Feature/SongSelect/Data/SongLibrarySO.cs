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
            if (category == SongCategory.Story && !IsInStoryTab(song)) continue;
            result.Add(song);
        }
        return result;
    }

    /// <summary>play:곡ID와 같은 곡을 찾는다 (없으면 null)</summary>
    public SongDataSO FindById(string id)
    {
        foreach (var song in songs)
        {
            if (song != null && song.MatchesId(id)) return song;
        }
        return null;
    }

    /// <summary>Story 탭에 보이는 곡: 처음부터 스토리 곡으로 지정했거나(isStorySong), 스토리를 진행하며 추가된 곡</summary>
    private static bool IsInStoryTab(SongDataSO song)
    {
        if (song.isStorySong) return true;
        if (GameManager.Instance == null) return false;

        foreach (var id in GameManager.Instance.UnlockedStorySongs)
        {
            if (song.MatchesId(id)) return true;
        }
        return false;
    }
}
