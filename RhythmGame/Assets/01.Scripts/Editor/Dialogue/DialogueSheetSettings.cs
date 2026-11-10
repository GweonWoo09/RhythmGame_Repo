using System;
using System.Collections.Generic;
using UnityEngine;

    /// <summary>
    /// 구글 시트 임포터 설정. Assets/Editor/ 폴더 안에 두면 빌드에 포함되지 않는다.
    /// 메뉴 Tools > Dialogue > Select Sheet Settings 를 처음 실행하면
    /// Assets/Editor/DialogueSheetSettings.asset 이 자동으로 만들어진다.
    ///
    /// 챕터/엔딩마다 스프레드시트를 나눠 쓸 수 있다. sheets에 전부 등록하면 한 번에 내려받아 함께 변환하므로
    /// 시트끼리 target_sequence / onEnd.nextSequence 로 서로를 가리킬 수 있다.
    /// </summary>
    [CreateAssetMenu(fileName = "DialogueSheetSettings", menuName = "Dialog/Sheet Settings")]
    public class DialogueSheetSettings : ScriptableObject
    {
        /// <summary>스프레드시트 한 개 (챕터 또는 엔딩 하나)</summary>
        [Serializable]
        public class SheetSource
        {
            [Tooltip("오류 메시지에 표시할 이름 (예: chapter2). 시퀀스 이름과 같게 두면 찾기 쉽다.")]
            public string label;

            [Tooltip("스프레드시트 주소 전체(https://docs.google.com/spreadsheets/d/…/edit) 또는 ID만 붙여넣어도 된다.\n" +
                     "공유 설정이 '링크가 있는 모든 사용자 - 뷰어'여야 내려받을 수 있다.")]
            public string spreadsheet;

            [Tooltip("lines 탭의 gid. 해당 탭을 열었을 때 주소 끝의 #gid=숫자 부분.")]
            public string linesGid = "0";

            [Tooltip("meta 탭의 gid. 비워두면 meta 탭 없이 변환한다.")]
            public string metaGid;

            [Tooltip("songs 탭(곡 ID와 에셋 경로 목록)의 gid. 이 시트에 곡이 없으면 비워 둔다.")]
            public string songsGid;

            [Tooltip("끄면 이 시트는 내려받지 않는다 (작업 중인 시트를 잠시 빼 둘 때).")]
            public bool enabled = true;
        }

        [Header("구글 시트 (챕터/엔딩별로 하나씩)")]
        public List<SheetSource> sheets = new List<SheetSource>();

        [Header("출력")]
        [Tooltip("JSON을 저장할 폴더. 런타임에서 불러오려면 Resources 폴더 아래여야 한다. 안에 {언어}/{시퀀스}.json 으로 저장된다.")]
        public string outputRoot = "Assets/Resources/Dialogue";

        [Tooltip("곡 카탈로그 JSON 저장 위치. 런타임(SongCatalog)이 Resources/SongCatalog 로 읽으므로 Resources 폴더 바로 아래여야 한다.")]
        public string songCatalogPath = "Assets/Resources/SongCatalog.json";

        [Tooltip("SpeakerDataSO 에셋이 있는 Resources 하위 경로 (DialogueJsonLoader의 Speakers 와 같아야 한다).")]
        public string speakerResourcesPath = "Speakers";

        // 예전 설정(스프레드시트 하나). sheets가 비어 있고 이 값이 있으면 OnValidate가 sheets의 첫 항목으로 옮긴다.
        [SerializeField, HideInInspector] private string spreadsheet;
        [SerializeField, HideInInspector] private string linesGid;
        [SerializeField, HideInInspector] private string metaGid;
        [SerializeField, HideInInspector] private string songsGid;

        private void OnValidate()
        {
            if (sheets == null) sheets = new List<SheetSource>();
            if (sheets.Count > 0 || string.IsNullOrWhiteSpace(spreadsheet)) return;

            sheets.Add(new SheetSource
            {
                label = "tutorial",
                spreadsheet = spreadsheet,
                linesGid = string.IsNullOrWhiteSpace(linesGid) ? "0" : linesGid,
                metaGid = metaGid,
                songsGid = songsGid
            });

            spreadsheet = "";
        }
    }
