/// <summary>
/// 탭 글자(all / story) 강조 표시만 담당한다.
/// labels에는 all, story 순서로 넣는다.
/// </summary>
public class CategoryLabelsView : LabelGroupView
{
    private void OnEnable() => controller.CategoryChanged += OnCategoryChanged;
    private void OnDisable() => controller.CategoryChanged -= OnCategoryChanged;

    private void OnCategoryChanged(SongCategory category) => Highlight((int)category);
}
