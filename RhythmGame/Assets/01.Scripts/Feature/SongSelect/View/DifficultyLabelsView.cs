/// <summary>
/// 난이도 글자(Easy / Normal / Hard) 강조 표시만 담당한다. 채보가 없는 난이도는 흐리게 표시.
/// labels에는 Easy, Normal, Hard 순서로 넣는다.
/// </summary>
public class DifficultyLabelsView : LabelGroupView
{
    private void OnEnable() => controller.SelectionChanged += OnSelectionChanged;
    private void OnDisable() => controller.SelectionChanged -= OnSelectionChanged;

    private void OnSelectionChanged(SongDataSO song, Difficulty difficulty)
    {
        Highlight((int)difficulty, i => song != null && song.HasChart((Difficulty)i));
    }
}
