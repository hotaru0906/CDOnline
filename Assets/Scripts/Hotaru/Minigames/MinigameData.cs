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
    
    [Tooltip("Dùng Third Person Camera (nếu không dùng shared camera)")]
    public bool useThirdPersonCamera = true;

    [Header("Player Control Settings")]
    [Tooltip("Cho phép player di chuyển (WASD)")]
    public bool canMove = true;
    
    [Tooltip("Cho phép player nhảy (Space)")]
    public bool canJump = true;
    
    [Tooltip("Cho phép player cúi/crouch (C/Ctrl)")]
    public bool canCrouch = true;
    
    [Tooltip("Cho phép player tấn công (Left Click)")]
    public bool canAttack = true;
    
    [Tooltip("Cho phép player chạy (Shift)")]
    public bool canRun = true;

    [Header("Game Settings")]
    [Tooltip("Thời gian chơi minigame (giây). 0 = không giới hạn")]
    public float timeLimit = 60f;
    
    [Tooltip("Thời gian hiển thị tutorial trước khi bắt đầu game (giây)")]
    public float tutorialDuration = 5f;
    
    [Tooltip("Cho phép player respawn khi chết. Nếu false, player sẽ bị loại khi chết")]
    public bool allowRespawn = true;

    [Header("Audio Settings")]
    [Tooltip("BGM riêng cho minigame này. Nếu null sẽ dùng BGM mặc định từ AudioManager")]
    public AudioClip minigameBGM;
}