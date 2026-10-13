using System;
using System.IO;
using UnityEngine;

#region Save Data Models
// JsonUtility 규칙: Dictionary / DateTime / 에셋 참조 불가. 필드 이름이 곧 저장 키이므로
// 이름을 바꾸면 기존 세이브를 읽지 못한다. 필드 추가는 대체로 호환된다.

/// <summary>세이브 슬롯 파일 하나의 최상위 구조</summary>
[Serializable]
public class SaveData
{
    public int version = DataManager.CurrentSaveVersion;
    public string savedAt;   // ISO 8601 문자열 (DateTime은 직렬화되지 않음)
    public GameManager.GameProgressData progress = new GameManager.GameProgressData();
}

/// <summary>설정 파일 구조. 진행도와 별도 파일이라 세이브를 지워도 설정은 남는다.</summary>
[Serializable]
public class SettingsData
{
    public int version = DataManager.CurrentSettingsVersion;
    public float bgmVolume = 1f;
    public float sfxVolume = 1f;
    public string language = "ko";
}
#endregion

/// <summary>
/// 저장 데이터 입출력 전담 싱글톤. "어떻게 저장할지"(직렬화, 파일 입출력, 백업, 버전)만 알고,
/// "무엇을/언제 저장할지"는 GameManager가 결정한다. 게임 규칙(해금, 랭크 등)은 이 클래스에 넣지 않는다.
///
/// - 다른 매니저를 호출하지 않는 최하위 계층이다 (GameProgressData 타입만 참조).
/// - 설정 적용은 직접 하지 않고 OnSettingsChanged 이벤트만 발행한다. AudioManager 등이 구독해서 적용.
///
/// 저장 위치: Application.persistentDataPath
///   save_slot_{n}.json (+ .bak / .corrupt)   진행도
///   settings.json (+ .bak / .corrupt)        설정
/// </summary>
public class DataManager : MonoBehaviour
{
    #region Singleton
    public static DataManager Instance { get; private set; }

    // Awake에서는 인스턴스 등록만 한다. 데이터 로드와 다른 매니저 참조는 Start 이후에 한다.
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    #endregion

    [Header("세이브 슬롯")]
    [Tooltip("사용 가능한 슬롯 수. 슬롯 번호는 0 ~ (slotCount-1). 슬롯 1개만 쓰려면 1로 두고 항상 slot 0을 사용.")]
    [SerializeField] private int slotCount = 3;

    public int SlotCount => slotCount;

    #region Constants
    public const int CurrentSaveVersion = 1;
    public const int CurrentSettingsVersion = 1;

    private const string SettingsFileName = "settings.json";
    private const string TempExt = ".tmp";
    private const string BackupExt = ".bak";
    private const string CorruptExt = ".corrupt";
    #endregion

    #region Settings (state / event)
    private SettingsData _settings;

    /// <summary>
    /// 현재 설정. 아직 로드 전이면 이 시점에 읽어온다 (구독자가 Start 순서와 무관하게 값을 얻을 수 있다).
    /// 반환된 객체를 직접 수정하지 말고, 바꾼 뒤 SaveSettings로 저장할 것.
    /// </summary>
    public SettingsData Settings
    {
        get
        {
            if (_settings == null) _settings = ReadSettingsFromDisk();
            return _settings;
        }
    }

    /// <summary>설정이 로드되거나 저장(변경)됐을 때 발행. AudioManager 등이 구독해서 적용한다.</summary>
    public event Action<SettingsData> OnSettingsChanged;

    private void Start()
    {
        // 부팅 시 한 번 읽고 구독자에게 알린다
        LoadSettings();
    }
    #endregion

    #region Progress (Save / Load)
    /// <summary>진행도를 슬롯 파일에 저장한다. 성공 여부 반환.</summary>
    public bool Save(GameManager.GameProgressData data, int slot = 0)
    {
        if (data == null)
        {
            Debug.LogError("[DataManager] 저장할 진행도 데이터가 null입니다.");
            return false;
        }

        if (!IsValidSlot(slot)) return false;

        var save = new SaveData
        {
            version = CurrentSaveVersion,
            savedAt = DateTime.Now.ToString("s"),
            progress = data
        };

        string json = JsonUtility.ToJson(save, true);
        return WriteSafely(GetSlotPath(slot), json, IsValidSaveJson);
    }

