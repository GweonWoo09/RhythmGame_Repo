using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

    /// <summary>
    /// 변환기가 에셋/파일 존재 여부를 물어볼 때 쓰는 창구.
    /// 실제 구현(Resources.Load 등)은 DialogueSheetImporter가 제공한다. 이렇게 분리해서
    /// 변환 로직 자체는 Unity 없이도 테스트할 수 있다.
    /// </summary>
    public interface IDialogueSheetEnvironment
    {
        bool SpeakerExists(string speakerId);
        bool SpeakerHasExpression(string speakerId, string expressionName);
        bool AudioExists(string resourcePath);
        bool SceneExists(string sceneName);
        bool SequenceFileExists(string language, string sequenceId);

        /// <summary>곡 에셋이 Resources 경로에 있는가 (songs 탭의 path 검사용)</summary>
        bool SongAssetExists(string resourcePath);
    }

    public sealed class GeneratedDialogueFile
    {
        public string Language;
        public string SequenceId;
        public string Json;
    }

    /// <summary>스프레드시트 한 개(챕터/엔딩 하나)의 탭 내용. 여러 개를 한 번에 변환하면 시트끼리 서로 참조할 수 있다.</summary>
    public sealed class DialogueSheetInput
    {
        /// <summary>오류 메시지에 붙는 시트 이름 (예: chapter2). 시트가 하나뿐이면 비워도 된다.</summary>
        public string Label;
        public string LinesCsv;
        public string MetaCsv;
        public string SongsCsv;
    }

    public sealed class SheetConvertResult
    {
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Infos = new List<string>();
        public readonly List<GeneratedDialogueFile> Files = new List<GeneratedDialogueFile>();
        public int SequenceCount;

        /// <summary>songs 탭이 있으면 만들어지는 곡 카탈로그 JSON (Resources/SongCatalog.json). 없으면 null.</summary>
        public string SongCatalogJson;
    }

    /// <summary>
    /// 구글 시트(lines / meta / songs 탭의 CSV)를 대사 JSON(v2 스키마)으로 변환한다. 스프레드시트 여러 개를 한 번에 변환할 수 있다.
    /// 오류가 하나라도 있으면 Files는 비어 있고, 오류는 "[탭 N행] (시퀀스) 메시지" 형태로 모두 모아서 돌려준다.
    ///
    /// [lines 탭 열]
    ///   sequence | id | type | speaker | expression | slot | name | style | text_{언어} ... |
    ///   if | goto | target_sequence | setFlags | commands | bgm | memo
    ///
    ///   type = line / choice / branch / comment
    ///   - line   : 대사 한 줄. speaker가 비어 있으면 내레이션.
    ///   - choice : 바로 위 line 행에 붙는 선택지. 텍스트는 text_{언어} 열.
    ///              speaker를 적으면 "에코": 선택지를 고른 뒤 그 화자가 같은 문구를 말하는 줄을 자동으로 만든다.
    ///              이때 expression / slot / name / style 칸은 그 에코 줄에 적용된다. (target_sequence와는 함께 쓸 수 없음)
    ///   - branch : 바로 위 line 행에 붙는 조건부 이동 (if, goto).
    ///   - comment: 무시 (sequence 칸이 # 로 시작해도 무시).
    ///
    ///   slot  = left / right / center (또는 왼쪽 / 오른쪽 / 가운데) 또는 0 이상의 번호. 비우면 meta의 cast 위치를 쓴다.
    ///   name  = 이 줄에서만 화자 이름 대신 보여줄 이름 (예: ???). {player} 같은 변수 사용 가능.
    ///   style = 대사 표시 방식 (thought, shout, whisper ...).
    ///   expression = 비우면 기본(첫 번째) 표정.
    ///   memo  = 작업 메모. 임포터는 읽지 않는다.
    ///   setFlags / commands 는 한 칸에 여러 개를 ; 또는 줄바꿈으로 구분한다.
    ///   commands = hide:슬롯 / show:슬롯 / show:슬롯:speakerId[:expression] / play:곡ID[:난이도] /
    ///              (그 밖의 이름:인자는 그대로 전달)
    ///     play: 그 줄을 넘기는 순간 곡을 연주하러 나갔다가, 돌아오면 다음 줄부터 이어진다. 곡 ID는 songs 탭에 있어야 한다.
    ///           한 줄에 하나만 쓸 수 있고, 선택지가 붙은 줄에는 쓸 수 없다.
    ///
    /// [songs 탭 열]  song_id | path | title | memo      (song_id 칸이 # 로 시작하면 무시)
    ///   song_id = 곡 ID. 영문/숫자/_ 만 가능하고, 곡 에셋 이름과 같아야 한다 (= path의 마지막 이름).
    ///   path    = Resources 기준 곡 에셋 경로 (예: Songs/tutorial_01). 임포터가 에셋이 있는지 검사한다.
    ///   결과: Resources/SongCatalog.json 에 곡 ID -> 경로 목록이 만들어진다.
    ///
    /// [meta 탭 열]   sequence | key | value | (memo)
    ///   key = bgm, chapterId, cast, onEnd.scene, onEnd.clearChapter, onEnd.unlockChapter, onEnd.songId, onEnd.difficulty,
    ///         onEnd.nextSequence
    ///   onEnd.nextSequence = 이 시퀀스가 끝나면 이어서 재생할 시퀀스 (다른 시트의 시퀀스도 가능. 예: chapter2 -> chapter3)
    ///
    /// [여러 시트]  챕터/엔딩마다 스프레드시트를 나눠도 된다. Build(inputs)에 한꺼번에 넘기면
    ///   target_sequence / onEnd.nextSequence 로 서로를 가리킬 수 있고, songs 탭은 하나의 곡 카탈로그로 합쳐진다.
    ///   같은 곡 ID가 여러 시트에 있어도 되지만 path가 서로 다르면 오류다.
    ///   path가 비어 있는 곡은 경고만 내고 카탈로그에서 뺀다 (그 곡의 play 줄은 연주 없이 다음 대사로 넘어간다).
    ///   cast 값 예: "White:left;Black:right"  또는 "White:left:Happy;Black:2" (speakerId:slot[:expression])
    ///
    /// Unity API에 의존하지 않는다.
    /// </summary>
    public static class DialogueSheetConverter
    {
        #region Field
        private const string ColSequence = "sequence";
        private const string ColId = "id";
        private const string ColType = "type";
        private const string ColSpeaker = "speaker";
        private const string ColExpression = "expression";
        private const string ColSlot = "slot";
        private const string ColName = "name";
        private const string ColStyle = "style";
        private const string ColIf = "if";
        private const string ColGoto = "goto";
        private const string ColTargetSequence = "target_sequence";
        private const string ColSetFlags = "setflags";
        private const string ColCommands = "commands";
        private const string ColBgm = "bgm";
        private const string ColMemo = "memo";
        private const string TextPrefix = "text_";

        private const string EndLabel = "end";
        private const string EchoIdPrefix = "echo_r";
        private const string GeneratedNote = "Generated from Google Sheet by DialogueSheetImporter. Do not edit by hand.";

        private static readonly HashSet<string> KnownColumns = new HashSet<string>
    {
        ColSequence, ColId, ColType, ColSpeaker, ColExpression, ColSlot, ColName, ColStyle, ColIf, ColGoto,
        ColTargetSequence, ColSetFlags, ColCommands, ColBgm, ColMemo
    };

        private static readonly HashSet<string> KnownMetaKeys = new HashSet<string>
    {
        "bgm", "chapterid", "cast",
        "onend.scene", "onend.clearchapter", "onend.unlockchapter", "onend.songid", "onend.difficulty",
        "onend.nextsequence"
    };

        // 오류 메시지에 시트 이름을 붙이기 위한 정보 (Build 한 번 동안만 쓴다)
        private static readonly Dictionary<string, string> SequenceLabels = new Dictionary<string, string>();
        private static string _readingLabel;

        // DialogueUIView가 기본으로 제공하는 스타일 이름 / DialogueVariables가 기본으로 제공하는 변수 이름
        private static readonly HashSet<string> BuiltInStyles = new HashSet<string> { "thought", "shout", "whisper" };
        private static readonly HashSet<string> BuiltInVariables = new HashSet<string> { "player" };

        private static readonly Regex BracePair = new Regex(@"\{([^{}]*)\}");
        #endregion

        #region Models
        private sealed class SheetRow
        {
            public int Number;
            public readonly Dictionary<string, string> Cells = new Dictionary<string, string>();

            public string Get(string column)
            {
                string value;
                return Cells.TryGetValue(column, out value) ? value : "";
            }
        }

        private sealed class Table
        {
            public readonly List<string> Columns = new List<string>();
            public readonly List<SheetRow> Rows = new List<SheetRow>();
        }

        private sealed class MetaEntry
        {
            public string Value;
            public int Row;
        }

        private sealed class CastModel
        {
            public string Speaker;
            public int Slot;
            public string Expression;
        }

        private sealed class BranchModel
        {
            public SheetRow Row;
            public string Condition;
            public string Goto;
        }

        private sealed class ChoiceModel
        {
            public SheetRow Row;
            public string Condition;
            public string Goto;
            public string TargetSequence;
            public string[] SetFlags;
            public Dictionary<string, string> Texts = new Dictionary<string, string>();

            /// <summary>speaker가 적힌 choice 행이면 만들어지는 에코 줄 (선택 직후 그 화자가 선택 문구를 말한다)</summary>
            public LineModel Echo;
        }

        private sealed class LineModel
        {
            public SheetRow Row;
            public string Id;
            public string Condition;
            public string Speaker;
            public string Expression;
            public string SlotText;
            public int Slot;
            public string DisplayName;
            public string Style;
            public string Goto;
            public string Bgm;
            public string[] SetFlags;
            public string[] Commands;
            public Dictionary<string, string> Texts = new Dictionary<string, string>();
            public List<BranchModel> Branches = new List<BranchModel>();
            public List<ChoiceModel> Choices = new List<ChoiceModel>();
        }

        private sealed class SongModel
        {
            public string Id;
            public string Title;
            public string Path;
        }

        private sealed class SequenceModel
        {
            public string Name;
            public List<LineModel> Lines = new List<LineModel>();
            public Dictionary<string, MetaEntry> Meta = new Dictionary<string, MetaEntry>();
            public HashSet<string> Ids = new HashSet<string>();
            public int BlankExpressionCount;

            // meta 해석 결과
            public List<CastModel> Cast = new List<CastModel>();
            public bool HasChapterId;
            public int ChapterId;
            public string Bgm;
            public string EndScene;
            public int EndClearChapter;
            public int EndUnlockChapter;
            public string EndSongId;
            public string EndDifficulty;
            public string EndNextSequence;
        }
        #endregion

        #region Build
        public static SheetConvertResult Build(string linesCsv, string metaCsv, IDialogueSheetEnvironment env)
        {
            return Build(linesCsv, metaCsv, null, env);
        }

        /// <param name="songsCsv">songs 탭 CSV. null이면 곡 카탈로그를 만들지 않고, play 명령은 오류가 된다.</param>
        public static SheetConvertResult Build(string linesCsv, string metaCsv, string songsCsv, IDialogueSheetEnvironment env)
        {
            return Build(new[] { new DialogueSheetInput { Label = "", LinesCsv = linesCsv, MetaCsv = metaCsv, SongsCsv = songsCsv } }, env);
        }

        /// <summary>스프레드시트 여러 개(챕터/엔딩별)를 한 번에 변환한다. 시퀀스 이름은 전체에서 겹치면 안 된다.</summary>
        public static SheetConvertResult Build(IList<DialogueSheetInput> inputs, IDialogueSheetEnvironment env)
        {
            var result = new SheetConvertResult();
            SequenceLabels.Clear();
            _readingLabel = null;

            try
            {
                BuildAll(inputs, env, result);
            }
            finally
            {
                SequenceLabels.Clear();
                _readingLabel = null;
            }

            return result;
        }

        private static void BuildAll(IList<DialogueSheetInput> inputs, IDialogueSheetEnvironment env, SheetConvertResult result)
        {
            if (inputs == null || inputs.Count == 0)
            {
                result.Errors.Add("변환할 시트가 없습니다.");
                return;
            }

            var languages = new List<string>();
            var sequences = new Dictionary<string, SequenceModel>();
            var order = new List<string>();
            var songs = new Dictionary<string, SongModel>();
            var songOrder = new List<string>();
            bool anySongsTab = false;

            foreach (DialogueSheetInput input in inputs)
            {
                string label = input.Label ?? "";
                string prefix = label.Length > 0 ? label + " · " : "";
                _readingLabel = label;

                Table lines = ReadTable(input.LinesCsv, prefix + "lines", result);
                if (lines == null) continue;

                bool headerOk = true;
                foreach (string required in new[] { ColSequence, ColType })
                {
                    if (!lines.Columns.Contains(required))
                    {
                        result.Errors.Add($"[{prefix}lines 탭] 필수 열 '{required}'이(가) 없습니다. (1행은 열 이름이어야 합니다)");
                        headerOk = false;
                    }
                }

                var sheetLanguages = new List<string>();
                var unknownColumns = new List<string>();
                foreach (string column in lines.Columns)
                {
                    if (column.Length == 0) continue;

                    if (column.StartsWith(TextPrefix, StringComparison.Ordinal) && column.Length > TextPrefix.Length)
                    {
                        string language = column.Substring(TextPrefix.Length);
                        if (!IsValidName(language))
                        {
                            result.Errors.Add($"[{prefix}lines 탭] 열 이름 '{column}'의 언어 코드가 올바르지 않습니다. (영문/숫자/-/_ 만 가능)");
                            headerOk = false;
                        }
                        sheetLanguages.Add(language);
                    }
                    else if (!KnownColumns.Contains(column))
                    {
                        unknownColumns.Add(column);
                    }
                }

                if (sheetLanguages.Count == 0)
                {
                    result.Errors.Add($"[{prefix}lines 탭] 대사 텍스트 열이 없습니다. 'text_ko' 처럼 'text_언어코드' 열이 하나 이상 필요합니다.");
                    headerOk = false;
                }

                if (unknownColumns.Count > 0)
                {
                    result.Infos.Add($"[{prefix}lines 탭] 인식하지 못한 열은 무시합니다: " + string.Join(", ", unknownColumns.ToArray()));
                }

                if (!headerOk) continue;

                // 언어 목록은 모든 시트의 합집합. 첫 시트의 첫 text_ 열이 기본 언어다.
                foreach (string language in sheetLanguages)
                {
                    if (!languages.Contains(language)) languages.Add(language);
                }

                BuildSequences(lines, sheetLanguages, sequences, order, label, result);
                ReadMeta(input.MetaCsv, sequences, label, result);

                if (!string.IsNullOrWhiteSpace(input.SongsCsv))
                {
                    anySongsTab = true;
                    ReadSongs(input.SongsCsv, label, env, songs, songOrder, result);
                }
            }

            _readingLabel = null;

            if (order.Count == 0 && result.Errors.Count == 0)
            {
                result.Errors.Add("[lines 탭] 변환할 대사가 없습니다.");
            }

            if (languages.Count == 0) return;

            string defaultLanguage = languages[0];
            HashSet<string> songIds = anySongsTab ? new HashSet<string>(songs.Keys) : null;

            // 다른 시퀀스의 id를 참조하는 검사가 있으므로, id 수집을 먼저 전부 끝낸다
            foreach (string name in order) CollectIds(sequences[name], result);
            foreach (string name in order) ValidateSequence(sequences[name], sequences, defaultLanguage, env, songIds, result);

            if (result.Errors.Count > 0) return;

            GenerateFiles(sequences, order, languages, result);

            if (anySongsTab)
            {
                var catalog = new List<SongModel>();
                foreach (string id in songOrder)
                {
                    SongModel song = songs[id];
                    if (song.Path.Length > 0)
                    {
                        catalog.Add(song);
                    }
                    else
                    {
                        result.Warnings.Add($"[songs 탭] 곡 '{id}'의 path가 비어 있습니다. 경로를 넣기 전에는 play:{id} 줄에서 연주 없이 다음 대사로 넘어갑니다.");
                    }
                }

                result.SongCatalogJson = BuildSongCatalogJson(catalog);
            }

            result.SequenceCount = order.Count;
        }
        #endregion

        #region Read
        private static Table ReadTable(string csv, string tabName, SheetConvertResult result)
        {
            List<List<string>> records = DialogueCsvParser.Parse(csv);

            if (records.Count == 0)
            {
                result.Errors.Add($"[{tabName} 탭] 내용이 비어 있습니다.");
                return null;
            }

            var table = new Table();
            var seen = new HashSet<string>();

            foreach (string raw in records[0])
            {
                string name = (raw ?? "").Trim().ToLowerInvariant();
                table.Columns.Add(name);

                if (name.Length > 0 && !seen.Add(name))
                {
                    result.Errors.Add($"[{tabName} 탭] 열 이름 '{name}'이(가) 중복됩니다.");
                }
            }

            for (int r = 1; r < records.Count; r++)
            {
                List<string> record = records[r];
                var row = new SheetRow { Number = r + 1 };
                bool anyContent = false;

                for (int c = 0; c < table.Columns.Count; c++)
                {
                    string column = table.Columns[c];
                    if (column.Length == 0) continue;

                    string value = c < record.Count ? Normalize(record[c]) : "";
                    row.Cells[column] = value;
                    if (value.Length > 0) anyContent = true;
                }

                if (anyContent) table.Rows.Add(row);
            }

            return table;
        }

        /// <summary>줄바꿈을 \n 으로 통일하고 앞뒤 공백을 제거한다.</summary>
        private static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        }

        private static void BuildSequences(Table lines, List<string> languages,
            Dictionary<string, SequenceModel> sequences, List<string> order, string label, SheetConvertResult result)
        {
            // 이 시트에서 처음 나온 시퀀스 / 다른 시트와 이름이 겹쳐 건너뛸 시퀀스
            var ownSequences = new HashSet<string>();
            var duplicated = new HashSet<string>();

            foreach (SheetRow row in lines.Rows)
            {
                string sequenceName = row.Get(ColSequence);
                string type = row.Get(ColType).ToLowerInvariant();

                if (sequenceName.StartsWith("#", StringComparison.Ordinal) || type == "comment") continue;

                if (sequenceName.Length == 0)
                {
                    AddError(result, "lines", row.Number, null, "sequence 칸이 비어 있습니다.");
                    continue;
                }

                if (!IsValidName(sequenceName))
                {
                    AddError(result, "lines", row.Number, sequenceName, "시퀀스 이름은 영문/숫자/-/_ 만 쓸 수 있습니다. (파일 이름이 됩니다)");
                    continue;
                }

                if (duplicated.Contains(sequenceName)) continue;

                SequenceModel sequence;
                if (!sequences.TryGetValue(sequenceName, out sequence))
                {
                    sequence = new SequenceModel { Name = sequenceName };
                    sequences[sequenceName] = sequence;
                    order.Add(sequenceName);
                    ownSequences.Add(sequenceName);
                    SequenceLabels[sequenceName] = label;
                }
                else if (!ownSequences.Contains(sequenceName))
                {
                    string other;
                    SequenceLabels.TryGetValue(sequenceName, out other);
                    duplicated.Add(sequenceName);
                    AddError(result, "lines", row.Number, null,
                        $"시퀀스 '{sequenceName}'는 다른 시트({other})에 이미 있습니다. 시퀀스 이름은 전체 시트에서 겹치면 안 됩니다.");
                    continue;
                }

                switch (type)
                {
                    case "line":
                        sequence.Lines.Add(ReadLine(row, languages));
                        break;

                    case "choice":
                        if (sequence.Lines.Count == 0)
                        {
                            AddError(result, "lines", row.Number, sequenceName, "choice 행 위에 line 행이 없습니다. 선택지는 바로 위 line 행에 붙습니다.");
                            break;
                        }
                        WarnIgnoredColumns(row, sequenceName, "choice", result);
                        sequence.Lines[sequence.Lines.Count - 1].Choices.Add(ReadChoice(row, sequenceName, languages, result));
                        break;

                    case "branch":
                        if (sequence.Lines.Count == 0)
                        {
                            AddError(result, "lines", row.Number, sequenceName, "branch 행 위에 line 행이 없습니다. branch는 바로 위 line 행에 붙습니다.");
                            break;
                        }
                        WarnIgnoredColumns(row, sequenceName, "branch", result);
                        sequence.Lines[sequence.Lines.Count - 1].Branches.Add(new BranchModel
                        {
                            Row = row,
                            Condition = row.Get(ColIf),
                            Goto = row.Get(ColGoto)
                        });
                        break;

                    case "":
                        AddError(result, "lines", row.Number, sequenceName, "type 칸이 비어 있습니다. (line / choice / branch / comment)");
                        break;

                    default:
                        AddError(result, "lines", row.Number, sequenceName, $"알 수 없는 type '{type}'입니다. (line / choice / branch / comment)");
                        break;
                }
            }
        }

        private static LineModel ReadLine(SheetRow row, List<string> languages)
        {
            var line = new LineModel
            {
                Row = row,
                Id = row.Get(ColId),
                Condition = row.Get(ColIf),
                Speaker = row.Get(ColSpeaker),
                Expression = row.Get(ColExpression),
                SlotText = row.Get(ColSlot),
                DisplayName = row.Get(ColName),
                Style = row.Get(ColStyle),
                Goto = row.Get(ColGoto),
                Bgm = row.Get(ColBgm),
                SetFlags = SplitList(row.Get(ColSetFlags)),
                Commands = SplitList(row.Get(ColCommands))
            };

            foreach (string language in languages)
            {
                line.Texts[language] = row.Get(TextPrefix + language);
            }

            return line;
        }

        private static ChoiceModel ReadChoice(SheetRow row, string sequenceName, List<string> languages, SheetConvertResult result)
        {
            var choice = new ChoiceModel
            {
                Row = row,
                Condition = row.Get(ColIf),
                Goto = row.Get(ColGoto),
                TargetSequence = row.Get(ColTargetSequence),
                SetFlags = SplitList(row.Get(ColSetFlags))
            };

            foreach (string language in languages)
            {
                choice.Texts[language] = row.Get(TextPrefix + language);
            }

            string speaker = row.Get(ColSpeaker);

            if (speaker.Length > 0)
            {
                // 에코 줄: 선택 직후 이 화자가 선택지와 같은 문구를 말한다. 선택 문구는 text 열과 공유한다.
                choice.Echo = new LineModel
                {
                    Row = row,
                    Id = EchoIdPrefix + row.Number.ToString(CultureInfo.InvariantCulture),
                    Condition = "",
                    Speaker = speaker,
                    Expression = row.Get(ColExpression),
                    SlotText = row.Get(ColSlot),
                    DisplayName = row.Get(ColName),
                    Style = row.Get(ColStyle),
                    Goto = choice.Goto,
                    Bgm = "",
                    Texts = choice.Texts
                };
            }
            else if (row.Get(ColExpression).Length > 0 || row.Get(ColSlot).Length > 0
                     || row.Get(ColName).Length > 0 || row.Get(ColStyle).Length > 0)
            {
                AddWarning(result, "lines", row.Number, sequenceName,
                    "choice 행의 expression / slot / name / style은 speaker가 있을 때(에코 줄)만 쓰입니다. speaker가 비어 있어 무시합니다.");
            }

            return choice;
        }

        private static void WarnIgnoredColumns(SheetRow row, string sequenceName, string type, SheetConvertResult result)
        {
            string[] columns = type == "choice"
                ? new[] { ColId, ColCommands, ColBgm }
                : new[] { ColId, ColSpeaker, ColExpression, ColSlot, ColName, ColStyle, ColCommands, ColBgm };

            var ignored = new List<string>();
            foreach (string column in columns)
            {
                if (row.Get(column).Length > 0) ignored.Add(column);
            }

            if (ignored.Count > 0)
            {
                AddWarning(result, "lines", row.Number, sequenceName,
                    $"{type} 행에서는 {string.Join(" / ", ignored.ToArray())} 칸을 쓰지 않습니다. (무시됨)");
            }
        }

        private static void ReadMeta(string metaCsv, Dictionary<string, SequenceModel> sequences, string label, SheetConvertResult result)
        {
            if (string.IsNullOrWhiteSpace(metaCsv)) return;

            string prefix = label.Length > 0 ? label + " · " : "";
            Table meta = ReadTable(metaCsv, prefix + "meta", result);
            if (meta == null) return;

            foreach (string required in new[] { "sequence", "key", "value" })
            {
                if (!meta.Columns.Contains(required))
                {
                    result.Errors.Add($"[{prefix}meta 탭] 필수 열 '{required}'이(가) 없습니다. (1행은 sequence / key / value)");
                    return;
                }
            }

            foreach (SheetRow row in meta.Rows)
            {
                string sequenceName = row.Get("sequence");
                if (sequenceName.StartsWith("#", StringComparison.Ordinal)) continue;

                string key = row.Get("key").ToLowerInvariant();
                string value = row.Get("value");

                if (value.Length == 0) continue; // 값이 없으면 지정하지 않은 것으로 본다

                if (!KnownMetaKeys.Contains(key))
                {
                    AddError(result, "meta", row.Number, sequenceName,
                        $"알 수 없는 key '{row.Get("key")}'입니다. (bgm, chapterId, cast, onEnd.scene, onEnd.clearChapter, onEnd.unlockChapter, onEnd.songId, onEnd.difficulty, onEnd.nextSequence)");
                    continue;
                }

                SequenceModel sequence;
                string owner;
                if (!sequences.TryGetValue(sequenceName, out sequence)
                    || !SequenceLabels.TryGetValue(sequenceName, out owner) || owner != label)
                {
                    AddWarning(result, "meta", row.Number, sequenceName, "이 시트의 lines 탭에 없는 시퀀스라 무시합니다.");
                    continue;
                }

                if (sequence.Meta.ContainsKey(key))
                {
                    AddWarning(result, "meta", row.Number, sequenceName, $"key '{key}'가 중복되어 마지막 값을 사용합니다.");
                }

                sequence.Meta[key] = new MetaEntry { Value = value, Row = row.Number };
            }
        }
        #endregion

        #region Validate
        private static void CollectIds(SequenceModel sequence, SheetConvertResult result)
        {
            foreach (LineModel line in sequence.Lines)
            {
                if (line.Id.Length > 0)
                {
                    if (!IsValidName(line.Id))
                    {
                        AddError(result, "lines", line.Row.Number, sequence.Name, $"id '{line.Id}'는 영문/숫자/-/_ 만 쓸 수 있습니다.");
                    }
                    else if (line.Id == EndLabel)
                    {
                        AddError(result, "lines", line.Row.Number, sequence.Name, $"'{EndLabel}'는 예약어라 id로 쓸 수 없습니다. (goto에서 '대화 종료'를 뜻합니다)");
                    }
                    else if (!sequence.Ids.Add(line.Id))
                    {
                        AddError(result, "lines", line.Row.Number, sequence.Name, $"id '{line.Id}'가 중복됩니다.");
                    }
                }

                // 에코 줄의 자동 id가 직접 적은 id와 겹치지 않는지 확인
                foreach (ChoiceModel choice in line.Choices)
                {
                    if (choice.Echo != null && !sequence.Ids.Add(choice.Echo.Id))
                    {
                        AddError(result, "lines", choice.Row.Number, sequence.Name,
                            $"id '{choice.Echo.Id}'가 에코 줄에 자동으로 붙는 id와 겹칩니다. (이 id는 직접 쓰지 마세요)");
                    }
                }
            }
        }

        private static void ValidateSequence(SequenceModel sequence, Dictionary<string, SequenceModel> sequences,
            string defaultLanguage, IDialogueSheetEnvironment env, HashSet<string> songIds, SheetConvertResult result)
        {
            ValidateMeta(sequence, sequences, defaultLanguage, env, result);

            foreach (LineModel line in sequence.Lines)
            {
                ValidateLine(sequence, line, defaultLanguage, env, songIds, result);

                if (line.Choices.Count > 0 && line.Branches.Count > 0)
                {
                    AddWarning(result, "lines", line.Row.Number, sequence.Name, "선택지가 있는 줄의 branch는 쓰이지 않습니다.");
                }

                if (line.Choices.Count > 0 && line.Goto.Length > 0)
                {
                    AddWarning(result, "lines", line.Row.Number, sequence.Name, "선택지가 있는 줄의 goto는 쓰이지 않습니다. (선택지의 goto가 우선)");
                }

                foreach (BranchModel branch in line.Branches)
                {
                    CheckCondition(branch.Condition, branch.Row.Number, sequence.Name, result);

                    if (branch.Goto.Length == 0)
                    {
                        AddError(result, "lines", branch.Row.Number, sequence.Name, "branch 행에는 goto가 필요합니다.");
                    }
                    else
                    {
                        CheckLabel(sequence, branch.Goto, branch.Row.Number, result);
                    }
                }

                foreach (ChoiceModel choice in line.Choices)
                {
                    ValidateChoice(sequence, choice, sequences, defaultLanguage, env, result);
                }
            }

            if (sequence.BlankExpressionCount > 0)
            {
                result.Infos.Add($"[{sequence.Name}] expression이 비어 있는 {sequence.BlankExpressionCount}곳은 기본 표정(첫 번째 표정)으로 표시됩니다.");
            }
        }

        private static void ValidateMeta(SequenceModel sequence, Dictionary<string, SequenceModel> sequences,
            string defaultLanguage, IDialogueSheetEnvironment env, SheetConvertResult result)
        {
            MetaEntry entry;

            if (sequence.Meta.TryGetValue("onend.nextsequence", out entry))
            {
                if (!IsValidName(entry.Value))
                {
                    AddError(result, "meta", entry.Row, sequence.Name, $"onEnd.nextSequence '{entry.Value}'는 영문/숫자/-/_ 만 쓸 수 있습니다.");
                }
                else if (entry.Value == sequence.Name)
                {
                    AddError(result, "meta", entry.Row, sequence.Name, "onEnd.nextSequence가 자기 자신을 가리킵니다. (끝나지 않는 반복)");
                }
                else if (!sequences.ContainsKey(entry.Value) && !env.SequenceFileExists(defaultLanguage, entry.Value))
                {
                    AddError(result, "meta", entry.Row, sequence.Name,
                        $"onEnd.nextSequence '{entry.Value}'가 어느 시트에도 없고, 이미 만들어진 JSON 파일도 없습니다.");
                }
                else
                {
                    sequence.EndNextSequence = entry.Value;
                }
            }

            if (sequence.Meta.TryGetValue("chapterid", out entry))
            {
                int value;
                if (TryParseNonNegative(entry.Value, out value))
                {
                    sequence.HasChapterId = true;
                    sequence.ChapterId = value;
                }
                else
                {
                    AddError(result, "meta", entry.Row, sequence.Name, $"chapterId '{entry.Value}'는 0 이상의 정수여야 합니다.");
                }
            }

            if (sequence.Meta.TryGetValue("bgm", out entry))
            {
                if (env.AudioExists(entry.Value)) sequence.Bgm = entry.Value;
                else AddError(result, "meta", entry.Row, sequence.Name, $"bgm 'Resources/{entry.Value}'에서 오디오를 찾을 수 없습니다.");
            }

            if (sequence.Meta.TryGetValue("onend.scene", out entry))
            {
                if (env.SceneExists(entry.Value)) sequence.EndScene = entry.Value;
                else AddError(result, "meta", entry.Row, sequence.Name, $"onEnd.scene '{entry.Value}'는 SceneType에 없는 이름입니다.");
            }

            if (sequence.Meta.TryGetValue("onend.clearchapter", out entry))
            {
                int value;
                if (TryParseNonNegative(entry.Value, out value)) sequence.EndClearChapter = value;
                else AddError(result, "meta", entry.Row, sequence.Name, $"onEnd.clearChapter '{entry.Value}'는 0 이상의 정수여야 합니다.");
            }

            if (sequence.Meta.TryGetValue("onend.unlockchapter", out entry))
            {
                int value;
                if (TryParseNonNegative(entry.Value, out value)) sequence.EndUnlockChapter = value;
                else AddError(result, "meta", entry.Row, sequence.Name, $"onEnd.unlockChapter '{entry.Value}'는 0 이상의 정수여야 합니다.");
            }

            if (sequence.Meta.TryGetValue("onend.songid", out entry)) sequence.EndSongId = entry.Value;
            if (sequence.Meta.TryGetValue("onend.difficulty", out entry)) sequence.EndDifficulty = entry.Value;

            if (sequence.Meta.TryGetValue("cast", out entry))
            {
                ParseCast(sequence, entry, env, result);
            }
        }

        private static void ParseCast(SequenceModel sequence, MetaEntry entry, IDialogueSheetEnvironment env, SheetConvertResult result)
        {
            string[] items = SplitList(entry.Value);
            if (items == null) return;

            var usedSlots = new HashSet<int>();
            var usedSpeakers = new HashSet<string>();

            foreach (string item in items)
            {
                string[] parts = item.Split(':');
                int slot = -1;

                if (parts.Length < 2 || parts.Length > 3
                    || parts[0].Trim().Length == 0
                    || !DialogueSlots.TryParse(parts[1], out slot))
                {
                    AddError(result, "meta", entry.Row, sequence.Name,
                        $"cast 항목 '{item}'의 형식이 올바르지 않습니다. 'speakerId:slot' 또는 'speakerId:slot:expression' (slot은 left/right/center 또는 번호. 예: White:left;Black:right)");
                    continue;
                }

                string speaker = parts[0].Trim();
                string expression = parts.Length == 3 ? parts[2].Trim() : "";

                if (!env.SpeakerExists(speaker))
                {
                    AddError(result, "meta", entry.Row, sequence.Name, $"cast의 speaker '{speaker}' 에셋을 찾을 수 없습니다.");
                    continue;
                }

                if (expression.Length > 0 && !env.SpeakerHasExpression(speaker, expression))
                {
                    AddError(result, "meta", entry.Row, sequence.Name, $"cast의 '{speaker}'에는 표정 '{expression}'이(가) 없습니다.");
                    continue;
                }

                if (!usedSlots.Add(slot))
                {
                    AddError(result, "meta", entry.Row, sequence.Name, $"cast에서 슬롯 {DialogueSlots.Describe(slot)}이(가) 두 번 쓰였습니다.");
                    continue;
                }

                if (!usedSpeakers.Add(speaker))
                {
                    AddError(result, "meta", entry.Row, sequence.Name, $"cast에서 '{speaker}'이(가) 두 번 나옵니다.");
                    continue;
                }

                sequence.Cast.Add(new CastModel { Speaker = speaker, Slot = slot, Expression = expression });
            }
        }

        private static void ValidateLine(SequenceModel sequence, LineModel line, string defaultLanguage,
            IDialogueSheetEnvironment env, HashSet<string> songIds, SheetConvertResult result)
        {
            int row = line.Row.Number;

            if (GetText(line.Texts, defaultLanguage).Length == 0)
            {
                AddError(result, "lines", row, sequence.Name, $"text_{defaultLanguage} 칸이 비어 있습니다.");
            }

            foreach (KeyValuePair<string, string> text in line.Texts)
            {
                CheckPlaceholders(text.Value, row, sequence.Name, result);
            }

            CheckPlaceholders(line.DisplayName, row, sequence.Name, result);
            CheckStyle(line.Style, row, sequence.Name, result);
            CheckCondition(line.Condition, row, sequence.Name, result);
            CheckAssignments(line.SetFlags, row, sequence.Name, result);

            if (line.Goto.Length > 0)
            {
                CheckLabel(sequence, line.Goto, row, result);
            }

            if (line.Bgm.Length > 0 && !env.AudioExists(line.Bgm))
            {
                AddError(result, "lines", row, sequence.Name, $"bgm 'Resources/{line.Bgm}'에서 오디오를 찾을 수 없습니다.");
            }

            if (line.Commands != null)
            {
                int playCount = 0;

                foreach (string command in line.Commands)
                {
                    CheckCommand(command, row, sequence.Name, env, songIds, result);
                    if (CommandName(command) == "play") playCount++;
                }

                if (playCount > 1)
                {
                    AddError(result, "lines", row, sequence.Name, "한 줄에는 play 명령을 하나만 쓸 수 있습니다.");
                }

                if (playCount > 0 && line.Choices.Count > 0)
                {
                    AddError(result, "lines", row, sequence.Name,
                        "선택지가 붙은 줄에는 play를 쓸 수 없습니다. 선택 후 이어지는 줄에 쓰세요.");
                }
            }

            ValidateSpeaker(sequence, line, env, result);
        }

        /// <summary>화자, 표정, 슬롯을 검사하고 슬롯 번호를 확정한다. (line과 에코 줄 공통)</summary>
        private static void ValidateSpeaker(SequenceModel sequence, LineModel line, IDialogueSheetEnvironment env, SheetConvertResult result)
        {
            int row = line.Row.Number;

            // 내레이션 (speaker 없음)
            if (line.Speaker.Length == 0)
            {
                if (line.Expression.Length > 0 || line.SlotText.Length > 0 || line.DisplayName.Length > 0)
                {
                    AddWarning(result, "lines", row, sequence.Name, "speaker가 없는 줄(내레이션)에서는 expression / slot / name을 쓰지 않습니다. (무시됨)");
                }
                return;
            }

            bool speakerExists = env.SpeakerExists(line.Speaker);
            if (!speakerExists)
            {
                AddError(result, "lines", row, sequence.Name, $"speaker '{line.Speaker}' 에셋을 찾을 수 없습니다.");
            }

            if (line.Expression.Length == 0)
            {
                sequence.BlankExpressionCount++; // 기본 표정을 쓴다 (경고 대신 시퀀스별로 한 줄 요약)
            }
            else if (speakerExists && !env.SpeakerHasExpression(line.Speaker, line.Expression))
            {
                AddError(result, "lines", row, sequence.Name, $"'{line.Speaker}'에는 표정 '{line.Expression}'이(가) 없습니다.");
            }

            // 슬롯: 직접 적었으면 그 값, 비어 있으면 meta의 cast에서 가져온다
            if (line.SlotText.Length > 0)
            {
                int slot;
                if (DialogueSlots.TryParse(line.SlotText, out slot)) line.Slot = slot;
                else AddError(result, "lines", row, sequence.Name, $"slot '{line.SlotText}'를 해석할 수 없습니다. (left / right / center 또는 0 이상의 번호)");
            }
            else
            {
                CastModel member = sequence.Cast.Find(c => c.Speaker == line.Speaker);
                if (member != null) line.Slot = member.Slot;
                else AddError(result, "lines", row, sequence.Name, $"slot이 비어 있는데 meta의 cast에도 '{line.Speaker}'이(가) 없습니다.");
            }
        }

        private static void ValidateChoice(SequenceModel sequence, ChoiceModel choice, Dictionary<string, SequenceModel> sequences,
            string defaultLanguage, IDialogueSheetEnvironment env, SheetConvertResult result)
        {
            int row = choice.Row.Number;

            if (GetText(choice.Texts, defaultLanguage).Length == 0)
            {
                AddError(result, "lines", row, sequence.Name, $"선택지의 text_{defaultLanguage} 칸이 비어 있습니다.");
            }

            foreach (KeyValuePair<string, string> text in choice.Texts)
            {
                CheckPlaceholders(text.Value, row, sequence.Name, result);
            }

            CheckCondition(choice.Condition, row, sequence.Name, result);
            CheckAssignments(choice.SetFlags, row, sequence.Name, result);

            if (choice.Echo != null)
            {
                ValidateSpeaker(sequence, choice.Echo, env, result);
                CheckPlaceholders(choice.Echo.DisplayName, row, sequence.Name, result);
                CheckStyle(choice.Echo.Style, row, sequence.Name, result);
            }

            // 다른 시퀀스로 분기
            if (choice.TargetSequence.Length > 0)
            {
                if (choice.Echo != null)
                {
                    AddError(result, "lines", row, sequence.Name,
                        "speaker(에코 줄)는 같은 시퀀스 안으로 이동하는 선택지에서만 쓸 수 있습니다. target_sequence를 쓰려면 speaker를 비우세요.");
                }

                if (!IsValidName(choice.TargetSequence))
                {
                    AddError(result, "lines", row, sequence.Name, $"target_sequence '{choice.TargetSequence}'는 영문/숫자/-/_ 만 쓸 수 있습니다.");
                    return;
                }

                SequenceModel target;
                if (sequences.TryGetValue(choice.TargetSequence, out target))
                {
                    if (choice.Goto.Length > 0 && choice.Goto != EndLabel && !target.Ids.Contains(choice.Goto))
                    {
                        AddError(result, "lines", row, sequence.Name, $"target_sequence '{target.Name}'에 id '{choice.Goto}'인 줄이 없습니다.");
                    }
                }
                else if (!env.SequenceFileExists(defaultLanguage, choice.TargetSequence))
                {
                    AddError(result, "lines", row, sequence.Name,
                        $"target_sequence '{choice.TargetSequence}'가 시트에도 없고, 이미 만들어진 JSON 파일도 없습니다.");
                }
                else if (choice.Goto.Length > 0 && choice.Goto != EndLabel)
                {
                    AddWarning(result, "lines", row, sequence.Name,
                        $"'{choice.TargetSequence}'는 시트에 없는 기존 JSON이라 goto '{choice.Goto}'가 실제로 있는지는 확인하지 못했습니다.");
                }

                return;
            }

            // 같은 시퀀스 안에서 이동: goto가 반드시 필요하다
            if (choice.Goto.Length == 0)
            {
                AddError(result, "lines", row, sequence.Name, "선택지에는 goto(이동할 줄의 id 또는 end) 또는 target_sequence가 필요합니다.");
                return;
            }

            CheckLabel(sequence, choice.Goto, row, result);
        }

        private static void CheckLabel(SequenceModel sequence, string label, int row, SheetConvertResult result)
        {
            if (label == EndLabel) return;

            if (!sequence.Ids.Contains(label))
            {
                AddError(result, "lines", row, sequence.Name, $"goto 대상 id '{label}'인 줄이 이 시퀀스에 없습니다.");
            }
        }

        private static void CheckCondition(string condition, int row, string sequenceName, SheetConvertResult result)
        {
            string error;
            if (!DialogueExpression.TryValidateCondition(condition, out error))
            {
                AddError(result, "lines", row, sequenceName, $"if 조건식 '{condition}' 오류: {error}");
            }
        }

        private static void CheckAssignments(string[] assignments, int row, string sequenceName, SheetConvertResult result)
        {
            if (assignments == null) return;

            foreach (string assignment in assignments)
            {
                string error;
                if (!DialogueExpression.TryValidateAssignment(assignment, out error))
                {
                    AddError(result, "lines", row, sequenceName, $"setFlags '{assignment}' 오류: {error}");
                }
            }
        }

        private static void CheckStyle(string style, int row, string sequenceName, SheetConvertResult result)
        {
            if (string.IsNullOrEmpty(style)) return;

            if (!IsValidName(style))
            {
                AddError(result, "lines", row, sequenceName, $"style '{style}'는 영문/숫자/-/_ 만 쓸 수 있습니다.");
            }
            else if (!BuiltInStyles.Contains(style))
            {
                AddWarning(result, "lines", row, sequenceName,
                    $"style '{style}'는 기본 제공 스타일(thought, shout, whisper)이 아닙니다. DialogueUIView의 textStyles에 같은 이름으로 정의되어 있어야 합니다.");
            }
        }

        /// <summary>텍스트 안의 {변수} 문법을 검사한다. 모든 문제는 경고다 (화면에 그대로 보일 뿐 진행은 막지 않는다).</summary>
        private static void CheckPlaceholders(string text, int row, string sequenceName, SheetConvertResult result)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (text.IndexOf('{') < 0 && text.IndexOf('}') < 0) return;

            string rest = BracePair.Replace(text, match =>
            {
                string key = match.Groups[1].Value;

                if (!IsValidName(key) || key.IndexOf('-') >= 0)
                {
                    AddWarning(result, "lines", row, sequenceName, $"변수 '{{{key}}}'의 이름이 올바르지 않습니다. (영문/숫자/_ 만 가능)");
                }
                else if (!BuiltInVariables.Contains(key))
                {
                    AddWarning(result, "lines", row, sequenceName,
                        $"변수 '{{{key}}}'는 기본 변수가 아닙니다. 코드에서 DialogueVariables.Set(\"{key}\", ...)로 등록하지 않으면 그대로 표시됩니다. (기본 제공: {{player}})");
                }

                return "";
            });

            if (rest.IndexOf('{') >= 0 || rest.IndexOf('}') >= 0)
            {
                AddWarning(result, "lines", row, sequenceName, "짝이 맞지 않는 { } 가 있습니다. 변수는 {player} 처럼 중괄호로 감싸야 합니다.");
            }
        }

        private static string CommandName(string command)
        {
            int colon = command.IndexOf(':');
            return (colon < 0 ? command : command.Substring(0, colon)).Trim();
        }

        private static void CheckCommand(string command, int row, string sequenceName, IDialogueSheetEnvironment env,
            HashSet<string> songIds, SheetConvertResult result)
        {
            int colon = command.IndexOf(':');
            string name = (colon < 0 ? command : command.Substring(0, colon)).Trim();
            string arg = colon < 0 ? "" : command.Substring(colon + 1).Trim();

            if (name.Length == 0)
            {
                AddError(result, "lines", row, sequenceName, $"commands 항목 '{command}'에 명령 이름이 없습니다. (형식: 이름:인자)");
                return;
            }

            int slot;

            if (name == "hide")
            {
                if (!DialogueSlots.TryParse(arg, out slot))
                {
                    AddError(result, "lines", row, sequenceName, $"hide 명령에는 슬롯(left / right / center 또는 번호)이 필요합니다. (예: hide:left) 입력값: '{command}'");
                }
            }
            else if (name == "show")
            {
                string[] parts = arg.Split(':');

                if (!DialogueSlots.TryParse(parts[0], out slot) || parts.Length > 3)
                {
                    AddError(result, "lines", row, sequenceName,
                        $"show 명령 형식이 올바르지 않습니다. show:슬롯 또는 show:슬롯:speakerId[:expression] (예: show:right) 입력값: '{command}'");
                    return;
                }

                if (parts.Length >= 2 && parts[1].Trim().Length > 0)
                {
                    string speaker = parts[1].Trim();

                    if (!env.SpeakerExists(speaker))
                    {
                        AddError(result, "lines", row, sequenceName, $"show 명령의 speaker '{speaker}' 에셋을 찾을 수 없습니다.");
                    }
                    else if (parts.Length == 3 && parts[2].Trim().Length > 0 && !env.SpeakerHasExpression(speaker, parts[2].Trim()))
                    {
                        AddError(result, "lines", row, sequenceName, $"show 명령의 '{speaker}'에는 표정 '{parts[2].Trim()}'이(가) 없습니다.");
                    }
                }
            }
            else if (name == "play")
            {
                string[] parts = arg.Split(':');
                string songId = parts[0].Trim();

                if (songId.Length == 0 || parts.Length > 2)
                {
                    AddError(result, "lines", row, sequenceName,
                        $"play 명령 형식이 올바르지 않습니다. play:곡ID 또는 play:곡ID:난이도 (예: play:tutorial_01) 입력값: '{command}'");
                    return;
                }

                if (parts.Length == 2 && !IsValidName(parts[1].Trim()))
                {
                    AddError(result, "lines", row, sequenceName, $"play 명령의 난이도 '{parts[1].Trim()}'는 영문/숫자/-/_ 만 쓸 수 있습니다.");
                }

                if (songIds == null)
                {
                    AddError(result, "lines", row, sequenceName,
                        $"play:{songId}를 쓰려면 곡 목록(songs 탭)이 필요합니다. 설정의 songsGid(또는 로컬 songs.csv)를 확인하세요.");
                }
                else if (!songIds.Contains(songId))
                {
                    AddError(result, "lines", row, sequenceName, $"곡 ID '{songId}'가 songs 탭에 없습니다.");
                }
            }
        }
        #endregion

        #region Songs
        /// <summary>
        /// songs 탭을 읽고 검사해서 전체 곡 목록(songs)에 합친다.
        /// 곡 ID는 플래그 이름(song_{id}_cleared)에 들어가므로 영문/숫자/_ 만 허용하고,
        /// 결과 처리가 곡 에셋 이름으로 곡을 구분하므로 ID는 경로의 마지막 이름과 같아야 한다.
        /// path가 비어 있는 곡은 ID만 등록한다 (play 명령 검사는 통과, 카탈로그에는 넣지 않음).
        /// 같은 ID가 여러 시트에 있으면 path가 채워진 쪽을 쓰고, 서로 다른 path면 오류다.
        /// </summary>
        private static void ReadSongs(string songsCsv, string label, IDialogueSheetEnvironment env,
            Dictionary<string, SongModel> songs, List<string> songOrder, SheetConvertResult result)
        {
            string prefix = label.Length > 0 ? label + " · " : "";
            Table table = ReadTable(songsCsv, prefix + "songs", result);
            if (table == null) return;

            foreach (string required in new[] { "song_id", "path" })
            {
                if (!table.Columns.Contains(required))
                {
                    result.Errors.Add($"[{prefix}songs 탭] 필수 열 '{required}'이(가) 없습니다. (1행은 song_id / path / title / memo)");
                    return;
                }
            }

            var seenInSheet = new HashSet<string>();

            foreach (SheetRow row in table.Rows)
            {
                string id = row.Get("song_id");
                if (id.StartsWith("#", StringComparison.Ordinal)) continue;

                string path = row.Get("path");

                if (id.Length == 0)
                {
                    AddError(result, "songs", row.Number, null, "song_id 칸이 비어 있습니다.");
                    continue;
                }

                if (!IsFlagSafeName(id))
                {
                    AddError(result, "songs", row.Number, null, $"song_id '{id}'는 영문/숫자/_ 만 쓸 수 있습니다. (플래그 이름 song_{id}_cleared 에 그대로 들어갑니다)");
                    continue;
                }

                if (!seenInSheet.Add(id))
                {
                    AddError(result, "songs", row.Number, null, $"song_id '{id}'가 중복됩니다.");
                    continue;
                }

                if (path.Length > 0)
                {
                    string assetName = path.Substring(path.LastIndexOf('/') + 1);
                    if (!string.Equals(assetName, id, StringComparison.OrdinalIgnoreCase))
                    {
                        AddError(result, "songs", row.Number, null,
                            $"song_id '{id}'가 path의 마지막 이름 '{assetName}'과 다릅니다. 연주 결과가 곡 에셋 이름으로 오기 때문에 같아야 합니다.");
                    }

                    if (!env.SongAssetExists(path))
                    {
                        AddError(result, "songs", row.Number, null, $"곡 에셋 'Resources/{path}'을(를) 찾을 수 없습니다.");
                    }
                }

                SongModel existing;
                if (!songs.TryGetValue(id, out existing))
                {
                    songs[id] = new SongModel { Id = id, Title = row.Get("title"), Path = path };
                    songOrder.Add(id);
                }
                else if (existing.Path.Length > 0 && path.Length > 0 && existing.Path != path)
                {
                    AddError(result, "songs", row.Number, null,
                        $"곡 '{id}'의 path '{path}'가 다른 시트에 적힌 path '{existing.Path}'와 다릅니다.");
                }
                else
                {
                    // 같은 곡을 여러 시트가 쓰는 경우: 비어 있는 칸만 채운다
                    if (existing.Path.Length == 0) existing.Path = path;
                    if (existing.Title.Length == 0) existing.Title = row.Get("title");
                }
            }
        }

        private static string BuildSongCatalogJson(List<SongModel> songs)
        {
            var entries = new List<string>();
            foreach (SongModel song in songs)
            {
                var m = new List<string>();
                m.Add(Prop("id", Str(song.Id)));
                if (song.Title.Length > 0) m.Add(Prop("title", Str(song.Title)));
                m.Add(Prop("path", Str(song.Path)));
                entries.Add(Obj(m));
            }

            var top = new List<string>();
            top.Add(Prop("_generated", Str(GeneratedNote)));
            top.Add("\"songs\": [" + (entries.Count > 0 ? "\n    " + string.Join(",\n    ", entries.ToArray()) + "\n  " : "") + "]");

            return "{\n  " + string.Join(",\n  ", top.ToArray()) + "\n}\n";
        }

        /// <summary>영문/숫자/_ 만 허용 (플래그 이름에 쓰일 수 있는 이름)</summary>
        private static bool IsFlagSafeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;

            foreach (char c in name)
            {
                if (!char.IsLetterOrDigit(c) && c != '_') return false;
            }

            return true;
        }
        #endregion

        #region Generate
        private static void GenerateFiles(Dictionary<string, SequenceModel> sequences, List<string> order,
            List<string> languages, SheetConvertResult result)
        {
            // 기본 언어(첫 text_ 열)는 항상 만들고, 나머지 언어는 내용이 하나라도 있을 때만 만든다
            var hasContent = new Dictionary<string, bool>();
            foreach (string language in languages) hasContent[language] = false;

            foreach (string name in order)
            {
                foreach (LineModel line in sequences[name].Lines)
                {
                    foreach (string language in languages)
                    {
                        if (GetText(line.Texts, language).Length > 0) hasContent[language] = true;
                    }

                    foreach (ChoiceModel choice in line.Choices)
                    {
                        foreach (string language in languages)
                        {
                            if (GetText(choice.Texts, language).Length > 0) hasContent[language] = true;
                        }
                    }
                }
            }

            for (int i = 0; i < languages.Count; i++)
            {
                string language = languages[i];

                if (i > 0 && !hasContent[language])
                {
                    result.Infos.Add($"text_{language} 열이 비어 있어 '{language}' 언어는 건너뜁니다.");
                    continue;
                }

                foreach (string name in order)
                {
                    var fallbackRows = new List<int>();
                    string json = BuildJson(sequences[name], language, languages[0], fallbackRows);

                    result.Files.Add(new GeneratedDialogueFile { Language = language, SequenceId = name, Json = json });

                    if (fallbackRows.Count > 0)
                    {
                        var shown = new List<string>();
                        for (int k = 0; k < fallbackRows.Count && k < 8; k++) shown.Add(fallbackRows[k].ToString(CultureInfo.InvariantCulture));

                        result.Warnings.Add($"[{name}] text_{language}이(가) 비어 있는 {fallbackRows.Count}곳은 text_{languages[0]} 내용으로 대체했습니다. " +
                                            $"(lines 탭 {string.Join(", ", shown.ToArray())}행{(fallbackRows.Count > shown.Count ? " ..." : "")})");
                    }
                }
            }
        }

        private static string BuildJson(SequenceModel sequence, string language, string defaultLanguage, List<int> fallbackRows)
        {
            var top = new List<string>();

            top.Add(Prop("_generated", Str(GeneratedNote)));

            if (sequence.HasChapterId) top.Add(Prop("chapterId", Num(sequence.ChapterId)));
            if (!string.IsNullOrEmpty(sequence.Bgm)) top.Add(Prop("bgm", Str(sequence.Bgm)));

            if (sequence.Cast.Count > 0)
            {
                var members = new List<string>();
                foreach (CastModel member in sequence.Cast)
                {
                    var m = new List<string>();
                    m.Add(Prop("speakerId", Str(member.Speaker)));
                    m.Add(Prop("slot", Num(member.Slot)));
                    if (member.Expression.Length > 0) m.Add(Prop("expression", Str(member.Expression)));
                    members.Add(Obj(m));
                }
                top.Add(Prop("cast", Arr(members)));
            }

            var end = new List<string>();
            if (!string.IsNullOrEmpty(sequence.EndScene)) end.Add(Prop("scene", Str(sequence.EndScene)));
            if (sequence.EndClearChapter > 0) end.Add(Prop("clearChapter", Num(sequence.EndClearChapter)));
            if (sequence.EndUnlockChapter > 0) end.Add(Prop("unlockChapter", Num(sequence.EndUnlockChapter)));
            if (!string.IsNullOrEmpty(sequence.EndSongId)) end.Add(Prop("songId", Str(sequence.EndSongId)));
            if (!string.IsNullOrEmpty(sequence.EndDifficulty)) end.Add(Prop("difficulty", Str(sequence.EndDifficulty)));
            if (!string.IsNullOrEmpty(sequence.EndNextSequence)) end.Add(Prop("nextSequence", Str(sequence.EndNextSequence)));
            if (end.Count > 0) top.Add(Prop("onEnd", Obj(end)));

            // 에코 줄은 시퀀스 맨 끝에 모아 둔다. 원래 줄들이 우연히 에코 줄로 흘러들어가지 않도록,
            // 에코 줄이 있으면 마지막 줄에 goto "end"를 명시한다.
            bool hasEcho = false;
            foreach (LineModel line in sequence.Lines)
            {
                foreach (ChoiceModel choice in line.Choices)
                {
                    if (choice.Echo != null) hasEcho = true;
                }
            }

            // 대사 한 줄 = JSON 한 줄. git diff에서 시트의 한 행 변경이 한 줄 변경으로 보인다.
            var lineJsons = new List<string>();
            var echoJsons = new List<string>();

            for (int i = 0; i < sequence.Lines.Count; i++)
            {
                LineModel line = sequence.Lines[i];
                bool forceEnd = hasEcho && i == sequence.Lines.Count - 1 && line.Goto.Length == 0;

                lineJsons.Add(BuildLineJson(line, language, defaultLanguage, fallbackRows, forceEnd));

                foreach (ChoiceModel choice in line.Choices)
                {
                    if (choice.Echo != null)
                    {
                        echoJsons.Add(BuildLineJson(choice.Echo, language, defaultLanguage, fallbackRows, false));
                    }
                }
            }

            lineJsons.AddRange(echoJsons);
            top.Add("\"lines\": [\n    " + string.Join(",\n    ", lineJsons.ToArray()) + "\n  ]");

            return "{\n  " + string.Join(",\n  ", top.ToArray()) + "\n}\n";
        }

        private static string BuildLineJson(LineModel line, string language, string defaultLanguage, List<int> fallbackRows, bool forceGotoEnd)
        {
            var m = new List<string>();

            if (line.Id.Length > 0) m.Add(Prop("id", Str(line.Id)));
            if (line.Condition.Length > 0) m.Add(Prop("if", Str(line.Condition)));

            if (line.Speaker.Length > 0)
            {
                m.Add(Prop("speakerId", Str(line.Speaker)));
                if (line.Expression.Length > 0) m.Add(Prop("expression", Str(line.Expression)));
                m.Add(Prop("screenSlot", Num(line.Slot)));
                if (line.DisplayName.Length > 0) m.Add(Prop("name", Str(line.DisplayName)));
            }

            if (line.Style.Length > 0) m.Add(Prop("style", Str(line.Style)));

            m.Add(Prop("text", Str(PickText(line.Texts, language, defaultLanguage, line.Row.Number, fallbackRows))));

            if (line.Bgm.Length > 0) m.Add(Prop("bgmOverride", Str(line.Bgm)));
            if (line.SetFlags != null) m.Add(Prop("setFlags", StrArr(line.SetFlags)));
            if (line.Commands != null) m.Add(Prop("commands", StrArr(line.Commands)));

            if (line.Branches.Count > 0)
            {
                var branches = new List<string>();
                foreach (BranchModel branch in line.Branches)
                {
                    var b = new List<string>();
                    if (branch.Condition.Length > 0) b.Add(Prop("if", Str(branch.Condition)));
                    b.Add(Prop("goto", Str(branch.Goto)));
                    branches.Add(Obj(b));
                }
                m.Add(Prop("branch", Arr(branches)));
            }

            string gotoValue = line.Goto.Length > 0 ? line.Goto : (forceGotoEnd ? EndLabel : "");
            if (gotoValue.Length > 0) m.Add(Prop("goto", Str(gotoValue)));

            if (line.Choices.Count > 0)
            {
                var choices = new List<string>();
                foreach (ChoiceModel choice in line.Choices)
                {
                    var c = new List<string>();
                    c.Add(Prop("text", Str(PickText(choice.Texts, language, defaultLanguage, choice.Row.Number, fallbackRows))));
                    if (choice.Condition.Length > 0) c.Add(Prop("if", Str(choice.Condition)));

                    // 에코 줄이 있으면 먼저 에코 줄로 가고, 에코 줄이 원래 목적지로 이어준다
                    string target = choice.Echo != null ? choice.Echo.Id : choice.Goto;
                    if (target.Length > 0) c.Add(Prop("goto", Str(target)));

                    if (choice.TargetSequence.Length > 0) c.Add(Prop("targetSequenceId", Str(choice.TargetSequence)));
                    if (choice.SetFlags != null) c.Add(Prop("setFlags", StrArr(choice.SetFlags)));
                    choices.Add(Obj(c));
                }
                m.Add(Prop("choices", Arr(choices)));
            }

            return Obj(m);
        }

        /// <summary>해당 언어 텍스트를 고르고, 비어 있으면 기본 언어 텍스트로 대체한 뒤 행 번호를 기록한다.</summary>
        private static string PickText(Dictionary<string, string> texts, string language, string defaultLanguage, int rowNumber, List<int> fallbackRows)
        {
            string text = GetText(texts, language);
            if (text.Length > 0) return text;

            if (language != defaultLanguage && !fallbackRows.Contains(rowNumber)) fallbackRows.Add(rowNumber);
            return GetText(texts, defaultLanguage);
        }
        #endregion

        #region JSON / Util
        private static string Prop(string key, string value) { return "\"" + key + "\": " + value; }
        private static string Str(string value) { return "\"" + Escape(value) + "\""; }
        private static string Num(int value) { return value.ToString(CultureInfo.InvariantCulture); }
        private static string Obj(List<string> members) { return "{ " + string.Join(", ", members.ToArray()) + " }"; }
        private static string Arr(List<string> items) { return "[ " + string.Join(", ", items.ToArray()) + " ]"; }

        private static string StrArr(string[] items)
        {
            var quoted = new List<string>();
            foreach (string item in items) quoted.Add(Str(item));
            return Arr(quoted);
        }

        private static string Escape(string value)
        {
            var sb = new StringBuilder(value.Length + 8);

            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }

            return sb.ToString();
        }

        private static string GetText(Dictionary<string, string> texts, string language)
        {
            string value;
            return texts.TryGetValue(language, out value) ? value : "";
        }

        /// <summary>";" 또는 줄바꿈으로 구분된 목록을 쪼갠다. 비어 있으면 null.</summary>
        private static string[] SplitList(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;

            var items = new List<string>();
            foreach (string part in value.Split(new[] { ';', '\n' }))
            {
                string trimmed = part.Trim();
                if (trimmed.Length > 0) items.Add(trimmed);
            }

            return items.Count > 0 ? items.ToArray() : null;
        }

        private static bool TryParseNonNegative(string text, out int value)
        {
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value >= 0;
        }

        /// <summary>시퀀스/언어/id/스타일 이름은 파일 이름이나 라벨로 쓰이므로 영문, 숫자, '-', '_' 만 허용한다.</summary>
        private static bool IsValidName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;

            foreach (char c in name)
            {
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-') return false;
            }

            return true;
        }

        private static void AddError(SheetConvertResult result, string tab, int row, string sequenceName, string message)
        {
            result.Errors.Add(Format(tab, row, sequenceName, message));
        }

        private static void AddWarning(SheetConvertResult result, string tab, int row, string sequenceName, string message)
        {
            result.Warnings.Add(Format(tab, row, sequenceName, message));
        }

        private static string Format(string tab, int row, string sequenceName, string message)
        {
            string sequencePart = string.IsNullOrEmpty(sequenceName) ? "" : $" ({sequenceName})";

            // 시트가 여러 개일 때 어느 시트인지 알려준다 (읽는 중이면 그 시트, 검증 중이면 시퀀스가 속한 시트)
            string label = _readingLabel;
            if (string.IsNullOrEmpty(label) && !string.IsNullOrEmpty(sequenceName)) SequenceLabels.TryGetValue(sequenceName, out label);
            string labelPart = string.IsNullOrEmpty(label) ? "" : label + " · ";

            return $"[{labelPart}{tab} 탭 {row}행]{sequencePart} {message}";
        }
        #endregion
    }