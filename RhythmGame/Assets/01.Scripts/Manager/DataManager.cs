using System;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// 파일 입출력 전담 클래스. "무엇을 / 언제 저장할지"는 GameManager가 정하고,
/// 이 클래스는 "어떻게 안전하게 읽고 쓸지"만 책임진다.
///
/// - MonoBehaviour가 아닌 static 클래스다. (싱글톤은 전역에서 꼭 필요한 4개만 둔다는 원칙 유지)
/// - 저장 파일 2개: save.json(진행도) / settings.json(볼륨, 언어 등 기기 설정)
///   설정을 진행도와 분리해 두면 "새 게임"을 눌러도 볼륨 설정이 초기화되지 않는다.
/// - 쓰기 도중 앱이 꺼져도 파일이 깨지지 않도록 임시 파일에 먼저 쓰고 교체하며, 직전 저장본을 .bak으로 남긴다.
/// - 저장 위치: Application.persistentDataPath
/// </summary>
public static class DataManager
{
    #region Constants
    /// <summary>저장 포맷이 바뀔 때마다 올리고, Migrate()에 변환 규칙을 추가한다.</summary>
    private const int CurrentSaveVersion = 1;
    private const int CurrentSettingsVersion = 1;

    private const string SaveFileName = "save.json";
    private const string SettingsFileName = "settings.json";
    private const string BackupExt = ".bak";
    private const string TempExt = ".tmp";

