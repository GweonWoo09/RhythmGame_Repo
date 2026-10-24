using System;
using System.Collections.Generic;
using UnityEngine;

#region JSON Models
// JsonUtility는 Dictionary, 최상위 배열, UnityEngine.Object 참조, 다형성을 지원하지 않는다.
// 그래서 JSON에는 문자열 ID/경로와 문자열 식(조건, 플래그, 명령)만 담고,
// 에셋 연결과 해석은 DialogueJsonLoader / DialogueExpression이 처리한다.
//
// JSON 키 "if", "goto"는 C# 예약어라 아래 모델에서 @if, @goto 로 선언했다.
// (@ 는 식별자 표기일 뿐이고, JsonUtility가 보는 필드 이름은 "if", "goto" 그대로다.)

/// <summary>JSON 파일 하나(= 대화 시퀀스 하나)의 최상위 구조</summary>
[Serializable]
public class DialogueJsonData
{
    public int chapterId;
    [Tooltip("시퀀스 기본 BGM의 Resources 경로. 예: \"Audio/BGM/calm_morning\". 비우면 기존 BGM 유지")]
    public string bgm;
    public DialogueCastJson[] cast;
    public DialogueEndJson onEnd;
    public DialogueLineJson[] lines;
}

[Serializable]
public class DialogueCastJson
{
    public int slot;
    public string speakerId;
    public string expression;   // 비우면 첫 번째 표정
}

[Serializable]
public class DialogueEndJson
{
    public string scene;        // SceneChanger.SceneType 이름. 비우면 이동 대상 미지정
    public int clearChapter;
    public int unlockChapter;
    public string songId;
    public string difficulty;
}

[Serializable]
public class DialogueLineJson
{
    public string id;             // 점프 대상이 되는 줄의 이름표
    public string @if;            // 조건식. 거짓이면 이 줄 건너뜀
    public string speakerId;      // 비우면 내레이션. Resources/Speakers/{speakerId}
    public string expression;     // SpeakerDataSO.expressions 의 expressionName
    public int screenSlot;        // 0=Left, 1=Right, 2=Center
    public string name;           // 이 줄에서만 쓸 표시 이름 (예: "???"). 비우면 SpeakerDataSO.speakerName
    public string style;          // 대사 표시 방식 (thought, shout, whisper ...)
    public string text;
    public string bgmOverride;    // 이 줄부터 BGM 교체 (Resources 경로, 비우면 유지)
    public string[] setFlags;     // 예: ["ch1_eager=1", "affinity+=1"]
    public string[] commands;     // 예: ["hide:0", "bg:studio_night"]
    public DialogueBranchJson[] branch;
    public string @goto;          // 이 줄 다음에 이동할 줄 id ("end"면 종료)
    public DialogueChoiceJson[] choices;
}

[Serializable]
public class DialogueBranchJson
{
    public string @if;
    public string @goto;
}

[Serializable]
public class DialogueChoiceJson
{
    public string text;
    public string @if;              // 거짓이면 선택지 숨김
    public string @goto;            // 이동할 줄 id (권장)
    public int targetIndex;         // @goto 가 없을 때만 사용 (하위 호환)
    public string targetSequenceId; // 다른 JSON 시퀀스로 분기할 때 그 파일 이름 (확장자 제외)
    public string[] setFlags;
    public string flagKey;          // (하위 호환) 단일 플래그
    public int flagValue;
}
#endregion

/// <summary>
/// Resources/Dialogue/{언어}/{sequenceId}.json 을 읽어 DialogueDataSO(런타임 인스턴스)로 변환한다.
/// 변환된 결과는 DialogueManager가 그대로 재생할 수 있다.
/// 변환 직후 id 중복, 존재하지 않는 goto 대상, 조건/플래그 문법 오류를 검사해 에러 로그로 알려준다.
/// </summary>
public static class DialogueJsonLoader
{
    private const string DialogueRoot = "Dialogue";
    private const string SpeakerRoot = "Speakers";

    /// <summary>현재 언어 폴더 이름. 언어별로 같은 파일명의 JSON을 두면 텍스트만 교체된다.</summary>
    public static string Language { get; private set; } = "ko";

    private static readonly Dictionary<string, DialogueDataSO> _cache = new Dictionary<string, DialogueDataSO>();

    /// <summary>언어를 바꾸면 캐시를 비워서 다음 Load부터 새 언어 JSON을 읽는다.</summary>
    public static void SetLanguage(string language)
    {
        if (Language == language) return;

        Language = language;
        _cache.Clear();
    }

