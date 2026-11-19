using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.Events;
using System;

namespace RhythmGameStarter
{
    public class NoteArea : MonoBehaviour
    {
        [Title("Events")] [CollapsedEvent] public UnityEvent OnInteractionBegin;
        [CollapsedEvent] public UnityEvent OnInteractionEnd;

        [Title("Effect")] public GameObject tapEffectCustomTarget;
        public NoteEffect sustainEffect;

        // [수정] 롱노트를 누르고 있는 동안 한 박자에 몇 번 콤보를 줄지
        [Title("Long Note")] public int longNoteTicksPerBeat = 2;
        private readonly LongNoteTicker longNoteTicker = new LongNoteTicker();

        private TapEffectPool effect;

        private Track track;

        private Camera _camera;

        private SongManager songManager;

        // [수정] 판정 범위(ms)를 가져오기 위함
        private StatsSystem statsSystem;

        // [수정] 롱노트를 누른 순간의 곡 시간 (롱노트 판정은 누른 타이밍 기준)
        private float longNoteDownSongTime;

        // [수정] 미스 기준 시간(초)
        private float MissWindow => statsSystem.MissWindowMs / 1000f;

        void Start()
        {
            track = GetComponentInParent<Track>();
            effect = GetComponentInParent<TapEffectPool>();
            songManager = GetComponentInParent<SongManager>();
            statsSystem = songManager.GetComponent<StatsSystem>(); // [수정]
            _camera = Camera.main;
        }

        private List<Note> notesInRange = new List<Note>();

        private Note currentNote;

        private int currentFingerID = -1;

        private Vector2 touchDownPosition;

        private Vector3 touchDownNotePosition;

        private LongNoteDetecter longNoteDetecter;

        //For recording mode
        private float touchDownTime;
        private float touchDownTimeInSong;
        private bool hasTriggeredSomething;
        private bool hasPossibleDirection;
        private Note.SwipeDirection possibleDirection;


        [NonSerialized] public BaseInputHandler keyboardInputHandler;

        [NonSerialized] public BaseTouchHandler touchInputHandler;

