using UnityEngine;

[System.Serializable]
public class PlayerData
{
    public string playerName;
    public Sprite icon;

    public PlayerData(string name, Sprite icon)
    {
        this.playerName = name;
        this.icon = icon;
    }
}