using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 구글 시트의 대사 데이터를 JSON으로 동기화하는 에디터 도구. (Assets/Editor/ 폴더에 둘 것)
///
/// 메뉴 (Tools > Dialogue)
///   Sync from Google Sheet           시트를 내려받아 검증하고, 오류가 없을 때만 JSON을 갱신한다.
///   Validate Google Sheet (no write) 검증만 한다 (파일을 건드리지 않음).
///   Import from Local CSV Folder...  lines.csv / meta.csv 가 든 폴더에서 같은 처리를 한다 (오프라인, 비공개 시트용).
///   Select Sheet Settings            설정 에셋을 연다 (없으면 만든다).
///
/// 흐름:  CSV 다운로드 -> DialogueSheetConverter(파싱, 검증, JSON 생성) -> 오류 없으면 파일 쓰기 -> AssetDatabase.Refresh
/// 오류가 하나라도 있으면 어떤 파일도 쓰지 않으므로, 반쯤 갱신된 상태가 생기지 않는다.
/// 내용이 바뀌지 않은 파일은 다시 쓰지 않는다 (git/임포트 불필요한 변경 방지).
///
/// 주의: 플레이 중에 동기화하면 이미 읽힌 JSON은 DialogueJsonLoader 캐시에 남아 있으므로 플레이를 다시 시작해야 반영된다.
/// </summary>
public static class DialogueSheetImporter
{
    private const string SettingsFolder = "Assets/Editor";
    private const string SettingsPath = "Assets/Editor/DialogueSheetSettings.asset";
    private const string DialogTitle = "Dialogue Sheet";
    private const string LogPrefix = "[DialogueSheet] ";
    private const string LocalLinesFile = "lines.csv";
    private const string LocalMetaFile = "meta.csv";
    private const int TimeoutSeconds = 20;

    #region Menu
    [UnityEditor.MenuItem("Tools/Dialogue/Sync from Google Sheet")]
    private static void SyncFromSheet()
    {
        RunFromWeb(false);
    }

    [UnityEditor.MenuItem("Tools/Dialogue/Validate Google Sheet (no write)")]
    private static void ValidateSheet()
    {
        RunFromWeb(true);
    }

    [UnityEditor.MenuItem("Tools/Dialogue/Import from Local CSV Folder...")]
    private static void ImportFromLocalFolder()
    {
        string folder = EditorUtility.OpenFolderPanel("lines.csv (와 meta.csv)가 들어 있는 폴더 선택", "", "");
        if (string.IsNullOrEmpty(folder)) return;

        string linesPath = Path.Combine(folder, LocalLinesFile);
        if (!File.Exists(linesPath))
        {
            EditorUtility.DisplayDialog(DialogTitle, $"선택한 폴더에 {LocalLinesFile} 가 없습니다.\n({LocalLinesFile}, {LocalMetaFile} 이름으로 저장해 주세요)", "확인");
            return;
        }

        string metaPath = Path.Combine(folder, LocalMetaFile);
        string linesCsv = File.ReadAllText(linesPath, Encoding.UTF8);
        string metaCsv = File.Exists(metaPath) ? File.ReadAllText(metaPath, Encoding.UTF8) : null;

        Process(linesCsv, metaCsv, GetOrCreateSettings(), false);
    }

    [UnityEditor.MenuItem("Tools/Dialogue/Select Sheet Settings")]
    private static void SelectSettings()
    {
        DialogueSheetSettings settings = GetOrCreateSettings();
        Selection.activeObject = settings;
        EditorGUIUtility.PingObject(settings);
    }
    #endregion

