using System;
using System.Collections.Generic;

/// <summary>
/// 박자와 누른 시간의 차이를 모아 보정값을 계산한다. 소리/화면과 상관없는 계산만 담당.
/// 차이가 +면 박자보다 늦게 누른 것이다.
/// </summary>
public class OffsetCalibrator
{
    private const float TrimRatio = 0.2f; // 가장 빠른/늦은 20%는 실수로 보고 뺀다

    private readonly List<double> diffs = new List<double>();

    public int Count => diffs.Count;

    public void Clear() => diffs.Clear();

    /// <summary>누른 시간을 기록하고, 가장 가까운 박자와의 차이(ms)를 돌려준다.</summary>
    public int Add(double pressTime, double firstBeatTime, double interval)
    {
        double beats = (pressTime - firstBeatTime) / interval;
        double diff = (beats - Math.Round(beats)) * interval;
        diffs.Add(diff);
        return ToMs(diff);
    }

    /// <summary>튀는 값을 뺀 평균 차이(ms)</summary>
    public int AverageMs
    {
        get
        {
            if (diffs.Count == 0) return 0;

            var sorted = new List<double>(diffs);
            sorted.Sort();
            int trim = diffs.Count >= 5 ? (int)(diffs.Count * TrimRatio) : 0;

            double sum = 0;
            for (int i = trim; i < sorted.Count - trim; i++) sum += sorted[i];
            return ToMs(sum / (sorted.Count - trim * 2));
        }
    }

    private static int ToMs(double seconds) => (int)Math.Round(seconds * 1000.0);
}
