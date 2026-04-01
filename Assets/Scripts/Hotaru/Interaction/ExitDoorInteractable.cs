using UnityEngine;
using Fusion;
using UnityEngine.SceneManagement;

/// <summary>
/// Cửa để thoát khỏi phòng (Leave Room)
/// </summary>
public class ExitDoorInteractable : InteractableObject
{
    [Header("Door Settings")]
    [SerializeField] private string menuSceneName = "TestMenu";

    private void Start()
    {
        promptText = "Leave Room";
        interactionKey = KeyCode.E;
    }

    public override void Interact()
    {
        base.Interact();
        LeaveRoom();
    }

    private async void LeaveRoom()
    {
        Debug.Log("[ExitDoorInteractable] Leaving room...");
        
        // Re-enable input
        if (PlayerInputHandler.Instance != null)
        {
            PlayerInputHandler.Instance.InputEnabled = true;
        }
        
        // Find NetworkRunner and shutdown
        var runner = FindAnyObjectByType<NetworkRunner>();
        if (runner != null)
        {
            await runner.Shutdown();
            SceneManager.LoadScene(menuSceneName);
        }
        else
        {
            Debug.LogError("[ExitDoorInteractable] Cannot find NetworkRunner!");
            // Fallback: just load menu scene
            SceneManager.LoadScene(menuSceneName);
        }
    }
}
