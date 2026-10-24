using TMPro;
using UnityEngine;

// 결과 씬에서 마지막 플레이 결과를 화면에 표시한다.
public class ResultView : MonoBehaviour
{
    [SerializeField] private TMP_Text rank;
    [SerializeField] private TMP_Text score;
    [SerializeField] private TMP_Text accuracy;
    [SerializeField] private TMP_Text maxCombo;
    [SerializeField] private TMP_Text excellent;
    [SerializeField] private TMP_Text good;
    [SerializeField] private TMP_Text bad;
    [SerializeField] private TMP_Text miss;

    private void Start()
    {
        var result = GameManager.Instance.LastResult;
        if (result == null) return;

        rank.text = GameManager.Instance.CalculateRank(result.accuracy);
        score.text = result.score.ToString("N0");
        accuracy.text = result.accuracy.ToString("F2") + "%";
        maxCombo.text = result.maxCombo.ToString();
        excellent.text = result.perfectCount.ToString();
        good.text = result.greatCount.ToString();
        bad.text = result.goodCount.ToString();
        miss.text = result.missCount.ToString();
    }
}
