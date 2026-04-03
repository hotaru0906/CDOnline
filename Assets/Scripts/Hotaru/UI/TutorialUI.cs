using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using System.Collections.Generic;
using TMPro;
using Fusion;

/// <summary>
/// Controller for Tutorial UI - shows before minigame starts
/// </summary>
public class TutorialUI : MonoBehaviour
{
    [Header("Canvas")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Minigame Info")]
    [SerializeField] private TMP_Text minigameNameText;
    [SerializeField] private TMP_Text descriptionText;

    [Header("Control Instructions")]
    [SerializeField] private Transform controlsContainer;
    [SerializeField] private ControlInstructionUI controlPrefab;

    [Header("Video Player")]
    [SerializeField] private RawImage videoDisplay;
    [SerializeField] private VideoPlayer videoPlayer;

    [Header("Player List")]
    [SerializeField] private Transform playerListContainer;
    [SerializeField] private TutorialPlayerStatusUI playerStatusPrefab;
    [SerializeField] private TMP_Text playerCountText;

    [Header("Start Button")]
    [SerializeField] private Button startButton;
    [SerializeField] private TMP_Text startButtonText;

    [Header("Settings")]
    [SerializeField] private bool hostOnlyStart = true;
    [SerializeField] private float updateInterval = 0.5f;

    private TutorialData _currentData;
    private List<ControlInstructionUI> _controlItems = new List<ControlInstructionUI>();
    private List<TutorialPlayerStatusUI> _playerStatusItems = new List<TutorialPlayerStatusUI>();
    private NetworkRunner _runner;
    private float _updateTimer;

    public System.Action OnTutorialComplete;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (startButton != null)
            startButton.onClick.AddListener(OnStartClicked);

        // Setup video player
        if (videoPlayer != null && videoDisplay != null)
        {
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.isLooping = true;
        }
    }

    private void OnEnable()
    {
        _runner = FindAnyObjectByType<NetworkRunner>();
        UpdatePlayerList();
        UpdateStartButton();
    }

    private void Update()
    {
        _updateTimer += Time.deltaTime;
        if (_updateTimer >= updateInterval)
        {
            _updateTimer = 0f;
            UpdatePlayerList();
            UpdateStartButton();
        }
    }

    /// <summary>
    /// Initialize tutorial with data
    /// </summary>
    public void Setup(TutorialData data)
    {
        _currentData = data;

        if (_currentData == null)
        {
            Debug.LogWarning("[TutorialUI] TutorialData is null!");
            return;
        }

        // Set minigame info
        if (minigameNameText != null)
            minigameNameText.text = _currentData.minigameName;

        if (descriptionText != null)
            descriptionText.text = _currentData.description;

        // Setup controls
        SetupControls();

        // Setup video
        SetupVideo();

        // Show UI
        Show();
    }

    private void SetupControls()
    {
        // Clear old items
        foreach (var item in _controlItems)
        {
            if (item != null)
                Destroy(item.gameObject);
        }
        _controlItems.Clear();

        if (_currentData.controls == null || controlPrefab == null || controlsContainer == null)
            return;

        // Create control items
        foreach (var control in _currentData.controls)
        {
            var item = Instantiate(controlPrefab, controlsContainer);
            item.Setup(control);
            _controlItems.Add(item);
        }
    }

    private void SetupVideo()
    {
        if (videoPlayer == null || _currentData.tutorialVideo == null)
        {
            if (videoDisplay != null)
                videoDisplay.gameObject.SetActive(false);
            return;
        }

        // Create render texture
        var renderTexture = new RenderTexture(1280, 720, 0);
        videoPlayer.targetTexture = renderTexture;
        videoDisplay.texture = renderTexture;

        videoPlayer.clip = _currentData.tutorialVideo;
        videoPlayer.Play();

        videoDisplay.gameObject.SetActive(true);
    }

    private void UpdatePlayerList()
    {
        if (_runner == null)
        {
            _runner = FindAnyObjectByType<NetworkRunner>();
            if (_runner == null) return;
        }

        var players = FindObjectsByType<PlayerNetworkData>(FindObjectsSortMode.None);

        // Update player count
        if (playerCountText != null)
        {
            playerCountText.text = $"Players: {players.Length}";
        }

        // Clear old items
        foreach (var item in _playerStatusItems)
        {
            if (item != null)
                Destroy(item.gameObject);
        }
        _playerStatusItems.Clear();

        if (playerStatusPrefab == null || playerListContainer == null)
            return;

        // Create player status items
        foreach (var player in players)
        {
            var item = Instantiate(playerStatusPrefab, playerListContainer);
            item.SetData(player);
            _playerStatusItems.Add(item);
        }
    }

    private void UpdateStartButton()
    {
        if (startButton == null) return;

        bool isHost = GameManager.Instance != null && GameManager.Instance.IsHost;

        if (hostOnlyStart)
        {
            startButton.interactable = isHost;

            if (startButtonText != null)
            {
                startButtonText.text = isHost ? "START" : "Waiting for Host...";
            }
        }
        else
        {
            startButton.interactable = true;

            if (startButtonText != null)
            {
                startButtonText.text = "READY";
            }
        }
    }

    private void OnStartClicked()
    {
        Debug.Log("[TutorialUI] Start clicked");

        if (hostOnlyStart && GameManager.Instance != null && !GameManager.Instance.IsHost)
        {
            Debug.LogWarning("[TutorialUI] Only host can start");
            return;
        }

        Hide(() =>
        {
            OnTutorialComplete?.Invoke();
        });
    }

    #region Show/Hide Methods
    public void Show(System.Action onComplete = null)
    {
        gameObject.SetActive(true);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        onComplete?.Invoke();
    }

    public void Hide(System.Action onComplete = null)
    {
        // Stop video
        if (videoPlayer != null)
        {
            videoPlayer.Stop();
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        gameObject.SetActive(false);
        onComplete?.Invoke();
    }
    #endregion

    private void OnDestroy()
    {
        // Clean up render texture
        if (videoPlayer != null && videoPlayer.targetTexture != null)
        {
            videoPlayer.targetTexture.Release();
            Destroy(videoPlayer.targetTexture);
        }

        if (startButton != null)
            startButton.onClick.RemoveListener(OnStartClicked);
    }
}
