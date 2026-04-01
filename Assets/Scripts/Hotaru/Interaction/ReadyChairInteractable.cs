using UnityEngine;
using Fusion;

/// <summary>
/// Ghế để ngồi và ready game trong Lobby
/// </summary>
public class ReadyChairInteractable : InteractableObject
{
    [Header("Chair Settings")]
    [SerializeField] private Transform sitPosition;
    [SerializeField] private bool autoReady = true;
    
    [Header("Animation")]
    [SerializeField] private string sitAnimationTrigger = "Sit";
    [SerializeField] private string standAnimationTrigger = "Stand";
    
    private PlayerNetworkData _seatedPlayer;
    private bool _isOccupied = false;

    private void Start()
    {
        promptText = "Sit & Ready";
        interactionKey = KeyCode.E;
        
        if (sitPosition == null)
            sitPosition = transform;
    }

    public override bool CanInteract()
    {
        if (!base.CanInteract()) return false;
        
        // Nếu ghế đã có người ngồi
        if (_isOccupied && _seatedPlayer != null)
        {
            // Chỉ player đang ngồi mới có thể đứng dậy
            var localPlayer = GetLocalPlayer();
            return localPlayer != null && localPlayer == _seatedPlayer;
        }
        
        return true;
    }

    public override void Interact()
    {
        base.Interact();
        
        if (_isOccupied)
            StandUp();
        else
            SitDown();
    }

    private void SitDown()
    {
        var player = GetLocalPlayer();
        if (player == null) return;
        
        _isOccupied = true;
        _seatedPlayer = player;
        
        // Set player position to chair
        var playerController = player.GetComponent<PlayerController>();
        if (playerController != null)
        {
            // Disable movement
            playerController.SetMovementEnabled(false);
            
            // Teleport to sit position
            playerController.transform.position = sitPosition.position;
            playerController.transform.rotation = sitPosition.rotation;
        }
        
        // Play sit animation
        var animator = player.GetComponentInChildren<Animator>();
        if (animator != null && !string.IsNullOrEmpty(sitAnimationTrigger))
        {
            animator.SetTrigger(sitAnimationTrigger);
        }
        
        // Auto set ready
        if (autoReady)
        {
            player.SetReady(true);
        }
        
        // Update prompt text
        promptText = "Stand Up";
        
        Debug.Log($"[ReadyChairInteractable] {player.PlayerName} sat down. Ready: {autoReady}");
    }

    private void StandUp()
    {
        if (_seatedPlayer == null) return;
        
        var player = _seatedPlayer;
        
        // Enable movement
        var playerController = player.GetComponent<PlayerController>();
        if (playerController != null)
        {
            playerController.SetMovementEnabled(true);
            
            // Move player slightly forward so they don't overlap with chair
            playerController.transform.position = sitPosition.position + sitPosition.forward * 0.5f;
        }
        
        // Play stand animation
        var animator = player.GetComponentInChildren<Animator>();
        if (animator != null && !string.IsNullOrEmpty(standAnimationTrigger))
        {
            animator.SetTrigger(standAnimationTrigger);
        }
        
        // Unready when stand
        if (autoReady)
        {
            player.SetReady(false);
        }
        
        _isOccupied = false;
        _seatedPlayer = null;
        
        // Reset prompt text
        promptText = "Sit & Ready";
        
        Debug.Log($"[ReadyChairInteractable] {player.PlayerName} stood up.");
    }

    private PlayerNetworkData GetLocalPlayer()
    {
        // Tìm local player
        foreach (var player in FindObjectsByType<PlayerNetworkData>(FindObjectsSortMode.None))
        {
            if (player.Object != null && player.Object.HasInputAuthority)
            {
                return player;
            }
        }
        return null;
    }

    /// <summary>
    /// Kiểm tra ghế có người ngồi không
    /// </summary>
    public bool IsOccupied => _isOccupied;

    /// <summary>
    /// Player đang ngồi
    /// </summary>
    public PlayerNetworkData SeatedPlayer => _seatedPlayer;
}
