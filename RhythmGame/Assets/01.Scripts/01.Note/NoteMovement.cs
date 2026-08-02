using UnityEngine;

public class NoteMovement : MonoBehaviour
{
    public float noteSpeed = 1f;
    
    void Start()
    {
        
    }

    void Update()
    {
        transform.localPosition = Vector3.down * noteSpeed * Time.deltaTime;
    }
}
