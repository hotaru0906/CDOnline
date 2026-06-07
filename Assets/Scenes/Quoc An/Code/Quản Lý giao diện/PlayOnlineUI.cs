using UnityEngine;

public class PlayOnlineUI : MonoBehaviour
{
    public void OnClick_CreateRoom()
    {
        UIManager.Instance.NavigateTo(UIManager.Instance.UICreateRoom);
    }

    public void OnClick_FindLobby()
    {
        UIManager.Instance.NavigateTo(UIManager.Instance.UIFindLobby);
    }

    public void OnClick_Back()
    {
        UIManager.Instance.NavigateBack(); // Về Menu
    }
}