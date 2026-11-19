using UnityEngine;

namespace RhythmGameStarter
{
    /// <summary>
    /// [수정] 동타 보정. 같은 시간의 노트(동타)를 여러 키로 칠 때, 첫 키 뒤 짧은 시간 안에 누른 키는
    /// 첫 키 타이밍과 비교해 노트에 더 가까운 쪽으로 판정한다. 입력은 프레임 단위로 읽혀서, 거의 동시에 눌러도
    /// 다른 프레임에 들어가면 1프레임(약 17ms)만큼 늦게 판정되기 때문이다.
    /// </summary>
    public static class ChordInputAssist
    {
        /// <summary>첫 키 뒤 이 시간(초) 안에 누르면 같은 동타로 본다</summary>
        public const float Window = 0.035f;

        private static float chordNoteTime = float.MinValue;
        private static float firstInputTime = float.MinValue;

        /// <summary>판정에 쓸 입력 시간을 돌려준다</summary>
        public static float Resolve(float noteTime, float inputTime)
        {
            bool sameChord = Mathf.Abs(noteTime - chordNoteTime) < 0.001f
                             && inputTime >= firstInputTime
                             && inputTime - firstInputTime <= Window;
            // [수정] 첫 키 시간으로만 바꾸면 지금 키가 더 정확해도 판정이 나빠지므로, 노트에 더 가까운 쪽을 쓴다
            if (sameChord)
                return Mathf.Abs(firstInputTime - noteTime) < Mathf.Abs(inputTime - noteTime) ? firstInputTime : inputTime;

            chordNoteTime = noteTime;
            firstInputTime = inputTime;
            return inputTime;
        }
    }
}