    /// <summary>
    /// 슬롯에서 진행도를 읽는다. 본 파일이 손상되면 .bak을 시도한다.
    /// 파일이 없거나 둘 다 읽을 수 없으면 null (호출한 쪽이 새 게임으로 처리).
    /// </summary>
    public GameManager.GameProgressData Load(int slot = 0)
    {
        if (!IsValidSlot(slot)) return null;

        string path = GetSlotPath(slot);

        if (TryReadSave(path, out var data))
        {
            return data.progress;
        }

        if (File.Exists(path))
        {
            // 읽을 수 없는 본 파일은 덮어쓰이기 전에 따로 보관한다
            PreserveCorrupt(path);
        }

        if (TryReadSave(path + BackupExt, out data))
        {
            Debug.LogWarning($"[DataManager] 슬롯 {slot} 본 파일을 읽지 못해 백업(.bak)에서 복구했습니다.");
            return data.progress;
        }

        return null;
    }

    /// <summary>이어하기 버튼 활성화 판단용. 본 파일 또는 백업이 있으면 true.</summary>
    public bool HasSave(int slot = 0)
    {
        if (!IsValidSlot(slot)) return false;

        string path = GetSlotPath(slot);
        return File.Exists(path) || File.Exists(path + BackupExt);
    }

    /// <summary>슬롯의 저장 파일(본 파일, 백업, 보관본)을 모두 삭제한다.</summary>
    public bool DeleteSave(int slot = 0)
    {
        if (!IsValidSlot(slot)) return false;

        string path = GetSlotPath(slot);

        try
        {
            DeleteIfExists(path);
            DeleteIfExists(path + BackupExt);
            DeleteIfExists(path + TempExt);
            DeleteIfExists(path + CorruptExt);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[DataManager] 슬롯 {slot} 삭제 실패: {e.Message}");
            return false;
        }
    }

    private bool TryReadSave(string path, out SaveData data)
    {
        data = null;

        string json = ReadTextOrNull(path);
        if (json == null) return false;

        return TryParseSave(json, out data);
    }

    private static bool TryParseSave(string json, out SaveData data)
    {
        data = null;
        if (string.IsNullOrWhiteSpace(json)) return false;

        try
        {
            data = JsonUtility.FromJson<SaveData>(json);
        }
        catch (Exception)
        {
            return false;
        }

        if (data == null || data.progress == null) return false;

        if (data.version > CurrentSaveVersion)
        {
            // 더 새로운 빌드가 만든 세이브. 읽기는 시도하되 이 빌드가 모르는 필드는 잃을 수 있다.
            Debug.LogWarning($"[DataManager] 세이브 버전({data.version})이 현재 빌드({CurrentSaveVersion})보다 높습니다.");
        }

        MigrateSave(data);
        return true;
    }

    /// <summary>
    /// 낡은 세이브를 현재 구조로 변환하는 자리. 필드 이름 변경/삭제처럼 호환이 깨지는 변경을 할 때
    /// version을 올리고 여기에 단계별 변환을 추가한다.
    /// </summary>
    private static void MigrateSave(SaveData data)
    {
        // 예) if (data.version < 2) { /* v1 → v2 변환 */ data.version = 2; }
        if (data.progress.chapters == null) data.progress.chapters = new System.Collections.Generic.List<GameManager.ChapterProgress>();
        if (data.progress.songRecords == null) data.progress.songRecords = new System.Collections.Generic.List<GameManager.SongRecord>();
        if (data.progress.storyFlags == null) data.progress.storyFlags = new System.Collections.Generic.List<GameManager.StoryFlagEntry>();
    }

    private static bool IsValidSaveJson(string json) => TryParseSave(json, out _);
    #endregion

    #region Settings (Save / Load)
    /// <summary>설정 파일을 다시 읽고 OnSettingsChanged를 발행한다. 파일이 없거나 손상되면 기본값.</summary>
    public SettingsData LoadSettings()
    {
        _settings = ReadSettingsFromDisk();
        OnSettingsChanged?.Invoke(_settings);
        return _settings;
    }