        void Update()
        {
            if (keyboardInputHandler)
            {
                if (keyboardInputHandler.GetTrackActionKeyDown(track, track.transform.GetSiblingIndex()))
                {
                    var fakeTouch = new TouchWrapper();
                    fakeTouch.phase = TouchPhase.Began;
                    TriggerNote(fakeTouch);
                }
                else if (keyboardInputHandler.GetTrackActionKeyUp(track, track.transform.GetSiblingIndex()))
                {
                    if (currentNote != null)
                        currentNote.inInteraction = false;

                    OnInteractionEnd.Invoke();
                }
            }

            TouchWrapper touch = default(TouchWrapper);

            if (currentFingerID != -1)
            {
                touch = touchInputHandler.GetTouchById(currentFingerID);

                if (touch.phase == TouchPhase.Canceled || touch.phase == TouchPhase.Ended)
                {
                    if (currentNote != null)
                        currentNote.inInteraction = false;

                    OnInteractionEnd.Invoke();
                }
            }

            //// Handling Recording mode
            if (NoteRecorder.INSTANCE && NoteRecorder.INSTANCE.IsRecording)
            {
                Note.NoteAction action = Note.NoteAction.Tap;
                Note.SwipeDirection direction = Note.SwipeDirection.Left;

                if (hasTriggeredSomething && !hasPossibleDirection)
                {
                    var diff = _camera.ScreenToWorldPoint(touchDownPosition) -
                               _camera.ScreenToWorldPoint(touch.position);
                    if ((keyboardInputHandler && keyboardInputHandler.GetTrackDirectionKey(Note.SwipeDirection.Left)) ||
                        diff.x >= NoteRecorder.INSTANCE.swipeThreshold)
                    {
                        possibleDirection = Note.SwipeDirection.Left;
                        hasPossibleDirection = true;
                    }
                    else if ((keyboardInputHandler &&
                              keyboardInputHandler.GetTrackDirectionKey(Note.SwipeDirection.Right)) ||
                             diff.x <= -NoteRecorder.INSTANCE.swipeThreshold)
                    {
                        possibleDirection = Note.SwipeDirection.Right;
                        hasPossibleDirection = true;
                    }
                    else if ((keyboardInputHandler &&
                              keyboardInputHandler.GetTrackDirectionKey(Note.SwipeDirection.Up)) ||
                             diff.y <= -NoteRecorder.INSTANCE.swipeThreshold)
                    {
                        possibleDirection = Note.SwipeDirection.Up;
                        hasPossibleDirection = true;
                    }
                    else if ((keyboardInputHandler &&
                              keyboardInputHandler.GetTrackDirectionKey(Note.SwipeDirection.Down)) ||
                             diff.y >= NoteRecorder.INSTANCE.swipeThreshold)
                    {
                        possibleDirection = Note.SwipeDirection.Down;
                        hasPossibleDirection = true;
                    }
                }

                if ((currentFingerID != -1 &&
                     (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)) ||
                    (keyboardInputHandler &&
                     keyboardInputHandler.GetTrackActionKeyUp(track, track.transform.GetSiblingIndex())))
                {
                    //This could be a tap, long press, or even swipe
                    var duration = songManager.songPosition - touchDownTimeInSong;

                    if (hasPossibleDirection)
                    {
                        action = Note.NoteAction.Swipe;
                        direction = possibleDirection;
                    }

                    //The duration is long enough
                    if (duration > NoteRecorder.INSTANCE.longNoteThershold)
                        action = Note.NoteAction.LongPress;

                    hasTriggeredSomething = false;
                    hasPossibleDirection = false;
                    currentFingerID = -1;

                    NoteRecorder.INSTANCE.RecordNote(new NoteRecorder.NoteEventData()
                    {
                        track = track,
                        action = action,
                        duration = duration,
                        startTime = touchDownTimeInSong,
                        swipeDirection = direction,
                    });
                }

                return;
            }

            CheckMissedNotes(); // [수정] 시간이 지난 노트를 미스 처리

            if (songManager.songPaused) return; // [수정] 일시정지 중에는 롱노트를 판정하지 않는다 (재개 후 키 상태로 판정)

            if (currentNote || (currentNote && keyboardInputHandler))
            {
                switch (currentNote.action)
                {
                    case Note.NoteAction.LongPress:
                        if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary ||
                            (keyboardInputHandler &&
                             keyboardInputHandler.GetTrackActionKey(track, track.transform.GetSiblingIndex())))
                        {
                            // [수정] 누르고 있는 동안 박자마다 콤보
                            AddLongNoteTicks(longNoteTicker.ConsumeTicks(songManager.songPosition));

                            // [수정] 충돌체 대신 시간으로 끝을 판단 (꼬리 시간이 지나면 성공)
                            if (longNoteDetecter && IsLongNoteFinished(currentNote, 0f))
                            {
                                longNoteDetecter.OnTouchUp();
                                longNoteDetecter = null;
                                KillNote(currentNote); // [수정] 끝까지 누른 롱노트는 사라진다
                                currentNote = null;

                                sustainEffect.StopEffect();
                            }
                        }
                        // [수정] '뗀 순간(KeyUp)'만 보면, 일시정지 중에 뗀 키는 영영 처리되지 않는다. 안 누르고 있으면 뗀 것으로 본다
                        else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled ||
                                 (keyboardInputHandler &&
                                  !keyboardInputHandler.GetTrackActionKey(track, track.transform.GetSiblingIndex())))
                        {
                            // [수정] 꼬리 시간보다 판정 범위(MissWindow)만큼 일찍 떼도 성공으로 인정 (남은 틱도 준다)
                            bool success = IsLongNoteFinished(currentNote, MissWindow);
                            if (success)
                                AddLongNoteTicks(longNoteTicker.ConsumeRemaining());
                            else
                                songManager.comboSystem.BreakCombo(); // [수정] 일찍 떼면 미스 1회, 남은 틱은 없음

                            longNoteDetecter.OnTouchUp();
                            longNoteDetecter = null;
                            if (success) KillNote(currentNote); // [수정] 성공한 롱노트는 사라진다 (실패하면 그대로 내려감)
                            currentNote = null;

                            sustainEffect.StopEffect();
                        }

                        break;
                    case Note.NoteAction.Swipe:
                        if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Ended || keyboardInputHandler)
                        {
                            if (currentNote.alreadyMissed)
                            {
                                currentNote = null;
                                return;
                            }

                            bool addCombo = false;
                            if (keyboardInputHandler)
                            {
                                addCombo = keyboardInputHandler.GetTrackDirectionKey(currentNote.swipeDirection);
                            }

                            if (!addCombo && currentFingerID != -1 && touch.phase != TouchPhase.Began)
                            {
                                var diff = _camera.ScreenToWorldPoint(touchDownPosition) -
                                           _camera.ScreenToWorldPoint(touch.position);
                                // var diff = touchDownPosition - touch.position;
                                switch (currentNote.swipeDirection)
                                {
                                    case Note.SwipeDirection.Up:
                                        addCombo = diff.y <= -currentNote.swipeThreshold;
                                        break;
                                    case Note.SwipeDirection.Down:
                                        addCombo = diff.y >= currentNote.swipeThreshold;
                                        break;
                                    case Note.SwipeDirection.Left:
                                        addCombo = diff.x >= currentNote.swipeThreshold;
                                        break;
                                    case Note.SwipeDirection.Right:
                                        addCombo = diff.x <= -currentNote.swipeThreshold;
                                        break;
                                }
                                // print(currentNote.swipeDirection + " " + diff.x + " " + addCombo + " " + touch.position + " " + touch.fingerId + " " + touch.phase);
                            }

                            if (addCombo)
                            {
                                PlayHitSound(currentNote);
                                AddCombo(currentNote, longNoteDownSongTime); // [수정] 누른 타이밍으로 판정

                                EmitEffectIfNeeded();

                                KillNote(currentNote);
                                currentNote = null;
                            }
                        }

