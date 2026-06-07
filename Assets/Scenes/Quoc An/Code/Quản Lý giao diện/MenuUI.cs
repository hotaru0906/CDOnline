using UnityEngine;

public class MenuUI : MonoBehaviour
{
    // Kéo thả vào Inspector
    // hoặc dùng UIManager.Instance
    
    public void OnClick_PlayOnline()
    {
        UIManager.Instance.NavigateTo(UIManager.Instance.UIPlayOnline);
    }

    public void OnClick_Settings()
    {
        UIManager.Instance.NavigateTo(UIManager.Instance.UISetting);
    }

    public void OnClick_Quit()
    {
        Application.Quit();
    }
}