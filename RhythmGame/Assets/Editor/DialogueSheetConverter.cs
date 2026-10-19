using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

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
}

public sealed class GeneratedDialogueFile
{
    public string Language;
    public string SequenceId;
    public string Json;
}

public sealed class SheetConvertResult
{
    public readonly List<string> Errors = new List<string>();
    public readonly List<string> Warnings = new List<string>();
    public readonly List<string> Infos = new List<string>();
    public readonly List<GeneratedDialogueFile> Files = new List<GeneratedDialogueFile>();
    public int SequenceCount;
}

/// <summary>
/// 구글 시트(CSV 두 장: lines 탭, meta 탭)를 대사 JSON(v2 스키마)으로 변환한다.
/// 오류가 하나라도 있으면 Files는 비어 있고, 오류는 "[탭 N행] (시퀀스) 메시지" 형태로 모두 모아서 돌려준다.
///
/// [lines 탭 열]  sequence | id | type | speaker | expression | slot | text_{언어} ... | if | goto |
///                target_sequence | setFlags | commands | bgm
///   type = line / choice / branch / comment
///   - line   : 대사 한 줄. speaker가 비어 있으면 내레이션.
///   - choice : 바로 위 line 행에 붙는 선택지. 텍스트는 text_{언어} 열.
///   - branch : 바로 위 line 행에 붙는 조건부 이동 (if, goto).
///   - comment: 무시 (sequence 칸이 # 로 시작해도 무시).
///   setFlags / commands 는 한 칸에 여러 개를 ; 또는 줄바꿈으로 구분한다.
///
/// [meta 탭 열]   sequence | key | value
///   key = bgm, chapterId, cast, onEnd.scene, onEnd.clearChapter, onEnd.unlockChapter, onEnd.songId, onEnd.difficulty
///   cast 값 예: "White:0;Black:1"  또는 "White:0:Happy;Black:1" (speakerId:slot[:expression])
///
/// Unity API에 의존하지 않는다.
/// </summary>
public static class DialogueSheetConverter
{
    private const string ColSequence = "sequence";
    private const string ColId = "id";
    private const string ColType = "type";
    private const string ColSpeaker = "speaker";
    private const string ColExpression = "expression";
    private const string ColSlot = "slot";
    private const string ColIf = "if";
    private const string ColGoto = "goto";
    private const string ColTargetSequence = "target_sequence";
    private const string ColSetFlags = "setflags";
    private const string ColCommands = "commands";
    private const string ColBgm = "bgm";
    private const string TextPrefix = "text_";

    private const string EndLabel = "end";
    private const string GeneratedNote = "Generated from Google Sheet by DialogueSheetImporter. Do not edit by hand.";

    private static readonly HashSet<string> KnownColumns = new HashSet<string>
    {
        ColSequence, ColId, ColType, ColSpeaker, ColExpression, ColSlot, ColIf, ColGoto,
        ColTargetSequence, ColSetFlags, ColCommands, ColBgm
    };

