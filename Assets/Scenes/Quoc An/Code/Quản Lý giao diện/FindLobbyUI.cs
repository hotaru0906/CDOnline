using UnityEngine;
using UnityEngine.UI;

public class FindLobbyUI : MonoBehaviour
{
    [Header("--- BUTTONS ---")]
    public Button createGameButton;
    public Button backButton;

    void Start()
    {
        if (createGameButton != null)
            createGameButton.onClick.AddListener(OnClick_CreateGame);
        else
            Debug.LogWarning("[FindLobby] createGameButton chưa được gán!");

        if (backButton != null)
            backButton.onClick.AddListener(OnClick_Back);
        else
            Debug.LogWarning("[FindLobby] backButton chưa được gán!");
    }

    void OnClick_CreateGame()
    {
        Debug.Log("[FindLobby] Chuyển sang CreateRoom.");

        if (UIManager.Instance != null)
            UIManager.Instance.NavigateTo(UIManager.Instance.UICreateRoom);
        else
            Debug.LogWarning("[FindLobby] UIManager.Instance là null!");
    }

    void OnClick_Back()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.NavigateBack();
    }
}