    /// <summary>sequenceId에 해당하는 JSON을 로드해 DialogueDataSO로 반환. 실패하면 null.</summary>
    public static DialogueDataSO Load(string sequenceId)
    {
        if (string.IsNullOrEmpty(sequenceId))
        {
            Debug.LogError("[DialogueJsonLoader] sequenceId가 비어있습니다.");
            return null;
        }

        string path = $"{DialogueRoot}/{Language}/{sequenceId}";

        if (_cache.TryGetValue(path, out var cached))
        {
            return cached;
        }

        TextAsset textAsset = Resources.Load<TextAsset>(path);
        if (textAsset == null)
        {
            Debug.LogError($"[DialogueJsonLoader] JSON 파일을 찾을 수 없습니다: Resources/{path}.json");
            return null;
        }

        DialogueJsonData json;
        try
        {
            json = JsonUtility.FromJson<DialogueJsonData>(textAsset.text);
        }
        catch (ArgumentException e)
        {
            Debug.LogError($"[DialogueJsonLoader] JSON 형식 오류 ({path}): {e.Message}");
            return null;
        }

        if (json == null || json.lines == null || json.lines.Length == 0)
        {
            Debug.LogError($"[DialogueJsonLoader] 대사(lines)가 비어있습니다: {path}");
            return null;
        }

        var sequence = ConvertToDataSO(json, sequenceId);
        ValidateSequence(sequence);

        _cache[path] = sequence;
        return sequence;
    }

    #region Convert
    private static DialogueDataSO ConvertToDataSO(DialogueJsonData json, string sequenceId)
    {
        var sequence = ScriptableObject.CreateInstance<DialogueDataSO>();
        sequence.name = sequenceId;
        sequence.chapterId = json.chapterId;
        sequence.bgm = LoadAudio(json.bgm);
        sequence.cast = ConvertCast(json.cast, sequenceId);
        sequence.onEnd = ConvertEnd(json.onEnd);

        var lines = new DialogLine[json.lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            var src = json.lines[i];

            lines[i] = new DialogLine
            {
                id = src.id,
                condition = src.@if,
                speaker = LoadSpeaker(src.speakerId, $"{sequenceId}의 {i}번 줄"),
                expressionName = src.expression,
                screenSlot = src.screenSlot,
                displayName = src.name,
                style = src.style,
                dialogText = src.text,
                bgmOverride = LoadAudio(src.bgmOverride),
                setFlags = NullIfEmpty(src.setFlags),
                commands = NullIfEmpty(src.commands),
                branches = ConvertBranches(src.branch),
                gotoLabel = src.@goto,
                choices = ConvertChoices(src.choices)
            };
        }

        sequence.lines = lines;
        return sequence;
    }

    private static DialogueCast[] ConvertCast(DialogueCastJson[] src, string sequenceId)
    {
        if (src == null || src.Length == 0) return null;

        var cast = new DialogueCast[src.Length];
        for (int i = 0; i < cast.Length; i++)
        {
            cast[i] = new DialogueCast
            {
                slot = src[i].slot,
                speaker = LoadSpeaker(src[i].speakerId, $"{sequenceId}의 cast {i}번"),
                expressionName = src[i].expression
            };
        }

        return cast;
    }

    private static DialogueEndAction ConvertEnd(DialogueEndJson src)
    {
        if (src == null) return default(DialogueEndAction);

        return new DialogueEndAction
        {
            scene = src.scene,
            clearChapter = src.clearChapter,
            unlockChapter = src.unlockChapter,
            songId = src.songId,
            difficulty = src.difficulty
        };
    }

    private static DialogBranch[] ConvertBranches(DialogueBranchJson[] src)
    {
        if (src == null || src.Length == 0) return null;

        var branches = new DialogBranch[src.Length];
        for (int i = 0; i < branches.Length; i++)
        {
            branches[i] = new DialogBranch
            {
                condition = src[i].@if,
                gotoLabel = src[i].@goto
            };
        }

        return branches;
    }

    private static DialogChoice[] ConvertChoices(DialogueChoiceJson[] src)
    {
        if (src == null || src.Length == 0) return null;

        var choices = new DialogChoice[src.Length];
        for (int i = 0; i < choices.Length; i++)
        {
            choices[i] = new DialogChoice
            {
                choiceText = src[i].text,
                condition = src[i].@if,
                gotoLabel = src[i].@goto,
                targetIndex = src[i].targetIndex,
                targetSequence = null,                         // JSON에서는 ID로만 지정
                targetSequenceId = src[i].targetSequenceId,    // 선택 시점에 지연 로드 (순환 분기 방지)
                setFlags = NullIfEmpty(src[i].setFlags),
                flagKey = src[i].flagKey,
                storyFlagValue = src[i].flagValue
            };
        }

        return choices;
    }

    private static T[] NullIfEmpty<T>(T[] array)
    {
        return (array == null || array.Length == 0) ? null : array;
    }

