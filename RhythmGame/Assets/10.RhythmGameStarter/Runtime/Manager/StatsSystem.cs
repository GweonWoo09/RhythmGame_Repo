using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmGameStarter
{
    [HelpURL("https://bennykok.gitbook.io/rhythm-game-starter/hierarchy-overview/stats-system")]
    public class StatsSystem : MonoBehaviour
    {
        [Comment("Responsible for advance stats' config and events.", order = 0)]
        [Title("Hit Level Config", false, 2, order = 1)]
        // [수정] threshold 단위가 거리 -> 밀리초(ms)로 바뀜. 작은 값부터 순서대로 둘 것
        [Tooltip("판정 단계별 허용 범위(ms, 앞뒤 ±). 작은 값부터 순서대로. 가장 큰 값을 넘으면 미스")]
        public HitLevelList levels;

        [Title("Events", 2)] [CollapsedEvent] public StringEvent onComboStatusUpdate;
        [CollapsedEvent] public StringEvent onScoreUpdate;
        [CollapsedEvent] public StringEvent onMaxComboUpdate;
        [CollapsedEvent] public StringEvent onMissedUpdate;
        // [수정] 정확도(%)가 바뀔 때 호출. 예: "98.50%"
        [CollapsedEvent] public StringEvent onAccuracyUpdate = new StringEvent();

        #region RUNTIME_FIELD

        [NonSerialized] public int combo;
        [NonSerialized] public int maxCombo;
        [NonSerialized] public int missed;
        [NonSerialized] public int score;

        #endregion

        [Serializable]
        public class HitLevelList : ReorderableList<HitLevel>
        {
        }

        [Serializable]
        public class HitLevel
        {
            public string name;
            [Tooltip("허용 범위(ms). 예: 40 = 앞뒤 40ms 이내")]
            public float threshold;
            [HideInInspector] public int count;
            public float scorePrecentage = 1;
            public StringEvent onCountUpdate;
        }

        public void AddMissed(int addMissed)
        {
            missed += addMissed;
            onMissedUpdate.Invoke(missed.ToString());
            UpdateAccuracyDisplay(); // [수정]
        }

        // [수정] 미스 기준 시간(ms) = 판정 단계 중 가장 큰 범위
        public float MissWindowMs
        {
            get
            {
                float max = 0f;
                if (levels == null || levels.values == null) return max;
                foreach (var level in levels.values)
                    max = Mathf.Max(max, level.threshold);
                return max;
            }
        }

        // [수정] 정확도(0~100) = 판정별 (개수 × 점수 비율) 합 ÷ 전체 판정 수
        public float GetAccuracy()
        {
            float weighted = 0f;
            int judged = missed;
            foreach (var level in levels.values)
            {
                weighted += level.count * level.scorePrecentage;
                judged += level.count;
            }

            if (judged == 0) return 100f;
            return weighted / judged * 100f;
        }

        private void UpdateAccuracyDisplay()
        {
            onAccuracyUpdate.Invoke(GetAccuracy().ToString("F2") + "%");
        }

        // [수정] 컴포넌트를 추가하거나 Reset 할 때 기본 판정값 적용
        private void Reset()
        {
            levels = new HitLevelList { values = new List<HitLevel>() };
            ApplyDefaultLevels();
        }

        // [수정] 인스펙터 우클릭 메뉴에서 기본 판정값 적용 (기존 UI 이벤트 연결은 유지)
        [ContextMenu("판정 기본값 적용 (정확 40 / 좋음 80 / 보통 120ms)")]
        private void ApplyDefaultLevels()
        {
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(this, "Apply Default Hit Levels");
#endif
            if (levels == null) levels = new HitLevelList();
            if (levels.values == null) levels.values = new List<HitLevel>();

            SetLevel(0, "정확", 40f, 1f);
            SetLevel(1, "좋음", 80f, 0.7f);
            SetLevel(2, "보통", 120f, 0.4f);

            // 3단계를 넘는 예전 단계는 제거
            while (levels.values.Count > 3)
                levels.values.RemoveAt(levels.values.Count - 1);

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        private void SetLevel(int index, string levelName, float thresholdMs, float scoreRate)
        {
            while (levels.values.Count <= index)
                levels.values.Add(new HitLevel { onCountUpdate = new StringEvent() });

            var level = levels.values[index];
            level.name = levelName;
            level.threshold = thresholdMs;
            level.scorePrecentage = scoreRate;
        }

        void Start()
        {
            // [수정] 예전 거리 단위 값이 남아 있으면 노트를 칠 수 없으므로 경고
            if (MissWindowMs < 10f)
                Debug.LogWarning("[StatsSystem] 판정 범위가 ms 단위가 아닙니다. StatsSystem 컴포넌트 우클릭 > '판정 기본값 적용'을 눌러주세요.", this);

            UpdateScoreDisplay();

            if (TryGetComponent<SongManager>(out var manager))
            {
                manager.onSongStartPlay.AddListener(ClearStats);
            }
        }

        public void ClearStats()
        {
            for (int i = 0; i < levels.values.Count; i++)
            {
                var x = levels.values[i];
                x.count = 0;
                x.onCountUpdate.Invoke(x.count.ToString());
            }

            score = 0;
            missed = 0;
            UpdateScoreDisplay();
            UpdateAccuracyDisplay(); // [수정]
        }

        public void AddCombo(int addCombo, float deltaDiff, int addScore)
        {
            // print(deltaDiff);
            combo += addCombo;
            if (combo > maxCombo)
            {
                maxCombo = combo;
                onMaxComboUpdate.Invoke(maxCombo.ToString());
            }

            for (int i = 0; i < levels.values.Count; i++)
            {
                var x = levels.values[i];
                if (deltaDiff <= x.threshold)
                {
                    x.count++;
                    score += (int) (addScore * x.scorePrecentage);
                    x.onCountUpdate.Invoke(x.count.ToString());
                    UpdateScoreDisplay();
                    UpdateAccuracyDisplay(); // [수정]
                    onComboStatusUpdate.Invoke(x.name);
                    // print(x.name);
                    return;
                }
            }

            //When no level matched
            onComboStatusUpdate.Invoke("");
        }

        public void UpdateScoreDisplay()
        {
            onScoreUpdate.Invoke(score.ToString());
        }
    }
}