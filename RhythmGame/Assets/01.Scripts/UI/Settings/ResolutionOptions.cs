using UnityEngine;

/// <summary>
/// 설정창에서 고를 수 있는 해상도 목록. 해상도를 추가할 때는 이 배열 끝에 붙인다.
/// (저장값이 번호라서 중간에 넣으면 기존 설정이 다른 해상도로 바뀐다)
/// </summary>
public static class ResolutionOptions
{
    private static readonly Vector2Int[] sizes =
    {
        new Vector2Int(1280, 720),
        new Vector2Int(1600, 900),
        new Vector2Int(1920, 1080),
        new Vector2Int(2560, 1440),
        new Vector2Int(3840, 2160),
        new Vector2Int(3440, 1440), // 21:9 울트라와이드
    };

    public const int DefaultIndex = 2; // 1920 x 1080

    public static Vector2Int Get(int index) => sizes[Mathf.Clamp(index, 0, sizes.Length - 1)];

    /// <summary>설정창에 보여줄 글자 ("1920 x 1080")</summary>
    public static string[] Labels
    {
        get
        {
            var labels = new string[sizes.Length];
            for (int i = 0; i < sizes.Length; i++) labels[i] = sizes[i].x + " x " + sizes[i].y;
            return labels;
        }
    }
}
