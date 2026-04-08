using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameplaySettings : MonoBehaviour
{
    [Header("=== Mouse Sensitivity ===")]
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private TextMeshProUGUI sensitivityValueText;

    [Header("=== Invert Y-Axis ===")]
    [SerializeField] private Toggle invertYToggle;

    [Header("=== Language ===")]
    [SerializeField] private TMP_Dropdown languageDropdown;

    // Static để các script khác có thể truy cập
    public static float MouseSensitivity { get; private set; } = 3f;
    public static bool IsInvertY { get; private set; } = false;

    // ==============================
    //         UNITY EVENTS
    // ==============================
    private void Start()
    {
        LoadGameplaySettings();
        BindEvents();
    }

    private void BindEvents()
    {
        sensitivitySlider.onValueChanged.AddListener(SetSensitivity);
        invertYToggle.onValueChanged.AddListener(SetInvertY);
        languageDropdown.onValueChanged.AddListener(SetLanguage);
    }

    // ==============================
    //        SET FUNCTIONS
    // ==============================
    public void SetSensitivity(float value)
    {
        MouseSensitivity = value;
        sensitivityValueText.text = value.ToString("F1"); // VD: "3.5"
        PlayerPrefs.SetFloat("Sensitivity", value);
    }

    public void SetInvertY(bool isOn)
    {
        IsInvertY = isOn;
        PlayerPrefs.SetInt("InvertY", isOn ? 1 : 0);
    }

    public void SetLanguage(int index)
    {
        // Tích hợp với Localization System của Unity
        string[] langCodes = { "vi", "en", "ja" };
        if (index < langCodes.Length)
        {
            PlayerPrefs.SetInt("Language", index);
            Debug.Log("Language changed to: " + langCodes[index]);
            // TODO: Gọi Unity Localization Package ở đây
        }
    }

    // ==============================
    //        SAVE / LOAD
    // ==============================
    private void LoadGameplaySettings()
    {
        float sensitivity = PlayerPrefs.GetFloat("Sensitivity", 3f);
        bool invertY      = PlayerPrefs.GetInt("InvertY", 0) == 1;
        int language      = PlayerPrefs.GetInt("Language", 0);

        sensitivitySlider.value   = sensitivity;
        invertYToggle.isOn        = invertY;
        languageDropdown.value    = language;
    }

    public void ResetGameplay()
    {
        sensitivitySlider.value = 3f;
        invertYToggle.isOn      = false;
        languageDropdown.value  = 0;
    }
}