using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;
using TMPro;

public class CustomizationManager : MonoBehaviour
{
    private const int HIGH_PRIORITY = 10;
    private const int LOW_PRIORITY = 0;
    private const int CHARACTER_COUNT = 4;

    [Header("UI")]
    [SerializeField] private Button nextCharacterButton;
    [SerializeField] private Button previousCharacterButton;
    [SerializeField] private Button backButton;
    [SerializeField] private TMP_InputField playerNameInput;

    [Header("Cameras")]
    [SerializeField] private CinemachineCamera camMain;
    [SerializeField] private CinemachineCamera[] characterCameras; // 4 cameras cho 4 nhân vật

    private int _currentCharacterIndex;
    private bool _isActive;

    public System.Action OnBackToMenu;

    private void Start()
    {
        nextCharacterButton.onClick.AddListener(ShowNextCharacter);
        previousCharacterButton.onClick.AddListener(ShowPreviousCharacter);
        
        if (backButton != null)
            backButton.onClick.AddListener(BackToMenu);

        if (playerNameInput != null)
            playerNameInput.onEndEdit.AddListener(OnPlayerNameChanged);

        // Load saved data từ PlayerPrefs
        _currentCharacterIndex = CharacterSelectionData.SelectedCharacterIndex;
        if (playerNameInput != null)
            playerNameInput.text = CharacterSelectionData.PlayerName;
    }

    /// <summary>
    /// Kích hoạt màn hình chọn nhân vật - chuyển sang camera nhân vật đã chọn
    /// </summary>
    public void Activate()
    {
        _isActive = true;
        
        // Load current selection từ PlayerPrefs
        _currentCharacterIndex = CharacterSelectionData.SelectedCharacterIndex;
        if (playerNameInput != null)
            playerNameInput.text = CharacterSelectionData.PlayerName;
        
        SwitchToCharacterCamera(_currentCharacterIndex);
    }

    /// <summary>
    /// Tắt màn hình chọn nhân vật - quay lại camera chính
    /// </summary>
    public void Deactivate()
    {
        _isActive = false;
        SwitchToMainCamera();
    }

    private void ShowNextCharacter()
    {
        if (!_isActive) return;
        
        _currentCharacterIndex = (_currentCharacterIndex + 1) % CHARACTER_COUNT;
        SwitchToCharacterCamera(_currentCharacterIndex);
    }

    private void ShowPreviousCharacter()
    {
        if (!_isActive) return;
        
        _currentCharacterIndex = (_currentCharacterIndex - 1 + CHARACTER_COUNT) % CHARACTER_COUNT;
        SwitchToCharacterCamera(_currentCharacterIndex);
    }

    private void BackToMenu()
    {
        SaveSelection();
        Deactivate();
        OnBackToMenu?.Invoke();
    }

    private void OnPlayerNameChanged(string newName)
    {
        CharacterSelectionData.PlayerName = newName;
    }

    private void SaveSelection()
    {
        CharacterSelectionData.SelectedCharacterIndex = _currentCharacterIndex;
        Debug.Log($"[CustomizationManager] Saved: Character {_currentCharacterIndex}, Name: {CharacterSelectionData.PlayerName}");
    }

    private void SwitchToCharacterCamera(int index)
    {
        if (characterCameras == null || characterCameras.Length == 0) return;

        // Set main camera low priority
        if (camMain != null)
            camMain.Priority = LOW_PRIORITY;

        // Set all character cameras low, then set selected one high
        for (int i = 0; i < characterCameras.Length; i++)
        {
            if (characterCameras[i] != null)
                characterCameras[i].Priority = (i == index) ? HIGH_PRIORITY : LOW_PRIORITY;
        }
    }

    private void SwitchToMainCamera()
    {
        // Set main camera high priority
        if (camMain != null)
            camMain.Priority = HIGH_PRIORITY;

        // Set all character cameras low
        if (characterCameras != null)
        {
            foreach (var cam in characterCameras)
            {
                if (cam != null)
                    cam.Priority = LOW_PRIORITY;
            }
        }
    }

    public int GetSelectedCharacterIndex() => _currentCharacterIndex;
}