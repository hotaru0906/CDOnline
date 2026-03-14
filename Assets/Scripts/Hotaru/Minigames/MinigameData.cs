using UnityEngine;

[CreateAssetMenu(menuName = "Game/Minigame Data")]
public class MinigameData : ScriptableObject
{
    public string minigameName;

    public string sceneName;

    public Sprite icon;

    [TextArea]
    public string description;

    public int minPlayers = 2;

    public int maxPlayers = 8;
}