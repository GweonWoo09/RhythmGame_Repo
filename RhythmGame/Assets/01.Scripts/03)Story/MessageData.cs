using UnityEngine;

[CreateAssetMenu(fileName = "Message", menuName = "ScriptableObject/MessageData")]
public class MessageData : ScriptableObject
{
    public string characterName;
    [TextArea]
    public string message;
}
