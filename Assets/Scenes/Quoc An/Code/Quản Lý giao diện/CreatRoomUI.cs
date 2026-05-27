using UnityEngine;

public class CreateRoomUI : MonoBehaviour
{
    public void OnClick_Back()
    {
        UIManager.Instance.NavigateBack(); // Về PlayOnline
    }

    // --- Code tạo phòng cũ của bạn ---
    public void OnClick_CreateRoom()
    {
        // code photon / mirror cũ...
    }
}