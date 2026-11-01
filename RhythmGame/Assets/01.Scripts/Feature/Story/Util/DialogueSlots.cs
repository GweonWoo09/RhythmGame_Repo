using System.Globalization;

/// <summary>
/// 화면 위치 슬롯 이름 규칙. 시트/JSON의 "left", "right", "center"를 슬롯 번호로 바꾼다.
///
///   0 = Left(왼쪽)   1 = Right(오른쪽)   2 = Center(가운데)
///
/// DialogueUIView.speakerSlots 배열은 이 순서로 구성한다: [0]=Left, [1]=Right, [2]=Center.
/// 기존 데이터의 숫자 슬롯(0, 1)은 그대로 동작한다. 그 밖의 번호(3 이상)도 숫자로 쓸 수 있다.
/// Unity API에 의존하지 않는다.
/// </summary>
public static class DialogueSlots
{
    public const int Left = 0;
    public const int Right = 1;
    public const int Center = 2;

    /// <summary>"left"/"right"/"center"(한글 별칭 포함) 또는 0 이상의 정수를 슬롯 번호로 해석한다.</summary>
    public static bool TryParse(string text, out int slot)
    {
        slot = -1;
        if (string.IsNullOrWhiteSpace(text)) return false;

        string trimmed = text.Trim();

        switch (trimmed.ToLowerInvariant())
        {
            case "left":
            case "왼쪽":
                slot = Left;
                return true;

            case "right":
            case "오른쪽":
                slot = Right;
                return true;

            case "center":
            case "가운데":
            case "중앙":
                slot = Center;
                return true;
        }

        int number;
        if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out number) && number >= 0)
        {
            slot = number;
            return true;
        }

        return false;
    }

    /// <summary>로그용 표기. 예: 0 -> "left(0)", 5 -> "5"</summary>
    public static string Describe(int slot)
    {
        switch (slot)
        {
            case Left: return "left(0)";
            case Right: return "right(1)";
            case Center: return "center(2)";
            default: return slot.ToString(CultureInfo.InvariantCulture);
        }
    }
}