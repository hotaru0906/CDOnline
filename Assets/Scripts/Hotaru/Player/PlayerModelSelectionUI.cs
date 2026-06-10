using UnityEngine;
using UnityEngine.UI;

public class PlayerModelSelectionUI : MonoBehaviour
{
    [Header("Model Switcher")]
    [Tooltip("PlayerModelSwitcher component of the player to control.")]
    [SerializeField] private PlayerModelSwitcher targetModelSwitcher;

    [Header("UI Buttons")]
    [Tooltip("Buttons for selecting models by index.")]
    [SerializeField] private Button[] modelButtons;

    private void Awake()
    {
        if (targetModelSwitcher == null)
        {
            targetModelSwitcher = FindObjectOfType<PlayerModelSwitcher>();
        }
    }

    private void Start()
    {
        if (targetModelSwitcher == null)
        {
            Debug.LogWarning("[PlayerModelSelectionUI] Không tìm thấy PlayerModelSwitcher.");
            enabled = false;
            return;
        }

        if (modelButtons == null || modelButtons.Length == 0)
        {
            Debug.LogWarning("[PlayerModelSelectionUI] Chưa gán button model.");
            return;
        }

        for (int i = 0; i < modelButtons.Length; i++)
        {
            int index = i;
            if (modelButtons[index] != null)
            {
                modelButtons[index].onClick.AddListener(() => SelectModel(index));
            }
        }
    }

    private void SelectModel(int index)
    {
        targetModelSwitcher.SetCharacterModel(index);
        Debug.Log($"[PlayerModelSelectionUI] Chọn model index {index}");
    }
}