    private static readonly HashSet<string> KnownMetaKeys = new HashSet<string>
    {
        "bgm", "chapterid", "cast",
        "onend.scene", "onend.clearchapter", "onend.unlockchapter", "onend.songid", "onend.difficulty"
    };

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
        public string Goto;
        public string Bgm;
        public string[] SetFlags;
        public string[] Commands;
        public Dictionary<string, string> Texts = new Dictionary<string, string>();
        public List<BranchModel> Branches = new List<BranchModel>();
        public List<ChoiceModel> Choices = new List<ChoiceModel>();
    }

    private sealed class SequenceModel
    {
        public string Name;
        public List<LineModel> Lines = new List<LineModel>();
        public Dictionary<string, MetaEntry> Meta = new Dictionary<string, MetaEntry>();
        public HashSet<string> Ids = new HashSet<string>();

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
    }
    #endregion

    public static SheetConvertResult Build(string linesCsv, string metaCsv, IDialogueSheetEnvironment env)
    {
        var result = new SheetConvertResult();

        Table lines = ReadTable(linesCsv, "lines", result);
        if (lines == null) return result;

        foreach (string required in new[] { ColSequence, ColType })
        {
            if (!lines.Columns.Contains(required))
            {
                result.Errors.Add($"[lines 탭] 필수 열 '{required}'이(가) 없습니다. (1행은 열 이름이어야 합니다)");
            }
        }

        var languages = new List<string>();
        var unknownColumns = new List<string>();
        foreach (string column in lines.Columns)
        {
            if (column.Length == 0) continue;

            if (column.StartsWith(TextPrefix, StringComparison.Ordinal) && column.Length > TextPrefix.Length)
            {
                string language = column.Substring(TextPrefix.Length);
                if (!IsValidName(language))
                {
                    result.Errors.Add($"[lines 탭] 열 이름 '{column}'의 언어 코드가 올바르지 않습니다. (영문/숫자/-/_ 만 가능)");
                }
                languages.Add(language);
            }
            else if (!KnownColumns.Contains(column))
            {
                unknownColumns.Add(column);
            }
        }

        if (languages.Count == 0)
        {
            result.Errors.Add("[lines 탭] 대사 텍스트 열이 없습니다. 'text_ko' 처럼 'text_언어코드' 열이 하나 이상 필요합니다.");
        }

        if (unknownColumns.Count > 0)
        {
            result.Infos.Add("[lines 탭] 인식하지 못한 열은 무시합니다: " + string.Join(", ", unknownColumns.ToArray()));
        }

        if (result.Errors.Count > 0) return result;

        string defaultLanguage = languages[0];

        var sequences = new Dictionary<string, SequenceModel>();
        var order = new List<string>();

        BuildSequences(lines, languages, sequences, order, result);
        ReadMeta(metaCsv, sequences, result);

        if (order.Count == 0 && result.Errors.Count == 0)
        {
            result.Errors.Add("[lines 탭] 변환할 대사가 없습니다.");
        }

        // 다른 시퀀스의 id를 참조하는 검사가 있으므로, id 수집을 먼저 전부 끝낸다
        foreach (string name in order) CollectIds(sequences[name], result);
        foreach (string name in order) ValidateSequence(sequences[name], sequences, defaultLanguage, env, result);

        if (result.Errors.Count > 0) return result;

        GenerateFiles(sequences, order, languages, result);
        result.SequenceCount = order.Count;
        return result;
    }

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
        Dictionary<string, SequenceModel> sequences, List<string> order, SheetConvertResult result)
    {
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

            SequenceModel sequence;
            if (!sequences.TryGetValue(sequenceName, out sequence))
            {
                sequence = new SequenceModel { Name = sequenceName };
                sequences[sequenceName] = sequence;
                order.Add(sequenceName);
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
                    WarnIfIgnoredColumns(row, sequenceName, "choice", result);
                    sequence.Lines[sequence.Lines.Count - 1].Choices.Add(ReadChoice(row, languages));
                    break;

                case "branch":
                    if (sequence.Lines.Count == 0)
                    {
                        AddError(result, "lines", row.Number, sequenceName, "branch 행 위에 line 행이 없습니다. branch는 바로 위 line 행에 붙습니다.");
                        break;
                    }
                    WarnIfIgnoredColumns(row, sequenceName, "branch", result);
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

    private static ChoiceModel ReadChoice(SheetRow row, List<string> languages)
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

        return choice;
    }

    private static void WarnIfIgnoredColumns(SheetRow row, string sequenceName, string type, SheetConvertResult result)
    {
        if (row.Get(ColId).Length > 0 || row.Get(ColSpeaker).Length > 0 || row.Get(ColExpression).Length > 0
            || row.Get(ColSlot).Length > 0 || row.Get(ColCommands).Length > 0 || row.Get(ColBgm).Length > 0)
        {
            AddWarning(result, "lines", row.Number, sequenceName,
                $"{type} 행에서는 id / speaker / expression / slot / commands / bgm 칸을 쓰지 않습니다. (무시됨)");
        }
    }

    private static void ReadMeta(string metaCsv, Dictionary<string, SequenceModel> sequences, SheetConvertResult result)
    {
        if (string.IsNullOrWhiteSpace(metaCsv)) return;

        Table meta = ReadTable(metaCsv, "meta", result);
        if (meta == null) return;

        foreach (string required in new[] { "sequence", "key", "value" })
        {
            if (!meta.Columns.Contains(required))
            {
                result.Errors.Add($"[meta 탭] 필수 열 '{required}'이(가) 없습니다. (1행은 sequence / key / value)");
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
                    $"알 수 없는 key '{row.Get("key")}'입니다. (bgm, chapterId, cast, onEnd.scene, onEnd.clearChapter, onEnd.unlockChapter, onEnd.songId, onEnd.difficulty)");
                continue;
            }

            SequenceModel sequence;
            if (!sequences.TryGetValue(sequenceName, out sequence))
            {
                AddWarning(result, "meta", row.Number, sequenceName, "lines 탭에 없는 시퀀스라 무시합니다.");
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
            if (line.Id.Length == 0) continue;

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
    }

    private static void ValidateSequence(SequenceModel sequence, Dictionary<string, SequenceModel> sequences,
        string defaultLanguage, IDialogueSheetEnvironment env, SheetConvertResult result)
    {
        ValidateMeta(sequence, env, result);

        foreach (LineModel line in sequence.Lines)
        {
            ValidateLine(sequence, line, defaultLanguage, env, result);

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
    }

    private static void ValidateMeta(SequenceModel sequence, IDialogueSheetEnvironment env, SheetConvertResult result)
    {
        MetaEntry entry;

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
            int slot;

            if (parts.Length < 2 || parts.Length > 3
                || parts[0].Trim().Length == 0
                || !TryParseNonNegative(parts[1].Trim(), out slot))
            {
                AddError(result, "meta", entry.Row, sequence.Name,
                    $"cast 항목 '{item}'의 형식이 올바르지 않습니다. 'speakerId:slot' 또는 'speakerId:slot:expression' (예: White:0;Black:1)");
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
                AddError(result, "meta", entry.Row, sequence.Name, $"cast에서 slot {slot}이(가) 두 번 쓰였습니다.");
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
        IDialogueSheetEnvironment env, SheetConvertResult result)
    {
        int row = line.Row.Number;

        if (GetText(line.Texts, defaultLanguage).Length == 0)
        {
            AddError(result, "lines", row, sequence.Name, $"text_{defaultLanguage} 칸이 비어 있습니다.");
        }

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
            foreach (string command in line.Commands)
            {
                CheckCommand(command, row, sequence.Name, result);
            }
        }

        ValidateSpeaker(sequence, line, env, result);
    }

    private static void ValidateSpeaker(SequenceModel sequence, LineModel line, IDialogueSheetEnvironment env, SheetConvertResult result)
    {
        int row = line.Row.Number;

        // 내레이션 (speaker 없음)
        if (line.Speaker.Length == 0)
        {
            if (line.Expression.Length > 0 || line.SlotText.Length > 0)
            {
                AddWarning(result, "lines", row, sequence.Name, "speaker가 없는 줄(내레이션)에서는 expression / slot을 쓰지 않습니다. (무시됨)");
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
            AddWarning(result, "lines", row, sequence.Name, $"'{line.Speaker}'의 expression이 비어 있어 기본 표정이 쓰입니다.");
        }
        else if (speakerExists && !env.SpeakerHasExpression(line.Speaker, line.Expression))
        {
            AddError(result, "lines", row, sequence.Name, $"'{line.Speaker}'에는 표정 '{line.Expression}'이(가) 없습니다.");
        }

        // 슬롯: 직접 적었으면 그 값, 비어 있으면 meta의 cast에서 가져온다
        if (line.SlotText.Length > 0)
        {
            int slot;
            if (TryParseNonNegative(line.SlotText, out slot)) line.Slot = slot;
            else AddError(result, "lines", row, sequence.Name, $"slot '{line.SlotText}'는 0 이상의 정수여야 합니다.");
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

        CheckCondition(choice.Condition, row, sequence.Name, result);
        CheckAssignments(choice.SetFlags, row, sequence.Name, result);

        // 다른 시퀀스로 분기
        if (choice.TargetSequence.Length > 0)
        {
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

    private static void CheckCommand(string command, int row, string sequenceName, SheetConvertResult result)
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
        if (name == "hide" && !TryParseNonNegative(arg, out slot))
        {
            AddError(result, "lines", row, sequenceName, $"hide 명령에는 슬롯 번호가 필요합니다. (예: hide:0) 입력값: '{command}'");
        }
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
        if (end.Count > 0) top.Add(Prop("onEnd", Obj(end)));

        // 대사 한 줄 = JSON 한 줄. git diff에서 시트의 한 행 변경이 한 줄 변경으로 보인다.
        var lineJsons = new List<string>();
        foreach (LineModel line in sequence.Lines)
        {
            lineJsons.Add(BuildLineJson(line, language, defaultLanguage, fallbackRows));
        }
        top.Add("\"lines\": [\n    " + string.Join(",\n    ", lineJsons.ToArray()) + "\n  ]");

        return "{\n  " + string.Join(",\n  ", top.ToArray()) + "\n}\n";
    }

    private static string BuildLineJson(LineModel line, string language, string defaultLanguage, List<int> fallbackRows)
    {
        var m = new List<string>();

        if (line.Id.Length > 0) m.Add(Prop("id", Str(line.Id)));
        if (line.Condition.Length > 0) m.Add(Prop("if", Str(line.Condition)));

        if (line.Speaker.Length > 0)
        {
            m.Add(Prop("speakerId", Str(line.Speaker)));
            if (line.Expression.Length > 0) m.Add(Prop("expression", Str(line.Expression)));
            m.Add(Prop("screenSlot", Num(line.Slot)));
        }

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

        if (line.Goto.Length > 0) m.Add(Prop("goto", Str(line.Goto)));

        if (line.Choices.Count > 0)
        {
            var choices = new List<string>();
            foreach (ChoiceModel choice in line.Choices)
            {
                var c = new List<string>();
                c.Add(Prop("text", Str(PickText(choice.Texts, language, defaultLanguage, choice.Row.Number, fallbackRows))));
                if (choice.Condition.Length > 0) c.Add(Prop("if", Str(choice.Condition)));
                if (choice.Goto.Length > 0) c.Add(Prop("goto", Str(choice.Goto)));
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

        if (language != defaultLanguage) fallbackRows.Add(rowNumber);
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

    /// <summary>시퀀스/언어/id 이름은 파일 이름이나 라벨로 쓰이므로 영문, 숫자, '-', '_' 만 허용한다.</summary>
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
        return $"[{tab} 탭 {row}행]{sequencePart} {message}";
    }
    #endregion
}