    /// <summary>speakerId에 해당하는 SpeakerDataSO를 찾는다. 없으면 null (경고 없음). 연출 명령(show) 등에서 쓴다.</summary>
    public static SpeakerDataSO FindSpeaker(string speakerId)
    {
        if (string.IsNullOrEmpty(speakerId)) return null;
        return Resources.Load<SpeakerDataSO>($"{SpeakerRoot}/{speakerId}");
    }

    /// <summary>speakerId가 비어있으면 내레이션(화자 없음)으로 보고 null을 반환한다 (경고 없음).</summary>
    private static SpeakerDataSO LoadSpeaker(string speakerId, string context)
    {
        if (string.IsNullOrEmpty(speakerId)) return null;

        var speaker = Resources.Load<SpeakerDataSO>($"{SpeakerRoot}/{speakerId}");
        if (speaker == null)
        {
            Debug.LogWarning($"[DialogueJsonLoader] Resources/{SpeakerRoot}/{speakerId} 에셋을 찾을 수 없습니다. ({context})");
        }

        return speaker;
    }

    private static AudioClip LoadAudio(string resourcePath)
    {
        if (string.IsNullOrEmpty(resourcePath)) return null;

        var clip = Resources.Load<AudioClip>(resourcePath);
        if (clip == null)
        {
            Debug.LogWarning($"[DialogueJsonLoader] Resources/{resourcePath} 오디오를 찾을 수 없습니다.");
        }

        return clip;
    }
    #endregion

    #region Validate
    /// <summary>
    /// 변환된 시퀀스를 검사해 에러 로그로 알린다. (재생은 막지 않는다)
    /// 다른 시퀀스로 분기하는 선택지의 goto 라벨은 그 시퀀스를 열어봐야 알 수 있어 검사하지 않는다.
    /// </summary>
    private static void ValidateSequence(DialogueDataSO sequence)
    {
        var lines = sequence.lines;
        var ids = new HashSet<string>();

        for (int i = 0; i < lines.Length; i++)
        {
            string id = lines[i].id;
            if (string.IsNullOrEmpty(id)) continue;

            if (id == DialogueDataSO.EndLabel)
            {
                Report(sequence.name, i, $"'{DialogueDataSO.EndLabel}'는 예약어라 id로 쓸 수 없습니다.");
            }
            else if (!ids.Add(id))
            {
                Report(sequence.name, i, $"id '{id}'가 중복됩니다.");
            }
        }

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            CheckCondition(sequence.name, i, line.condition);
            CheckLabel(ids, sequence.name, i, line.gotoLabel);
            CheckAssignments(sequence.name, i, line.setFlags);

            if (line.branches != null)
            {
                foreach (var branch in line.branches)
                {
                    CheckCondition(sequence.name, i, branch.condition);
                    CheckLabel(ids, sequence.name, i, branch.gotoLabel);
                }
            }

            if (line.choices != null)
            {
                foreach (var choice in line.choices)
                {
                    CheckCondition(sequence.name, i, choice.condition);
                    CheckAssignments(sequence.name, i, choice.setFlags);

                    // 같은 시퀀스 안에서 점프하는 선택지만 검사 가능
                    if (string.IsNullOrEmpty(choice.targetSequenceId))
                    {
                        if (!string.IsNullOrEmpty(choice.gotoLabel))
                        {
                            CheckLabel(ids, sequence.name, i, choice.gotoLabel);
                        }
                        else if (choice.targetIndex < 0 || choice.targetIndex >= lines.Length)
                        {
                            Report(sequence.name, i, $"선택지 '{choice.choiceText}'의 targetIndex({choice.targetIndex})가 범위를 벗어났습니다.");
                        }
                    }
                }
            }
        }
    }

    private static void CheckLabel(HashSet<string> ids, string sequenceName, int lineIndex, string label)
    {
        if (string.IsNullOrEmpty(label) || label == DialogueDataSO.EndLabel) return;

        if (!ids.Contains(label))
        {
            Report(sequenceName, lineIndex, $"goto 대상 id '{label}'가 이 시퀀스에 없습니다.");
        }
    }

    private static void CheckCondition(string sequenceName, int lineIndex, string condition)
    {
        if (!DialogueExpression.TryValidateCondition(condition, out string error))
        {
            Report(sequenceName, lineIndex, $"조건식 '{condition}' 오류: {error}");
        }
    }

    private static void CheckAssignments(string sequenceName, int lineIndex, string[] assignments)
    {
        if (assignments == null) return;

        foreach (string assignment in assignments)
        {
            if (!DialogueExpression.TryValidateAssignment(assignment, out string error))
            {
                Report(sequenceName, lineIndex, $"플래그 기록식 '{assignment}' 오류: {error}");
            }
        }
    }

    private static void Report(string sequenceName, int lineIndex, string message)
    {
        Debug.LogError($"[DialogueJsonLoader] {sequenceName} {lineIndex}번 줄: {message}");
    }
    #endregion
}