    /// <summary>
    /// 설정을 저장하고 OnSettingsChanged를 발행한다.
    /// 파일 쓰기에 실패해도 메모리의 설정은 바뀌어 이벤트는 발행되며, 성공 여부만 반환한다.
    /// </summary>
    public bool SaveSettings(SettingsData data)
    {
        if (data == null)
        {
            Debug.LogError("[DataManager] 저장할 설정 데이터가 null입니다.");
            return false;
        }

        Sanitize(data);
        _settings = data;

        bool ok = WriteSafely(GetSettingsPath(), JsonUtility.ToJson(data, true), IsValidSettingsJson);

        OnSettingsChanged?.Invoke(_settings);
        return ok;
    }

    private SettingsData ReadSettingsFromDisk()
    {
        string path = GetSettingsPath();

        if (TryReadSettings(path, out var data)) return data;

        if (File.Exists(path))
        {
            PreserveCorrupt(path);
        }

        if (TryReadSettings(path + BackupExt, out data))
        {
            Debug.LogWarning("[DataManager] 설정 파일을 읽지 못해 백업(.bak)에서 복구했습니다.");
            return data;
        }

        return new SettingsData();
    }

    private bool TryReadSettings(string path, out SettingsData data)
    {
        data = null;

        string json = ReadTextOrNull(path);
        if (json == null) return false;

        return TryParseSettings(json, out data);
    }

    private static bool TryParseSettings(string json, out SettingsData data)
    {
        data = null;
        if (string.IsNullOrWhiteSpace(json)) return false;

        try
        {
            data = JsonUtility.FromJson<SettingsData>(json);
        }
        catch (Exception)
        {
            return false;
        }

        if (data == null) return false;

        Sanitize(data);
        return true;
    }

    private static bool IsValidSettingsJson(string json) => TryParseSettings(json, out _);

    /// <summary>범위를 벗어난 값이나 빈 값을 안전한 값으로 고친다.</summary>
    private static void Sanitize(SettingsData data)
    {
        data.bgmVolume = Mathf.Clamp01(data.bgmVolume);
        data.sfxVolume = Mathf.Clamp01(data.sfxVolume);

        if (string.IsNullOrEmpty(data.language)) data.language = "ko";
    }
    #endregion

    #region File Helpers
    private string GetSlotPath(int slot) => Path.Combine(Application.persistentDataPath, $"save_slot_{slot}.json");

    private string GetSettingsPath() => Path.Combine(Application.persistentDataPath, SettingsFileName);

    private bool IsValidSlot(int slot)
    {
        if (slot >= 0 && slot < slotCount) return true;

        Debug.LogError($"[DataManager] 잘못된 슬롯 번호입니다: {slot} (사용 가능: 0 ~ {slotCount - 1})");
        return false;
    }

    /// <summary>
    /// 쓰기 도중 종료되어도 기존 파일이 깨지지 않도록:
    /// 1) .tmp에 먼저 쓰고  2) 현재 본 파일이 정상일 때만 .bak으로 한 세대 보관  3) 본 파일 교체  4) .tmp 삭제.
    /// 현재 본 파일이 손상된 상태라면 .bak을 덮어쓰지 않아 마지막 정상본을 지킨다.
    /// </summary>
    private bool WriteSafely(string path, string json, Func<string, bool> isValidJson)
    {
        string tmpPath = path + TempExt;
        string bakPath = path + BackupExt;

        try
        {
            File.WriteAllText(tmpPath, json);

            if (File.Exists(path))
            {
                string existing = ReadTextOrNull(path);
                if (existing != null && isValidJson(existing))
                {
                    File.Copy(path, bakPath, true);
                }
            }

            File.Copy(tmpPath, path, true);
            File.Delete(tmpPath);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[DataManager] 저장 실패 ({Path.GetFileName(path)}): {e.Message}");
            return false;
        }
    }

    /// <summary>파일이 없거나 읽기 실패 시 null</summary>
    private static string ReadTextOrNull(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception e)
        {
            Debug.LogError($"[DataManager] 읽기 실패 ({Path.GetFileName(path)}): {e.Message}");
            return null;
        }
    }

    /// <summary>읽을 수 없는 파일을 .corrupt로 복사해 남겨둔다 (복구 여지). 이전 보관본은 덮어쓴다.</summary>
    private static void PreserveCorrupt(string path)
    {
        try
        {
            File.Copy(path, path + CorruptExt, true);
            Debug.LogWarning($"[DataManager] 손상된 파일을 보관했습니다: {Path.GetFileName(path)}{CorruptExt}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[DataManager] 손상 파일 보관 실패: {e.Message}");
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }
    #endregion
}