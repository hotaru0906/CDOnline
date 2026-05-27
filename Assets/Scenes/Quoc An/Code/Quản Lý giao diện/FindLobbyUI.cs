using UnityEngine;

public class FindLobbyUI : MonoBehaviour
{
    public void OnClick_Back()
    {
        UIManager.Instance.NavigateBack(); // Về PlayOnline
    }

    // --- Code tìm phòng cũ của bạn ---
    public void OnClick_Refresh()
    {
        // code cũ...
    }
}