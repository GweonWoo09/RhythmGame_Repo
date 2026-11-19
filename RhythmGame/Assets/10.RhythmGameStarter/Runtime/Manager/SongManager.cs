using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace RhythmGameStarter
{
    [HelpURL("https://bennykok.gitbook.io/rhythm-game-starter/hierarchy-overview/song-manager")]
    [RequireComponent(typeof(TrackManager))]
    [DefaultExecutionOrder(-100)] // [수정] 노트 판정(NoteArea)보다 먼저 곡 시간을 갱신해야 같은 프레임의 시간으로 판정된다
    public class SongManager : MonoBehaviour
    {
        [Comment("Responsible for song control, handling song related events.")]
        public AudioSource audioSource;

        [Title("Properties", 0)]
        [Space]
        public bool playOnAwake = true;
        public SongItem defaultSong;
        public float delay;
        public bool looping;

        [Tooltip("Automatically handle play/pause when timescale set to 0, or back to 1")]
        public bool autoTimeScalePause;

        [Title("Display", 0)]
        public bool progressAsPercentage = true;
        public bool inverseProgressFill = false;

        [HideInInspector] public float secPerBeat;
        [HideInInspector] public float songPosition;
        [NonSerialized] public float timingOffset; // [수정] 싱크 보정(초). 양수면 노트와 판정이 늦어진다
        [HideInInspector] public IEnumerable<SongItem.MidiNote> currnetNotes;

        [Title("Events", 0)]
        [CollapsedEvent("Triggered every frame when a song progress")] public FloatEvent onSongProgress;
        [CollapsedEvent("Triggered every frame when a song progress with a float between [0,1] useful for Image fill")] public FloatEvent onSongProgressFill;
        [CollapsedEvent("Triggered every frame the song progress with a string variable of the progress display")] public StringEvent onSongProgressDisplay;
        [CollapsedEvent("Triggered when the song play, after the delay wait time")] public UnityEvent onSongStart;
        [CollapsedEvent("Triggered when the song play, before the delay wait time")] public UnityEvent onSongStartPlay;
        [CollapsedEvent("Triggered when the song finished")] public UnityEvent onSongFinished;


        #region RUNTIME_FIELD

        [NonSerialized] public bool songPaused;
        [NonSerialized] public SongItem currentSongItem;
        [NonSerialized] public ComboSystem comboSystem;
        [NonSerialized] public TrackManager trackManager;

        private bool songHasStarted;
        public bool SongHasStarted => songHasStarted; // [수정] 일시정지 컨트롤러에서 사용
        private bool songStartEventInvoked;
        private double dspStartTime;
        private double dspPausedTime;
        private double accumulatedPauseTime;
        private float audioPosition; // [수정] 보정 전 실제 오디오 재생 위치 (오디오 예약/곡 종료에 사용)

        // [수정] 부드러운 dspTime 계산용
        private double lastDspTime = -1;
        private double lastDspRealtime;
        private double smoothDspTime;

        private int lastProgressValue = -1; // [수정] 진행도 글자가 바뀔 때만 갱신하기 위함

        #endregion

        private void Awake()
        {
            trackManager = GetComponent<TrackManager>();
            comboSystem = GetComponent<ComboSystem>();

            trackManager.Init(this);
        }

        private void Start()
        {
            if (playOnAwake && defaultSong)
            {
                PlaySong(defaultSong);
            }
        }

        public void PlaySong()
        {
            if (defaultSong)
                PlaySong(defaultSong);
            else
                Debug.LogWarning("Default song is not set!");
        }

        public void PlaySongSelected(SongItem songItem)
        {
            PlaySong(songItem);
        }

        public void SetDefaultSong(SongItem songItem)
        {
            defaultSong = songItem;
        }

        public void PlaySong(SongItem songItem, double specificStartTime = 0)
        {
            currentSongItem = songItem;
            songPaused = false;
            songHasStarted = true;
            accumulatedPauseTime = 0;
            dspPausedTime = 0;
            songPosition = -1;
            audioPosition = -1; // [수정]
            lastProgressValue = -1; // [수정]

            if (audioSource) audioSource.clip = songItem.clip;
            if (songItem.customMidiMapping || songItem.customPrefabMapping)
                trackManager.OverrideMapping(songItem.customMidiMapping, songItem.customPrefabMapping);
            else
                trackManager.ResetMappingOverride();

            // songItem.ResetNotesState();
            currnetNotes = songItem.GetNotes();
            secPerBeat = 60f / songItem.bpm;

            //Starting the audio play back
            dspStartTime = AudioSettings.dspTime;
            if (audioSource)
            {
                audioSource.pitch = songItem.speedModifier;

                audioSource.PlayScheduled(AudioSettings.dspTime + delay);
                audioSource.time = (float)specificStartTime;
                dspStartTime -= specificStartTime;
            }

            trackManager.SetupForNewSong();

            onSongStartPlay.Invoke();
        }

        public void PauseSong()
        {
            if (songPaused) return;

            songPaused = true;
            // [수정] 시작 대기(delay) 중이면 예약 재생을 멈추고, 재개할 때 남은 시간만큼 다시 예약한다
            if (audioSource)
            {
                if (audioPosition < 0) audioSource.Stop();
                else audioSource.Pause();
            }

            dspPausedTime = AudioSettings.dspTime;
        }

        public void ResumeSong()
        {
            if (!songHasStarted)
            {
                PlaySong();
                return;
            }
            if (!songPaused) return;

            songPaused = false;
            // [수정]
            if (audioSource)
            {
                if (audioPosition < 0) audioSource.PlayScheduled(AudioSettings.dspTime - audioPosition / audioSource.pitch);
                else audioSource.Play();
            }

            accumulatedPauseTime += AudioSettings.dspTime - dspPausedTime;
        }

        public void StopSong(bool dontInvokeEvent = false)
        {
            if (audioSource) audioSource.Stop();
            songHasStarted = false;
            songStartEventInvoked = false;

            if (!dontInvokeEvent)
                onSongFinished.Invoke();

            trackManager.ClearAllTracks();
        }

        // [수정] dspTime은 오디오 버퍼 단위(약 10~20ms)로 끊겨서 올라간다. 그대로 쓰면 노트가 덜컥거리므로
        // 그 사이를 실제 흐른 시간으로 이어 준다. 뒤로 가지는 않게 막는다.
        private double GetSmoothDspTime()
        {
            double dsp = AudioSettings.dspTime;
            double now = Time.realtimeSinceStartupAsDouble;
            if (dsp != lastDspTime)
            {
                lastDspTime = dsp;
                lastDspRealtime = now;
            }

            // [수정] 오디오 시계가 멈춘 동안(장치 변경, 창 전환 등) 실제 시간을 끝없이 더하지 않게 50ms(버퍼 2개 정도)까지만 이어 준다
            double smooth = lastDspTime + Math.Min(now - lastDspRealtime, 0.05);
            if (smooth < smoothDspTime) smooth = smoothDspTime;
            smoothDspTime = smooth;
            return smooth;
        }

        void Update()
        {
            if (!songStartEventInvoked && songHasStarted && songPosition >= 0)
            {
                songStartEventInvoked = true;
                onSongStart.Invoke();
            }

            // If we need to automatically handle play/pause according to the timescale;
            if (songHasStarted && songStartEventInvoked && autoTimeScalePause)
            {
                if (!songPaused && Time.timeScale == 0)
                {
                    PauseSong();
                }
                else if (songPaused && Time.timeScale == 1)
                {
                    ResumeSong();
                }
            }

            //Sync the tracks position with the audio
            if (!songPaused && songHasStarted)
            {
                audioPosition = (float)(GetSmoothDspTime() - dspStartTime - delay - accumulatedPauseTime) * audioSource.pitch; // [수정]
                songPosition = audioPosition - timingOffset; // [수정] 노트 위치와 판정 모두 이 값을 쓴다

                trackManager.UpdateTrack(songPosition, secPerBeat);

                onSongProgress.Invoke(songPosition);

                // [수정] 음악이 없는 테스트 채보는 진행도 표시를 건너뛴다 (매 프레임 에러 방지)
                if (currentSongItem.clip)
                {
                    if (inverseProgressFill)
                        onSongProgressFill.Invoke(1 - (songPosition / currentSongItem.clip.length));
                    else
                        onSongProgressFill.Invoke(songPosition / currentSongItem.clip.length);
                }

                // [수정] 매 프레임 새 글자를 만들지 않고, 표시 값(% 또는 초)이 바뀔 때만 갱신한다
                if (songPosition >= 0 && currentSongItem.clip)
                {
                    int value = progressAsPercentage
                        ? (int)Math.Truncate(songPosition / currentSongItem.clip.length * 100)
                        : (int)songPosition;

                    if (value != lastProgressValue)
                    {
                        lastProgressValue = value;
                        if (progressAsPercentage)
                            onSongProgressDisplay.Invoke(value + "%");
                        else
                            onSongProgressDisplay.Invoke(new DateTime((long)value * TimeSpan.TicksPerSecond).ToString("mm:ss"));
                    }
                }
            }

            if (songHasStarted && currentSongItem.clip && audioPosition >= currentSongItem.clip.length) // [수정]
            {
                songHasStarted = false;
                songStartEventInvoked = false;
                onSongFinished.Invoke();

                trackManager.ClearAllTracks();

                //If its looping, we replay the current song
                if (looping)
                    PlaySong(currentSongItem);
            }
        }
    }
}