    private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);
    #endregion

    #region Data Models
    /// <summary>JsonUtility는 최상위에 버전 같은 메타 정보를 함께 두려면 래퍼 클래스가 필요하다.</summary>
    [Serializable]
    private class SaveFile
    {
        public int version;
        public string savedAt;
        public GameManager.GameProgressData progress;
    }

    /// <summary>진행도와 무관한 기기/사용자 설정.</summary>
    [Serializable]
    public class SettingsData
    {
        public int version = CurrentSettingsVersion;
        [Range(0f, 1f)] public float bgmVolume = 1f;
        [Range(0f, 1f)] public float sfxVolume = 1f;
        public string language = "ko";
    }
    #endregion

    #region Paths
    private static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);
    private static string SettingsPath => Path.Combine(Application.persistentDataPath, SettingsFileName);
    #endregion

    #region Domain Reload 대응
    // 에디터에서 "Enter Play Mode Options → Reload Domain 끄기"를 쓰면 static 값이 남아 있으므로 초기화해 준다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _settings = null;
    }
    #endregion

    // ------------------------------------------------------------------
    // 진행도 (save.json)
    // ------------------------------------------------------------------
    #region Progress Save / Load
    /// <summary>저장 파일(또는 백업)이 존재하는지. 타이틀의 "이어하기" 버튼 활성화 판단에 사용.</summary>
    public static bool HasSave()
    {
        return File.Exists(SavePath) || File.Exists(SavePath + BackupExt);
    }

    /// <summary>
    /// 진행도를 불러온다. 저장 파일이 없거나 본파일·백업 모두 읽을 수 없으면 null.
    /// (null이면 호출한 쪽에서 새 데이터를 만들어 쓴다)
    /// </summary>
    public static GameManager.GameProgressData LoadProgress()
    {
        if (TryReadSaveFile(SavePath, out var progress))
            return progress;

        // 본파일이 없거나 손상된 경우 직전 저장본으로 복구 시도
        if (File.Exists(SavePath))
            Debug.LogWarning("[DataManager] 저장 파일을 읽지 못했습니다. 백업 파일로 복구를 시도합니다.");

        if (TryReadSaveFile(SavePath + BackupExt, out progress))
        {
            Debug.LogWarning("[DataManager] 백업 파일에서 진행도를 복구했습니다.");
            return progress;
        }

        return null;
    }

    /// <summary>진행도를 저장한다. 성공 여부를 반환.</summary>
    public static bool SaveProgress(GameManager.GameProgressData progress)
    {
        if (progress == null)
        {
            Debug.LogError("[DataManager] 저장할 진행도 데이터가 null입니다.");
            return false;
        }

        var file = new SaveFile
        {
            version = CurrentSaveVersion,
            savedAt = DateTime.UtcNow.ToString("o"),
            progress = progress
        };

        return TryWriteAtomic(SavePath, JsonUtility.ToJson(file, true));
    }

    /// <summary>"새 게임" 시작 시 호출. 본파일·백업·임시 파일을 모두 지운다. (설정 파일은 유지)</summary>
    public static void DeleteSave()
    {
        TryDelete(SavePath);
        TryDelete(SavePath + BackupExt);
        TryDelete(SavePath + TempExt);
    }

    private static bool TryReadSaveFile(string path, out GameManager.GameProgressData progress)
    {
        progress = null;

        if (!TryReadJson(path, out SaveFile file) || file == null || file.progress == null)
            return false;

        if (file.version > CurrentSaveVersion)
        {
            // 더 새로운 버전의 게임에서 만든 파일. 읽을 수 있는 만큼만 읽는다.
            Debug.LogWarning($"[DataManager] 저장 파일 버전({file.version})이 현재 버전({CurrentSaveVersion})보다 높습니다.");
        }
        else
        {
            Migrate(file);
        }

        Sanitize(file.progress);
        progress = file.progress;
        return true;
    }

    /// <summary>
    /// 구버전 저장 파일을 현재 포맷으로 변환한다.
    /// 필드를 추가/변경할 때 CurrentSaveVersion을 올리고 아래에 단계를 추가하면 된다.
    /// </summary>
    private static void Migrate(SaveFile file)
    {
        // 예시:
        // if (file.version < 2) { /* v1 → v2 변환 */ file.version = 2; }
        file.version = CurrentSaveVersion;
    }

    /// <summary>손으로 편집했거나 필드 추가 이전에 만든 파일에서 null이 생기지 않도록 보정.</summary>
    private static void Sanitize(GameManager.GameProgressData progress)
    {
        if (progress.chapters == null)
            progress.chapters = new System.Collections.Generic.List<GameManager.ChapterProgress>();
        if (progress.songRecords == null)
            progress.songRecords = new System.Collections.Generic.List<GameManager.SongRecord>();
        if (progress.storyFlags == null)
            progress.storyFlags = new System.Collections.Generic.List<GameManager.StoryFlagEntry>();
    }
    #endregion

    // ------------------------------------------------------------------
    // 설정 (settings.json)
    // ------------------------------------------------------------------
    #region Settings
    private static SettingsData _settings;

    /// <summary>
    /// 현재 설정. 처음 접근할 때 파일에서 자동으로 불러온다.
    /// 값을 바꾼 뒤 SaveSettings()를 호출해야 파일에 반영된다.
    /// (슬라이더를 드래그하는 동안 매 프레임 저장하지 않도록, 저장 시점은 호출자가 정한다)
    /// </summary>
    public static SettingsData Settings
    {
        get
        {
            if (_settings == null) _settings = LoadSettings();
            return _settings;
        }
    }

    public static bool SaveSettings()
    {
        if (_settings == null) return true; // 한 번도 읽거나 바꾼 적이 없으면 저장할 것이 없다.

        _settings.version = CurrentSettingsVersion;
        return TryWriteAtomic(SettingsPath, JsonUtility.ToJson(_settings, true));
    }

    private static SettingsData LoadSettings()
    {
        if (TryReadJson(SettingsPath, out SettingsData loaded) && loaded != null)
        {
            loaded.bgmVolume = Mathf.Clamp01(loaded.bgmVolume);
            loaded.sfxVolume = Mathf.Clamp01(loaded.sfxVolume);
            if (string.IsNullOrEmpty(loaded.language)) loaded.language = "ko";
            return loaded;
        }

        return new SettingsData();
    }
    #endregion

    // ------------------------------------------------------------------
    // 파일 입출력 공통
    // ------------------------------------------------------------------
    #region File IO
    private static bool TryReadJson<T>(string path, out T result) where T : class
    {
        result = null;

        try
        {
            if (!File.Exists(path)) return false;

            string json = File.ReadAllText(path, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json)) return false;

            result = JsonUtility.FromJson<T>(json);
            return result != null;
        }
        catch (Exception e)
        {
            Debug.LogError($"[DataManager] 파일 읽기 실패: {path}\n{e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 임시 파일에 먼저 쓴 뒤 본파일과 교체한다.
    ///   1) path.tmp 에 전체 기록
    ///   2) 기존 본파일을 path.bak 으로 복사
    ///   3) 본파일 삭제 후 tmp를 본파일로 이동
    /// 어느 단계에서 꺼져도 "본파일 또는 백업" 중 하나는 온전하다.
    /// </summary>
    private static bool TryWriteAtomic(string path, string json)
    {
        string tmpPath = path + TempExt;
        string bakPath = path + BackupExt;

        try
        {
            File.WriteAllText(tmpPath, json, Utf8NoBom);

            if (File.Exists(path))
            {
                File.Copy(path, bakPath, true);
                File.Delete(path);
            }

            File.Move(tmpPath, path);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[DataManager] 파일 저장 실패: {path}\n{e.Message}");
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception e)
        {
            Debug.LogError($"[DataManager] 파일 삭제 실패: {path}\n{e.Message}");
        }
    }
    #endregion

#if UNITY_EDITOR
    #region Editor Utilities
    [UnityEditor.MenuItem("Tools/Save Data/Open Save Folder")]
    private static void OpenSaveFolder()
    {
        UnityEditor.EditorUtility.RevealInFinder(Application.persistentDataPath);
    }

    [UnityEditor.MenuItem("Tools/Save Data/Delete Save File")]
    private static void DeleteSaveFromMenu()
    {
        if (UnityEditor.EditorUtility.DisplayDialog("저장 데이터 삭제",
                "진행도 저장 파일(save.json, 백업 포함)을 삭제할까요?\n설정 파일은 유지됩니다.", "삭제", "취소"))
        {
            DeleteSave();
            Debug.Log("[DataManager] 저장 파일을 삭제했습니다.");
        }
    }
    #endregion
#endif
}