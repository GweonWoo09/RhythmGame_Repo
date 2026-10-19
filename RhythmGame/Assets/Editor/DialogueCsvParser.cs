using System.Collections.Generic;
using System.Text;

/// <summary>
/// RFC 4180 방식의 CSV 파서. 구글 시트가 내보내는 CSV를 읽는 데 쓴다.
/// - 따옴표로 감싼 셀 안의 쉼표, 줄바꿈(Alt+Enter), 이중 따옴표("")를 처리한다.
/// - 줄바꿈은 \r\n, \n, \r 모두 허용한다.
/// - 파일 맨 앞의 BOM은 무시한다.
/// - 빈 줄도 한 레코드로 돌려준다 (레코드 번호가 시트의 행 번호와 일치하도록).
/// Unity API에 의존하지 않는다.
/// </summary>
public static class DialogueCsvParser
{
    public static List<List<string>> Parse(string text)
    {
        var rows = new List<List<string>>();
        if (string.IsNullOrEmpty(text)) return rows;

        if (text[0] == '\uFEFF') text = text.Substring(1);

        var row = new List<string>();
        var field = new StringBuilder();
        bool inQuotes = false;
        int i = 0;

        while (i < text.Length)
        {
            char c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"'); // 이스케이프된 따옴표
                        i += 2;
                        continue;
                    }

                    inQuotes = false;
                    i++;
                    continue;
                }

                field.Append(c);
                i++;
                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    i++;
                    break;

                case ',':
                    row.Add(field.ToString());
                    field.Length = 0;
                    i++;
                    break;

                case '\r':
                case '\n':
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++; // \r\n 은 한 번만 줄바꿈
                    row.Add(field.ToString());
                    field.Length = 0;
                    rows.Add(row);
                    row = new List<string>();
                    i++;
                    break;

                default:
                    field.Append(c);
                    i++;
                    break;
            }
        }

        // 마지막 줄이 줄바꿈 없이 끝난 경우
        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        return rows;
    }
}