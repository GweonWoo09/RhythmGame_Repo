namespace RhythmGameStarter
{
    /// <summary>
    /// [수정] 롱노트를 누르고 있는 동안 일정 간격마다 콤보를 주기 위한 시간 계산만 담당한다.
    /// 틱 시간은 노트 시작 시간 기준이라 프레임이 끊겨도 개수가 달라지지 않는다.
    /// </summary>
    public class LongNoteTicker
    {
        private float nextTickTime;
        private float endTime;
        private float interval;

        public void Begin(float noteTime, float noteLength, float tickInterval)
        {
            interval = tickInterval;
            endTime = noteTime + noteLength;
            nextTickTime = noteTime + interval;
        }

        /// <summary>songTime까지 지나간 틱 수 (노트 끝을 넘는 틱은 세지 않는다)</summary>
        public int ConsumeTicks(float songTime)
        {
            int count = 0;
            while (nextTickTime <= songTime && nextTickTime <= endTime + 0.001f)
            {
                count++;
                nextTickTime += interval;
            }
            return count;
        }

        /// <summary>노트 끝까지 남은 틱 수 (판정 범위 안에서 조금 일찍 뗀 경우 남은 틱을 준다)</summary>
        public int ConsumeRemaining() => ConsumeTicks(endTime);
    }
}
