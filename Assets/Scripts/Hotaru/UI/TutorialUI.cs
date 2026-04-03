using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;
using Fusion;

/// <summary>
/// Controller đơn giản cho Tutorial UI trong từng scene minigame
/// Mỗi scene MG tự setup canvas tutorial riêng, script này chỉ quản lý countdown và callback
/// </summary>
public class TutorialUI : MonoBehaviour
{
    [Header("Canvas")]
    [SerializeField] private GameObject canvasObject;

    [Header("Start Button (Optional)")]
    [SerializeField] private Button startButton;
    [SerializeField] private bool hostOnlyStart = true;

    private NetworkRunner _runner;

    public System.Action OnTutorialComplete;

    private void Awake()
    {
        if (canvasObject == null)
            canvasObject = gameObject;

        if (startButton != null)
            startButton.onClick.AddListener(OnStartClicked);
    }

    private void OnEnable()
    {
        _runner = FindAnyObjectByType<NetworkRunner>();
        UpdateStartButton();
    }

    private void OnStartClicked()
    {
        // Chỉ host mới được ấn start
        if (hostOnlyStart && _runner != null && !_runner.IsServer)
        {
            Debug.Log("[TutorialUI] Only host can start");
            CompleteTutorial();
            return;
        }
    }

    private void UpdateStartButton()
    {
        if (startButton == null) return;

        bool isHost = _runner != null && _runner.IsServer;

        if (hostOnlyStart)
        {
            startButton.gameObject.SetActive(isHost);
        }
    }

    private void CompleteTutorial()
    {
        Hide();
        OnTutorialComplete?.Invoke();
    }

    public void Show()
    {
        if (canvasObject != null)
        {
            canvasObject.SetActive(true);
        }
    }

    public void Hide()
    {
        if (canvasObject != null)
        {
            canvasObject.SetActive(false);
        }
    }
}
