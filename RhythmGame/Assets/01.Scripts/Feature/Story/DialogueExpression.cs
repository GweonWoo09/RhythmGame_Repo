using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// 대사 데이터의 조건식과 플래그 기록식을 해석하는 정적 유틸.
///
/// [조건식]  Evaluate / TryValidateCondition
///   "ch1_eager==1"          비교: ==  !=  >=  <=  >  <
///   "ch1_eager"             값이 0이 아니면 참
///   "!ch1_eager"            값이 0이면 참
///   "a==1 && b>=2"          AND
///   "a==1 || b==1"          OR (AND보다 우선순위 낮음)
///   빈 문자열/null           항상 참
///
/// [기록식]  Apply / TryValidateAssignment
///   "ch1_eager=1"           대입
///   "affinity+=1"           가산
///   "affinity-=1"           감산
///
/// 플래그 값은 GameManager의 스토리 플래그를 읽고 쓴다.
/// GameManager가 없는 환경(스토리 씬만 단독 실행 등)에서는 임시 저장소를 대신 쓴다.
/// </summary>
public static class DialogueExpression
{
    private static readonly Dictionary<string, int> _fallbackFlags = new Dictionary<string, int>();

    // 긴 연산자를 먼저 검사해야 ">=" 가 ">" 로 잘못 잡히지 않는다.
    private static readonly string[] CompareOps = { "==", "!=", ">=", "<=", ">", "<" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _fallbackFlags.Clear();
    }

    #region Flag Access
    public static int GetFlag(string key)
    {
        if (GameManager.Instance != null)
        {
            return GameManager.Instance.GetStoryFlag(key, 0);
        }

        return _fallbackFlags.TryGetValue(key, out int value) ? value : 0;
    }

    public static void SetFlag(string key, int value)
    {
        if (string.IsNullOrEmpty(key)) return;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetStoryFlag(key, value);
        }
        else
        {
            _fallbackFlags[key] = value;
        }
    }
    #endregion

    #region Condition
    /// <summary>조건식을 평가한다. 비어있으면 true, 해석에 실패하면 에러 로그 후 false.</summary>
    public static bool Evaluate(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression)) return true;

        foreach (string orPart in expression.Split(new[] { "||" }, StringSplitOptions.None))
        {
            bool allTrue = true;

            foreach (string andPart in orPart.Split(new[] { "&&" }, StringSplitOptions.None))
            {
                if (!TryParseClause(andPart.Trim(), out string key, out string op, out int value, out string error))
                {
                    Debug.LogError($"[DialogueExpression] 조건 '{expression}' 해석 실패: {error}");
                    return false;
                }

                if (!Compare(GetFlag(key), op, value))
                {
                    allTrue = false;
                    break;
                }
            }

            if (allTrue) return true;
        }

        return false;
    }

    /// <summary>문법만 검사한다 (플래그 값은 읽지 않음). 로더/임포터의 사전 검증용.</summary>
    public static bool TryValidateCondition(string expression, out string error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(expression)) return true;

        foreach (string orPart in expression.Split(new[] { "||" }, StringSplitOptions.None))
        {
            foreach (string andPart in orPart.Split(new[] { "&&" }, StringSplitOptions.None))
            {
                if (!TryParseClause(andPart.Trim(), out _, out _, out _, out error))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool Compare(int flagValue, string op, int value)
    {
        switch (op)
        {
            case "==": return flagValue == value;
            case "!=": return flagValue != value;
            case ">=": return flagValue >= value;
            case "<=": return flagValue <= value;
            case ">": return flagValue > value;
            case "<": return flagValue < value;
            default: return false;
        }
    }

    private static bool TryParseClause(string clause, out string key, out string op, out int value, out string error)
    {
        key = null;
        op = null;
        value = 0;
        error = null;

        if (string.IsNullOrEmpty(clause))
        {
            error = "비어있는 조건 항목이 있습니다.";
            return false;
        }

        foreach (string candidate in CompareOps)
        {
            int index = clause.IndexOf(candidate, StringComparison.Ordinal);
            if (index < 0) continue;

            key = clause.Substring(0, index).Trim();
            string valueText = clause.Substring(index + candidate.Length).Trim();

            if (!IsValidKey(key))
            {
                error = $"플래그 이름이 올바르지 않습니다: '{key}'";
                return false;
            }

            if (!int.TryParse(valueText, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                error = $"비교할 값이 정수가 아닙니다: '{valueText}'";
                return false;
            }

            op = candidate;
            return true;
        }

        // 연산자가 없으면 "key"(0이 아니면 참) 또는 "!key"(0이면 참)
        bool negate = clause.StartsWith("!", StringComparison.Ordinal);
        key = (negate ? clause.Substring(1) : clause).Trim();

        if (!IsValidKey(key))
        {
            error = $"플래그 이름이 올바르지 않습니다: '{key}'";
            return false;
        }

        op = negate ? "==" : "!=";
        value = 0;
        return true;
    }
    #endregion

    #region Assignment
    /// <summary>"key=1", "key+=1", "key-=1" 형식의 기록식을 적용한다.</summary>
    public static void Apply(string assignment)
    {
        if (string.IsNullOrWhiteSpace(assignment)) return;

        if (!TryParseAssignment(assignment.Trim(), out string key, out string mode, out int value, out string error))
        {
            Debug.LogError($"[DialogueExpression] 플래그 기록식 '{assignment}' 해석 실패: {error}");
            return;
        }

        switch (mode)
        {
            case "=": SetFlag(key, value); break;
            case "+=": SetFlag(key, GetFlag(key) + value); break;
            case "-=": SetFlag(key, GetFlag(key) - value); break;
        }
    }

    public static void ApplyAll(string[] assignments)
    {
        if (assignments == null) return;

        foreach (string assignment in assignments)
        {
            Apply(assignment);
        }
    }

    public static bool TryValidateAssignment(string assignment, out string error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(assignment)) return true;

        return TryParseAssignment(assignment.Trim(), out _, out _, out _, out error);
    }

    private static bool TryParseAssignment(string text, out string key, out string mode, out int value, out string error)
    {
        key = null;
        mode = null;
        value = 0;
        error = null;

        int index;
        if ((index = text.IndexOf("+=", StringComparison.Ordinal)) >= 0) mode = "+=";
        else if ((index = text.IndexOf("-=", StringComparison.Ordinal)) >= 0) mode = "-=";
        else if ((index = text.IndexOf("=", StringComparison.Ordinal)) >= 0) mode = "=";
        else
        {
            error = "'=' 가 없습니다. 예: ch1_eager=1";
            return false;
        }

        key = text.Substring(0, index).Trim();
        string valueText = text.Substring(index + mode.Length).Trim();

        if (!IsValidKey(key))
        {
            error = $"플래그 이름이 올바르지 않습니다: '{key}'";
            return false;
        }

        if (!int.TryParse(valueText, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            error = $"값이 정수가 아닙니다: '{valueText}'";
            return false;
        }

        return true;
    }
    #endregion

    /// <summary>플래그 이름은 글자, 숫자, 밑줄만 허용한다.</summary>
    private static bool IsValidKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;

        foreach (char c in key)
        {
            if (!char.IsLetterOrDigit(c) && c != '_') return false;
        }

        return true;
    }
}