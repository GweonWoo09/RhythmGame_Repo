using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneChanger : MonoBehaviour
{




    public void ChangeScene(GameManager.GameState state)
    {
        Debug.Log("Change to \"" + state + "\" scene.");
        SceneManager.LoadScene(state.ToString());
    }
}
