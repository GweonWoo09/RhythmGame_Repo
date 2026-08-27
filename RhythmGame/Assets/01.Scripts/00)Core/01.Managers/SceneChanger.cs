using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneChanger : MonoBehaviour
{
    public static SceneChanger instance;

    public enum GameState
    {
        Loading,
        Lobby,
        ChapterSelect,
        SongSelect,
        InGame,
        Result,
        Setting
    }

    public GameState currentState = GameState.Loading;

    private void Awake()
    {
        if (instance == null)
            instance = this;

        else if (instance != null)
            Destroy(this.gameObject);
    }

    private void Start()
    {
        SceneManager.LoadScene(currentState.ToString());
    }

    public void ChangeScene(GameState state)
    {
        Debug.Log("Change to \"" + state + "\" scene.");
        SceneManager.LoadScene(state.ToString());
    }
}
