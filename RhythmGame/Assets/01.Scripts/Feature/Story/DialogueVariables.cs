using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// 대사 텍스트 안의 {변수}를 실제 값으로 바꾸는 정적 유틸.
///
///   "{player}, 어서 와."  ->  "하늘, 어서 와."
///
/// 기본 제공 변수: {player} = 플레이어 닉네임 (설정 전에는 DefaultPlayerName).
/// 그 밖의 변수는 Set("키", "값")으로 등록해서 {키}로 쓴다. 등록되지 않은 {키}는 그대로 화면에 남고,
/// 같은 키에 대해 경고를 한 번만 남긴다.
///
/// 이름이 표시되는 곳(SpeakerDataSO.speakerName, DialogLine.displayName)과 선택지 문구에도 적용된다.
/// 주인공 SpeakerDataSO의 speakerName을 "{player}"로 해두면 주인공 대사 이름이 항상 닉네임으로 표시된다.
///
/// 닉네임 저장: 지금은 PlayerPrefs를 쓴다. 세이브 데이터(GameManager)에 저장하고 싶다면
/// PlayerName의 getter/setter 두 곳만 바꾸면 된다.
/// </summary>
public static class DialogueVariables
{
    public const string PlayerKey = "player";
    public const string DefaultPlayerName = "주인공";

    private const string PlayerNamePrefsKey = "Dialogue.PlayerName";

    private static readonly Dictionary<string, string> _values = new Dictionary<string, string>();
    private static readonly HashSet<string> _warnedKeys = new HashSet<string>();
    private static readonly Regex Placeholder = new Regex(@"\{([A-Za-z0-9_]+)\}", RegexOptions.Compiled);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _values.Clear();
        _warnedKeys.Clear();
    }

    /// <summary>플레이어 닉네임. 설정한 적이 없으면 DefaultPlayerName.</summary>
    public static string PlayerName
    {
        get
        {
            string value;
            if (_values.TryGetValue(PlayerKey, out value) && !string.IsNullOrEmpty(value)) return value;

            string saved = PlayerPrefs.GetString(PlayerNamePrefsKey, "");
            return string.IsNullOrEmpty(saved) ? DefaultPlayerName : saved;
        }
        set
        {
            _values[PlayerKey] = value;
            PlayerPrefs.SetString(PlayerNamePrefsKey, value ?? "");
            PlayerPrefs.Save();
        }
    }

    /// <summary>닉네임을 한 번이라도 정했는지 (튜토리얼 닉네임 입력을 건너뛸지 판단할 때)</summary>
    public static bool HasPlayerName
    {
        get { return !string.IsNullOrEmpty(PlayerPrefs.GetString(PlayerNamePrefsKey, "")); }
    }

    /// <summary>{키}로 쓸 변수를 등록한다. (player는 PlayerName을 쓸 것)</summary>
    public static void Set(string key, string value)
    {
        if (string.IsNullOrEmpty(key)) return;

        if (key == PlayerKey) PlayerName = value;
        else _values[key] = value;
    }

    /// <summary>텍스트의 {변수}를 값으로 바꾼다. {가 없으면 그대로 돌려준다.</summary>
    public static string Resolve(string text)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) return text;

        return Placeholder.Replace(text, match =>
        {
            string key = match.Groups[1].Value;

            if (key == PlayerKey) return PlayerName;

            string value;
            if (_values.TryGetValue(key, out value)) return value;

            if (_warnedKeys.Add(key))
            {
                Debug.LogWarning($"[DialogueVariables] 등록되지 않은 변수 {{{key}}}가 대사에 있습니다. (그대로 표시됩니다)");
            }

            return match.Value;
        });
    }
}