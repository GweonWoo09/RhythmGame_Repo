using UnityEngine;

/// <summary>
/// 캐릭터(화자) 데이터를 담는 에셋. 이름과 표정별 스프라이트를 갖는다.
/// Project 창에서 우클릭 -> Create -> Dialog -> Speaker 로 생성.
/// 씬/대화 시퀀스와 무관하게 캐릭터 단위로 하나씩 관리한다 (예: Speaker_Yuna.asset).
/// </summary>
[CreateAssetMenu(fileName = "NewSpeaker", menuName = "Dialog/Speaker")]
public class SpeakerDataSO : ScriptableObject
{
    [Tooltip("대화창에 표시될 이름")]
    public string speakerName;

    [Tooltip("표정별 스프라이트 목록")]
    public SpeakerExpression[] expressions;

    /// <summary>
    /// expressionName에 해당하는 스프라이트를 찾는다.
    /// 없으면 첫 번째 표정(기본 표정)을 대신 반환하고 경고를 남긴다.
    /// </summary>
    public Sprite GetSprite(string expressionName)
    {
        if (expressions == null || expressions.Length == 0)
        {
            Debug.LogWarning($"[SpeakerSO] '{speakerName}'에 등록된 표정이 없습니다.");
            return null;
        }

        foreach (var expr in expressions)
        {
            if (expr.expressionName == expressionName)
            {
                return expr.sprite;
            }
        }

        Debug.LogWarning($"[SpeakerSO] '{speakerName}'에서 '{expressionName}' 표정을 찾지 못해 기본 표정을 사용합니다.");
        return expressions[0].sprite;
    }
}

[System.Serializable]
public struct SpeakerExpression
{
    [Tooltip("DialogLine의 expressionName과 매칭되는 식별자. 예: \"Normal\", \"Happy\", \"Angry\"")]
    public string expressionName;
    public Sprite sprite;
}