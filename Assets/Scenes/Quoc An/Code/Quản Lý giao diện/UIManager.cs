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
    public CanvasGroup UILobby;
    public CanvasGroup UIMiniGame; // ← Thêm mới

    public CanvasGroup UIBoardGame;

    public CanvasGroup UIInventory;


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
        ShowPanel(UIMenu);
        currentPanel = UIMenu;
    }

    public void NavigateTo(CanvasGroup nextPanel)
    {
        if (nextPanel == null) return;
        if (nextPanel == currentPanel) return;

        if (currentPanel != null)
        {
            history.Push(currentPanel);
            HidePanel(currentPanel);
        }

        ShowPanel(nextPanel);
        currentPanel = nextPanel;
    }

    public void NavigateBack()
    {
        if (history.Count == 0) return;

        if (currentPanel != null)
            HidePanel(currentPanel);

        CanvasGroup previous = history.Pop();
        ShowPanel(previous);
        currentPanel = previous;
    }

    public void QuitToPlayOnline()
    {
        if (currentPanel != null)
            HidePanel(currentPanel);

        history.Clear();
        ShowPanel(UIPlayOnline);
        currentPanel = UIPlayOnline;
        history.Push(UIMenu);
    }

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
        HidePanel(UILobby);
        HidePanel(UIMiniGame); // ← Thêm
        HidePanel(UIBoardGame); // ← Thêm
        HidePanel(UIInventory);
    }
}