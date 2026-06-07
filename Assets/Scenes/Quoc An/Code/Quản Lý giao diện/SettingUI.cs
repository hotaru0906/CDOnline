using UnityEngine;

public class SettingUI : MonoBehaviour
{
    // Giữ nguyên code setting cũ của bạn
    // Chỉ thêm hàm Back

    public void OnClick_Back()
    {
        UIManager.Instance.NavigateBack(); // Tự động về Menu
    }

    // --- Code Setting cũ của bạn vẫn dùng bình thường ---
    public void OnVolumeChange(float value)
    {
        // code cũ...
    }
}