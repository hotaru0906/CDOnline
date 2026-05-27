using UnityEngine;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("Các Panel")]
    public CanvasGroup UIMenu;
    public CanvasGroup UISetting;
    public CanvasGroup UIPlayOnline;
    public CanvasGroup UICreateRoom;
    public CanvasGroup UIFindLobby;

    // Theo dõi panel hiện tại trực tiếp - KHÔNG dùng GetCurrentPanel() nữa
    private CanvasGroup currentPanel;
    private Stack<CanvasGroup> history = new Stack<CanvasGroup>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        HideAll();
        // Mở Menu, đặt currentPanel = Menu
        ShowPanel(UIMenu);
        currentPanel = UIMenu;
    }

    // =====================
    // HÀM ĐIỀU HƯỚNG CHÍNH
    // =====================

    public void NavigateTo(CanvasGroup nextPanel)
    {
        if (nextPanel == null) return;
        if (nextPanel == currentPanel) return; // Tránh navigate tới chính nó

        // Lưu panel hiện tại vào history
        if (currentPanel != null)
        {
            history.Push(currentPanel);
            HidePanel(currentPanel);
        }

        // Hiện panel mới
        ShowPanel(nextPanel);
        currentPanel = nextPanel; // Cập nhật panel hiện tại
    }

    public void NavigateBack()
    {
        if (history.Count == 0) return;

        // Ẩn panel hiện tại
        if (currentPanel != null)
            HidePanel(currentPanel);

        // Lấy panel trước
        CanvasGroup previous = history.Pop();
        ShowPanel(previous);
        currentPanel = previous; // Cập nhật panel hiện tại
    }

    // =====================
    // HÀM TIỆN ÍCH
    // =====================

    void ShowPanel(CanvasGroup cg)
    {
        if (cg == null) return;
        cg.alpha = 1f;
        cg.interactable = true;
        cg.blocksRaycasts = true;
    }

    void HidePanel(CanvasGroup cg)
    {
        if (cg == null) return;
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;
    }

    void HideAll()
    {
        HidePanel(UIMenu);
        HidePanel(UISetting);
        HidePanel(UIPlayOnline);
        HidePanel(UICreateRoom);
        HidePanel(UIFindLobby);
    }
}