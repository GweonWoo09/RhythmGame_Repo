using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    public enum GameState
    {
        Loading,
        Lobby,
        ChapterSelect,
        SongSelect,
        InGame,
        Result,
        Setting,
        Story
    }

    public GameState currentState = GameState.Loading;

    public Dictionary<int, bool> chapterDict = new Dictionary<int, bool>();

    private void Awake()
    {
        if(instance == null)
            instance = this;
        else
            Destroy(instance);
    }


}
