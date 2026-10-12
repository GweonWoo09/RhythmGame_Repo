using System;
using UnityEngine;

namespace RhythmGameStarter
{
    [HelpURL("https://bennykok.gitbook.io/rhythm-game-starter/note-prefab")]
    public class Note : MonoBehaviour
    {
        public int score;
        public NoteAction action;
        public SwipeDirection swipeDirection;
        public float swipeThreshold = 1f;
        public AnimationClip killAnim;
        public bool noTapEffect;
        public AudioClip hitSound;
        public bool noHitSound;
        public SpriteRenderer applyNoteLenghtTarget;
        public float noteLengthSizeOffset;

        #region RUNTIME_FIELD

        [NonSerialized] public int noteType;
        [NonSerialized] public bool inUse;
        [NonSerialized] public bool inInteraction;
        [NonSerialized] public bool alreadyMissed;
        // [수정] 판정이 끝난 노트인지 (성공/미스 모두). 같은 노트를 두 번 판정하지 않기 위함
        [NonSerialized] public bool judged;
        [NonSerialized] public float noteLength;
        [NonSerialized] public float noteTime;

        [NonSerialized] public SongManager songManager;

        [NonSerialized] public Track parentTrack;

        private BoxCollider target_collider;
        private BoxCollider m_collider;
        private Vector3[] initValues = new Vector3[5];

        #endregion


        public enum NoteAction
        {
            Tap, LongPress, Swipe
        }

        public enum SwipeDirection
        {
            Up, Down, Left, Right
        }

        void Awake()
        {
            if (applyNoteLenghtTarget)
                target_collider = applyNoteLenghtTarget.GetComponent<BoxCollider>();

            m_collider = GetComponent<BoxCollider>();
        }

        public void InitNoteLength(float length)
        {
            noteLength = length;
            if (applyNoteLenghtTarget)
            {
                initValues[0] = applyNoteLenghtTarget.size;
                initValues[1] = target_collider.center;
                initValues[2] = target_collider.size;
                initValues[3] = m_collider.center;
                initValues[4] = m_collider.size;

                //We set the size of the note
                var size = applyNoteLenghtTarget.size;
                size.y = length / songManager.secPerBeat * songManager.trackManager.beatSize;
                applyNoteLenghtTarget.size = size;

                //Update target collider
                var col_center = target_collider.center;
                var col_size = target_collider.size;

                col_center.y = size.y / 2 - noteLengthSizeOffset / 2;
                target_collider.center = col_center;

                col_size.y = size.y - noteLengthSizeOffset;
                target_collider.size = col_size;

                //Update self collider
                var col_center2 = m_collider.center;
                var col_size2 = m_collider.size;

                col_center2.y = size.y / 2 - col_size2.y / 2;
                m_collider.center = col_center2;

                col_size2.y = size.y;
                m_collider.size = col_size2;
            }
        }

        public void ResetForPool()
        {
            inUse = false;
            inInteraction = false;
            alreadyMissed = false;
            judged = false; // [수정]
            parentTrack = null;
            ResetNoteLength();
        }

        public void ResetNoteLength()
        {
            if (applyNoteLenghtTarget)
            {
                applyNoteLenghtTarget.size = initValues[0];
                target_collider.center = initValues[1];
                target_collider.size = initValues[2];
                m_collider.center = initValues[3];
                m_collider.size = initValues[4];

                applyNoteLenghtTarget.GetComponent<LongNoteDetecter>().exitedLineArea = false;
            }
        }

        public LongNoteDetecter GetNoteDetecter()
        {
            if (applyNoteLenghtTarget)
            {
                return applyNoteLenghtTarget.GetComponent<LongNoteDetecter>();
            }
            return null;
        }

        void OnTriggerExit(Collider col)
        {
            if (col.tag == "LineArea")
            {
                // [수정] 미스 판정은 NoteArea.CheckMissedNotes()에서 시간(ms) 기준으로 처리.
                // 충돌체 기준 미스는 판정 범위와 맞지 않아서 사용하지 않음
            }
        }
    }
}