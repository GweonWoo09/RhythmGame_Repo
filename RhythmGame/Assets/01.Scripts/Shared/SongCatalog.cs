using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 곡 ID -> 곡 에셋 경로 목록. 시트의 songs 탭에서 임포터가 만든 Resources/SongCatalog.json 을 읽는다.
///
///   { "songs": [ { "id": "tutorial_01", "title": "튜토리얼 곡", "path": "Songs/tutorial_01" } ] }
///
/// id는 곡 에셋 이름(PlaySession.Song.name)과 같아야 한다. ResultSubmitter가 결과의 songId로 에셋 이름을 보내고,
/// StoryFlow가 그 값을 흐름의 곡 ID와 비교하기 때문이다. (임포터가 id == 경로의 마지막 이름인지 검사한다)
///
/// 카탈로그 파일이 없으면 비어 있는 것으로 본다. (이때 StoryFlow의 Play 칸은 예전처럼 payload로만 곡을 넘긴다)
/// </summary>
public static class SongCatalog
{
    [Serializable]
    public class Entry
    {
        public string id;
        public string title;
        public string path;   // Resources 기준 경로 (확장자 없음)
    }

    [Serializable]
    public class CatalogJson
    {
        public Entry[] songs;
    }

    public const string ResourcePath = "SongCatalog";

    private static Dictionary<string, Entry> _entries;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _entries = null;
    }

    /// <summary>카탈로그에 곡이 하나라도 있는가</summary>
    public static bool HasCatalog
    {
        get
        {
            EnsureLoaded();
            return _entries.Count > 0;
        }
    }

    public static bool TryGet(string songId, out Entry entry)
    {
        EnsureLoaded();

        entry = null;
        return !string.IsNullOrEmpty(songId) && _entries.TryGetValue(songId, out entry);
    }

    /// <summary>코드에서 직접 곡을 등록한다 (테스트용, 또는 시트 없이 쓸 때)</summary>
    public static void Register(Entry entry)
    {
        if (entry == null || string.IsNullOrEmpty(entry.id)) return;

        EnsureLoaded();
        _entries[entry.id] = entry;
    }

    /// <summary>임포터로 JSON을 다시 만든 뒤 플레이 중에 바로 반영하고 싶을 때</summary>
    public static void Reload()
    {
        _entries = null;
    }

    private static void EnsureLoaded()
    {
        if (_entries != null) return;

        // StoryFlow가 곡 ID를 대소문자 구분 없이 비교하므로 여기도 맞춘다
        _entries = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

        TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
        if (asset == null) return;

        CatalogJson json;
        try
        {
            json = JsonUtility.FromJson<CatalogJson>(asset.text);
        }
        catch (ArgumentException e)
        {
            Debug.LogError($"[SongCatalog] JSON 형식 오류 (Resources/{ResourcePath}.json): {e.Message}");
            return;
        }

        if (json == null || json.songs == null) return;

        foreach (Entry entry in json.songs)
        {
            if (entry != null && !string.IsNullOrEmpty(entry.id))
            {
                _entries[entry.id] = entry;
            }
        }
    }
}
