using UnityEngine;

/// Script này xử lý việc đóng panel room với animation
public class RoomPanelManager : MonoBehaviour
{
    public void CloseRoomPanel()
    {
        panelroom panel = GetComponent<panelroom>();
        if (panel != null)
        {
            panel.ClosePanel();
        }
        else
        {
            Debug.LogError("Không tìm thấy panelroom component!");
        }
    }
}
