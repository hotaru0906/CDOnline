using UnityEngine;

[CreateAssetMenu(menuName = "Game/Minigame Data")]
public class MinigameData : ScriptableObject
{
    [Header("Basic Info")]
    public string minigameName;
    public string sceneName;
    public Sprite icon;

    [TextArea]
    public string description;

    [Header("Player Settings")]
    public int minPlayers = 2;
    public int maxPlayers = 8;

    [Header("Camera Settings")]
    [Tooltip("Nếu true: Dùng shared camera cho tất cả player. Nếu false: Mỗi player có camera riêng")]
    public bool useSharedCamera = false;

    [Header("Game Settings")]
    [Tooltip("Thời gian chơi minigame (giây). 0 = không giới hạn")]
    public float timeLimit = 60f;
    
    [Tooltip("Có hiện tutorial trước khi chơi không")]
    public bool showTutorial = true;
}