                        break;
                }
            }
        }

        public void EmitEffectIfNeeded()
        {
            if (effect && !currentNote.noTapEffect)
                effect.EmitEffects(tapEffectCustomTarget != null
                    ? tapEffectCustomTarget.transform
                    : track.lineArea); // [수정] 판정(완벽/좋음)과 상관없이 판정선 위치에서 이펙트
        }

        public void ResetNoteArea()
        {
            currentFingerID = -1;
            notesInRange.Clear();
            if (sustainEffect) sustainEffect.StopEffect(); // [수정] 곡을 다시 시작하거나 나갈 때 남은 롱노트 이펙트 정리
        }

        public void TriggerNote(TouchWrapper touch)
        {
            if (songManager.songPaused)
                return;

            currentFingerID = touch.fingerId;
            if (touch.phase == TouchPhase.Began)
            {
                OnInteractionBegin.Invoke();
            }

            var note = FindJudgeableNote(); // [수정] 충돌체 대신 시간 기준으로 판정할 노트 선택

            //// Handling Recording mode
            if (NoteRecorder.INSTANCE && NoteRecorder.INSTANCE.IsRecording)
            {
                if (touch.phase != TouchPhase.Began) return;
                hasTriggeredSomething = true;
                touchDownPosition = touch.position;
                // touchDownTime = Time.time;
                touchDownTimeInSong = songManager.songPosition;
                return;
            }

            if (note)
            {
                switch (note.action)
                {
                    case Note.NoteAction.Tap:
                        if (touch.phase != TouchPhase.Began) break;

                        currentNote = note;
                        
                        EmitEffectIfNeeded();

                        PlayHitSound(note);
                        AddCombo(note, ChordInputAssist.Resolve(note.noteTime, songManager.songPosition)); // [수정] 누른 순간의 곡 시간으로 판정 (동타 보정)

                        KillNote(note);
                        currentNote = null; // [수정] 친 노트를 계속 들고 있으면, 같은 노트 오브젝트가 재사용될 때 미스 검사에서 빠진다

                        break;
                    case Note.NoteAction.LongPress:
                        if (touch.phase != TouchPhase.Began) break;

                        currentNote = note;
                        
                        EmitEffectIfNeeded();

                        note.inInteraction = true;

                        touchDownNotePosition = note.transform.position;
                        longNoteDownSongTime = ChordInputAssist.Resolve(note.noteTime, songManager.songPosition); // [수정] 동타 보정

                        longNoteDetecter = note.GetNoteDetecter();
                        longNoteDetecter.OnTouchDown();

                        PlayHitSound(note);

                        // [수정] 누르는 순간 머리 판정 + 콤보, 이후 박자마다 콤보
                        AddCombo(note, longNoteDownSongTime);
                        longNoteTicker.Begin(note.noteTime, note.noteLength,
                            songManager.secPerBeat / Mathf.Max(1, longNoteTicksPerBeat));

                        notesInRange.Remove(note);

                        sustainEffect.StartEffect(null);

                        break;
                    case Note.NoteAction.Swipe:
                        if (touch.phase != TouchPhase.Began) break;

                        notesInRange.Remove(note);

                        touchDownPosition = touch.position;
                        currentNote = note;
                        note.inInteraction = true;

                        touchDownNotePosition = note.transform.position;
                        longNoteDownSongTime = songManager.songPosition; // [수정]
                        break;
                }
            }
        }

        private void PlayHitSound(Note note)
        {
            if (!note.noHitSound)
            {
                track.trackHitAudio.clip = note.hitSound;
                track.trackHitAudio.Play();
            }
        }

        // [수정] 거리 대신 시간 차이(ms)로 판정
        private void AddCombo(Note note, float inputSongTime)
        {
            note.judged = true;
            var diffMs = Mathf.Abs(inputSongTime - note.noteTime) * 1000f;
            songManager.comboSystem.AddCombo(1, diffMs, note.score);
            songManager.trackManager.onNoteTriggered.Invoke(note);
        }

        // [수정] 롱노트를 누르고 있는 동안의 콤보 (판정은 항상 가장 좋은 단계)
        private void AddLongNoteTicks(int count)
        {
            for (int i = 0; i < count; i++)
                songManager.comboSystem.AddCombo(1, 0f, currentNote.score);
        }

        // [수정] 롱노트 꼬리 시간이 지났는지 (tolerance초만큼 일찍 떼는 것까지 허용)
        private bool IsLongNoteFinished(Note note, float tolerance)
        {
            if (!note) return false;
            return songManager.songPosition >= note.noteTime + note.noteLength - tolerance;
        }

        // [수정] 이 레인에서 판정 범위 안에 있는, 아직 판정 안 된 가장 이른 노트
        // 범위보다 일찍 누르면 null (입력 무시)
        private Note FindJudgeableNote()
        {
            Note earliest = null;
            foreach (var note in track.runtimeNote)
            {
                if (!note || !note.inUse || note.judged || note.inInteraction) continue;

                var diff = note.noteTime - songManager.songPosition;
                if (diff > MissWindow || diff < -MissWindow) continue;

                if (earliest == null || note.noteTime < earliest.noteTime)
                    earliest = note;
            }
            return earliest;
        }

        // [수정] 판정 범위를 넘어 지나간 노트를 미스 처리
        private void CheckMissedNotes()
        {
            if (songManager.songPaused) return;

            foreach (var note in track.runtimeNote)
            {
                if (!note || !note.inUse || note.judged || note.inInteraction) continue;
                if (note == currentNote) continue; // 처리 중인 롱노트는 뗄 때 판정

                if (songManager.songPosition - note.noteTime > MissWindow)
                {
                    note.judged = true;
                    note.alreadyMissed = true;
                    songManager.comboSystem.BreakCombo();
                }
            }
        }

        private void KillNoteAnimation(Note note)
        {
            if (note.killAnim)
            {
                var anim = note.GetComponent<Animation>();
                anim.Play(note.killAnim.name, PlayMode.StopAll);
                note.transform.SetParent(null);

                if (songManager.trackManager.useNotePool)
                    StartCoroutine(DelayResetNote(note.gameObject, note.killAnim.length));
                else
                    Destroy(note.gameObject, note.killAnim.length);
            }
            else
            {
                if (songManager.trackManager.useNotePool)
                    songManager.trackManager.ResetNoteToPool(note.gameObject);
                else
                    Destroy(note.gameObject);
            }
        }

        IEnumerator DelayResetNote(GameObject note, float delay)
        {
            yield return new WaitForSeconds(delay);
            songManager.trackManager.ResetNoteToPool(note);
        }

        private void KillNote(Note note)
        {
            KillNoteAnimation(note);
            notesInRange.Remove(note);
        }

        void OnTriggerEnter(Collider col)
        {
            if (col.tag == "Note")
            {
                notesInRange.Add(col.GetComponent<Note>());
            }
        }

        void OnTriggerExit(Collider col)
        {
            if (col.tag == "Note")
            {
                var outOfRange = col.GetComponent<Note>();
                notesInRange.Remove(outOfRange);

                // [수정] 누르고 있는 롱노트는 머리가 판정선을 지나가도 놓지 않는다 (끝은 시간으로 판단)
                // 여기서 놓아버리면 뗄 때의 판정과 이펙트 정지가 실행되지 않아 이펙트가 계속 남는다
                if (currentNote != null && currentNote == outOfRange && currentNote.action != Note.NoteAction.LongPress)
                {
                    currentNote = null;
                }
            }
        }
    }
}