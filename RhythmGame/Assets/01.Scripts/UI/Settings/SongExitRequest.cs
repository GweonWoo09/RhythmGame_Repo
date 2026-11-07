using System;

/// <summary>
/// 설정창에서 '곡 나가기'를 눌렀을 때 실제로 나가는 처리를 인게임 쪽에 전달한다.
/// 처리하는 쪽(인게임)이 등록되어 있을 때만 설정창에 '곡 나가기' 줄이 보인다.
/// </summary>
public static class SongExitRequest
{
    private static Action handler;

    public static bool IsAvailable => handler != null;

    public static void Register(Action exitHandler) => handler = exitHandler;

    public static void Unregister(Action exitHandler)
    {
        if (handler == exitHandler) handler = null;
    }

    public static void Invoke() => handler?.Invoke();
}
