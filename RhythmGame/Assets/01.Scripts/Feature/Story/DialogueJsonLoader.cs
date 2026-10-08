using System;
using System.Collections.Generic;
using UnityEngine;

#region JSON Models
// JsonUtility는 Dictionary, 최상위 배열, UnityEngine.Object 참조를 지원하지 않는다.
// 그래서 JSON에는 문자열 ID/경로만 담고, 에셋 연결은 DialogueJsonLoader가 처리한다.

/// <summary>JSON 파일 하나(= 대화 시퀀스 하나)의 최상위 구조</summary>
[Serializable]
public class DialogueJsonData
{
    [Tooltip("시퀀스 기본 BGM의 Resources 경로. 예: \"Audio/BGM/calm_morning\". 비우면 기존 BGM 유지")]
    public string bgm;
    public DialogueLineJson[] lines;
}

[Serializable]
public class DialogueLineJson
{
    public string speakerId;      // Resources/Speakers/{speakerId} 의 SpeakerDataSO 에셋 이름
    public string expression;     // SpeakerDataSO.expressions 의 expressionName
    public int screenSlot;
    public string text;
    public string bgmOverride;    // 이 줄부터 BGM 교체 (Resources 경로, 비우면 유지)
    public DialogueChoiceJson[] choices;
}

[Serializable]
public class DialogueChoiceJson
{
    public string text;
    public int targetIndex;
    public string targetSequenceId; // 다른 JSON 시퀀스로 분기할 때 그 파일 이름 (확장자 제외)
    public string flagKey;
    public int flagValue;
}
#endregion

/// <summary>
/// Resources/Dialogue/{언어}/{sequenceId}.json 을 읽어 DialogueDataSO(런타임 인스턴스)로 변환한다.
/// 변환된 결과는 기존 DialogueManager가 그대로 재생할 수 있다.
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
        _cache[path] = sequence;
        return sequence;
    }

    private static DialogueDataSO ConvertToDataSO(DialogueJsonData json, string sequenceId)
    {
        var sequence = ScriptableObject.CreateInstance<DialogueDataSO>();
        sequence.name = sequenceId;
        sequence.bgm = LoadAudio(json.bgm);

        var lines = new DialogLine[json.lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            var src = json.lines[i];

            lines[i] = new DialogLine
            {
                speaker = LoadSpeaker(src.speakerId, sequenceId, i),
                expressionName = src.expression,
                screenSlot = src.screenSlot,
                dialogText = src.text,
                bgmOverride = LoadAudio(src.bgmOverride),
                choices = ConvertChoices(src.choices)
            };
        }

        sequence.lines = lines;
        return sequence;
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
                targetIndex = src[i].targetIndex,
                targetSequence = null,                         // JSON에서는 ID로만 지정
                targetSequenceId = src[i].targetSequenceId,    // 선택 시점에 지연 로드 (순환 분기 방지)
                flagKey = src[i].flagKey,
                storyFlagValue = src[i].flagValue
            };
        }

        return choices;
    }

    private static SpeakerDataSO LoadSpeaker(string speakerId, string sequenceId, int lineIndex)
    {
        if (string.IsNullOrEmpty(speakerId))
        {
            Debug.LogWarning($"[DialogueJsonLoader] {sequenceId}의 {lineIndex}번 줄에 speakerId가 없습니다.");
            return null;
        }

        var speaker = Resources.Load<SpeakerDataSO>($"{SpeakerRoot}/{speakerId}");
        if (speaker == null)
        {
            Debug.LogWarning($"[DialogueJsonLoader] Resources/{SpeakerRoot}/{speakerId} 에셋을 찾을 수 없습니다. ({sequenceId}의 {lineIndex}번 줄)");
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
}
