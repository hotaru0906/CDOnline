using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChestUI : MonoBehaviour
{
    public static ChestUI Instance;

    [Header("UI")]
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text keyText;
    [SerializeField] private Button openButton;
    [SerializeField] private Button cancelButton;

    private void Awake()
    {
        Debug.Log("[ChestUI] Awake");

        Instance = this;

        cancelButton.onClick.AddListener(OnCancelClicked);

        openButton.onClick.AddListener(OnOpenClicked);

        root.SetActive(false);
    }

    public void Show(int currentKeys)
    {
        root.SetActive(true);

        keyText.text =
            $"Need 5 Keys\n\nYou have {currentKeys} Keys";
    }

    public void Hide()
    {
        root.SetActive(false);
    }

    private void OnCancelClicked()
    {
        BoardChestManager.Instance.RPC_EndInteraction();
    }

    private void OnOpenClicked()
    {
        BoardChestManager.Instance.RPC_OpenChest();
    }
}