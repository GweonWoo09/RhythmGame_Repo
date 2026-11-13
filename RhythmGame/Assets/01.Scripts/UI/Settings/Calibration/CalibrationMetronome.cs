using UnityEngine;

/// <summary>
/// 일정한 간격으로 '틱' 소리를 낸다. 박자 시간은 AudioSettings.dspTime 기준이다.
/// 틱 소리는 코드로 만들어서 따로 사운드 파일이 필요 없다.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class CalibrationMetronome : MonoBehaviour
{
    [SerializeField] private float bpm = 120f;
    [SerializeField] private float startDelay = 1f;   // 시작 후 첫 박까지 (초)
    [SerializeField, Range(0f, 1f)] private float volume = 0.8f;

    private AudioSource source;

    /// <summary>첫 박이 울리는 시간 (dspTime)</summary>
    public double FirstBeatTime { get; private set; }

    /// <summary>박 간격 (초)</summary>
    public double Interval { get; private set; }

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.ignoreListenerPause = true; // 인게임 일시정지 중에도 소리가 나게
    }

    public void Play()
    {
        int rate = AudioSettings.outputSampleRate;
        int samples = Mathf.RoundToInt(rate * 60f / bpm);
        Interval = (double)samples / rate; // 샘플 단위로 맞춘 실제 간격

        if (source.clip != null) Destroy(source.clip); // 코드로 만든 소리는 직접 지워야 메모리가 쌓이지 않는다
        source.clip = CreateClickClip(samples, rate);
        source.volume = volume;

        FirstBeatTime = AudioSettings.dspTime + startDelay;
        source.PlayScheduled(FirstBeatTime);
    }

    public void Stop() => source.Stop();

    private void OnDestroy()
    {
        if (source != null && source.clip != null) Destroy(source.clip);
    }

    // 맨 앞에 짧은 '틱'이 있고 나머지는 무음인 한 박 길이의 소리
    private static AudioClip CreateClickClip(int samples, int rate)
    {
        var data = new float[samples];
        int clickLength = Mathf.Min(samples, rate / 50); // 20ms
        for (int i = 0; i < clickLength; i++)
        {
            float t = (float)i / rate;
            float fade = 1f - (float)i / clickLength;
            data[i] = Mathf.Sin(2f * Mathf.PI * 1500f * t) * fade;
        }

        var clip = AudioClip.Create("CalibrationClick", samples, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