    #region Settings
    private static DialogueSheetSettings GetOrCreateSettings()
    {
        var settings = AssetDatabase.LoadAssetAtPath<DialogueSheetSettings>(SettingsPath);
        if (settings != null) return settings;

        if (!AssetDatabase.IsValidFolder(SettingsFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Editor");
        }

        settings = ScriptableObject.CreateInstance<DialogueSheetSettings>();
        AssetDatabase.CreateAsset(settings, SettingsPath);
        AssetDatabase.SaveAssets();
        return settings;
    }

    /// <summary>주소 전체를 붙여넣어도, ID만 넣어도 ID를 뽑아낸다.</summary>
    private static string ExtractSpreadsheetId(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        Match match = Regex.Match(input, @"/spreadsheets/d/([a-zA-Z0-9\-_]+)");
        return match.Success ? match.Groups[1].Value : input.Trim();
    }

    private static string BuildCsvUrl(string spreadsheetId, string gid)
    {
        string safeGid = string.IsNullOrWhiteSpace(gid) ? "0" : gid.Trim();
        return $"https://docs.google.com/spreadsheets/d/{spreadsheetId}/export?format=csv&gid={safeGid}";
    }
    #endregion

    #region Download
    private static void RunFromWeb(bool dryRun)
    {
        DialogueSheetSettings settings = GetOrCreateSettings();
        string spreadsheetId = ExtractSpreadsheetId(settings.spreadsheet);

        if (string.IsNullOrEmpty(spreadsheetId))
        {
            EditorUtility.DisplayDialog(DialogTitle,
                "스프레드시트 주소(또는 ID)가 비어 있습니다.\nDialogueSheetSettings에 입력한 뒤 다시 실행해 주세요.", "설정 열기");
            Selection.activeObject = settings;
            return;
        }

        string linesCsv = null;
        string metaCsv = null;
        string error = null;

        try
        {
            if (!TryDownload(BuildCsvUrl(spreadsheetId, settings.linesGid), "lines 탭", out linesCsv, out error))
            {
                ShowDownloadError(error);
                return;
            }

            if (!string.IsNullOrWhiteSpace(settings.metaGid))
            {
                if (!TryDownload(BuildCsvUrl(spreadsheetId, settings.metaGid), "meta 탭", out metaCsv, out error))
                {
                    ShowDownloadError(error);
                    return;
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Process(linesCsv, metaCsv, settings, dryRun);
    }

    private static bool TryDownload(string url, string label, out string text, out string error)
    {
        text = null;
        error = null;

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = TimeoutSeconds;
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();

            // 에디터 도구라 메인 스레드에서 끝날 때까지 기다린다 (취소 버튼 제공)
            while (!operation.isDone)
            {
                if (EditorUtility.DisplayCancelableProgressBar(DialogTitle, $"{label}을(를) 내려받는 중...", Mathf.Clamp01(request.downloadProgress)))
                {
                    request.Abort();
                    error = "사용자가 취소했습니다.";
                    return false;
                }

                Thread.Sleep(20);
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                error = $"{label} 다운로드 실패: {request.error} (HTTP {request.responseCode})";
                return false;
            }

            text = request.downloadHandler.text;
        }

        // 공유 설정이 막혀 있거나 gid가 틀리면 CSV 대신 로그인/오류 HTML 페이지가 내려온다
        if (text.TrimStart().StartsWith("<", StringComparison.Ordinal))
        {
            text = null;
            error = $"{label}: CSV 대신 웹 페이지(HTML)가 내려왔습니다.\n" +
                    "· 시트 공유가 '링크가 있는 모든 사용자 - 뷰어'인지\n" +
                    "· 스프레드시트 ID와 탭 gid가 맞는지 확인해 주세요.";
            return false;
        }

        return true;
    }

    private static void ShowDownloadError(string error)
    {
        Debug.LogError(LogPrefix + error);
        EditorUtility.DisplayDialog(DialogTitle, error, "확인");
    }
    #endregion

    #region Process / Write
    private static void Process(string linesCsv, string metaCsv, DialogueSheetSettings settings, bool dryRun)
    {
        var environment = new UnityEnvironment(settings);
        SheetConvertResult result = DialogueSheetConverter.Build(linesCsv, metaCsv, environment);

        foreach (string info in result.Infos) Debug.Log(LogPrefix + info);
        foreach (string warning in result.Warnings) Debug.LogWarning(LogPrefix + warning);

        if (result.Errors.Count > 0)
        {
            foreach (string error in result.Errors) Debug.LogError(LogPrefix + error);

            EditorUtility.DisplayDialog(DialogTitle,
                $"오류 {result.Errors.Count}개 때문에 변환하지 않았습니다.\n기존 JSON 파일은 하나도 바뀌지 않았습니다.\n\nConsole 창에서 오류의 탭/행 위치를 확인해 주세요.",
                "확인");
            return;
        }

        if (dryRun)
        {
            EditorUtility.DisplayDialog(DialogTitle,
                $"검증 통과\n시퀀스 {result.SequenceCount}개 / 생성될 파일 {result.Files.Count}개 / 경고 {result.Warnings.Count}개\n\n(파일은 쓰지 않았습니다)",
                "확인");
            return;
        }

        WriteFiles(result, settings);
    }

    private static void WriteFiles(SheetConvertResult result, DialogueSheetSettings settings)
    {
        var utf8 = new UTF8Encoding(false); // BOM 없는 UTF-8
        var generated = new HashSet<string>();
        int written = 0;
        int unchanged = 0;

        foreach (GeneratedDialogueFile file in result.Files)
        {
            string directory = Path.Combine(settings.outputRoot, file.Language);
            Directory.CreateDirectory(directory);

            string path = Path.Combine(directory, file.SequenceId + ".json").Replace('\\', '/');
            generated.Add(path);

            if (File.Exists(path) && File.ReadAllText(path, utf8) == file.Json)
            {
                unchanged++;
                continue;
            }

            File.WriteAllText(path, file.Json, utf8);
            written++;
        }

        AssetDatabase.Refresh();

        ReportFilesNotInSheet(settings.outputRoot, generated);

        string summary = $"시퀀스 {result.SequenceCount}개 동기화 완료\n새로 쓴 파일 {written}개 / 변경 없음 {unchanged}개 / 경고 {result.Warnings.Count}개";
        Debug.Log(LogPrefix + summary.Replace("\n", " - "));
        EditorUtility.DisplayDialog(DialogTitle, summary, "확인");
    }

    /// <summary>출력 폴더에 있지만 이번 시트에서 만들어지지 않은 JSON을 알려준다 (삭제는 하지 않는다).</summary>
    private static void ReportFilesNotInSheet(string outputRoot, HashSet<string> generated)
    {
        if (!Directory.Exists(outputRoot)) return;

        var others = new List<string>();
        foreach (string path in Directory.GetFiles(outputRoot, "*.json", SearchOption.AllDirectories))
        {
            string normalized = path.Replace('\\', '/');
            if (!generated.Contains(normalized)) others.Add(normalized);
        }

        if (others.Count > 0)
        {
            Debug.Log(LogPrefix + $"시트에서 만들어지지 않은 기존 JSON {others.Count}개는 건드리지 않았습니다:\n" + string.Join("\n", others.ToArray()));
        }
    }
    #endregion

    #region Environment
    /// <summary>변환기가 묻는 "이 에셋/파일이 있나?"를 Unity Resources와 파일 시스템으로 답한다.</summary>
    private sealed class UnityEnvironment : IDialogueSheetEnvironment
    {
        private readonly DialogueSheetSettings _settings;
        private readonly Dictionary<string, SpeakerDataSO> _speakers = new Dictionary<string, SpeakerDataSO>();
        private readonly Dictionary<string, bool> _audio = new Dictionary<string, bool>();

        public UnityEnvironment(DialogueSheetSettings settings)
        {
            _settings = settings;
        }

        public bool SpeakerExists(string speakerId)
        {
            return GetSpeaker(speakerId) != null;
        }

        public bool SpeakerHasExpression(string speakerId, string expressionName)
        {
            SpeakerDataSO speaker = GetSpeaker(speakerId);
            if (speaker == null || speaker.expressions == null) return false;

            foreach (SpeakerExpression expression in speaker.expressions)
            {
                if (expression.expressionName == expressionName) return true;
            }

            return false;
        }

        public bool AudioExists(string resourcePath)
        {
            bool exists;
            if (!_audio.TryGetValue(resourcePath, out exists))
            {
                exists = Resources.Load<AudioClip>(resourcePath) != null;
                _audio[resourcePath] = exists;
            }

            return exists;
        }

        public bool SceneExists(string sceneName)
        {
            SceneChanger.SceneType sceneType;
            return Enum.TryParse(sceneName, true, out sceneType)
                   && Enum.IsDefined(typeof(SceneChanger.SceneType), sceneType);
        }

        public bool SequenceFileExists(string language, string sequenceId)
        {
            return File.Exists(Path.Combine(Path.Combine(_settings.outputRoot, language), sequenceId + ".json"));
        }

        private SpeakerDataSO GetSpeaker(string speakerId)
        {
            SpeakerDataSO speaker;
            if (!_speakers.TryGetValue(speakerId, out speaker))
            {
                speaker = Resources.Load<SpeakerDataSO>(_settings.speakerResourcesPath + "/" + speakerId);
                _speakers[speakerId] = speaker;
            }

            return speaker;
        }
    }
    #endregion
}