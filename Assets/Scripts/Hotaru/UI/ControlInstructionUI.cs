using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI item for displaying a single control instruction
/// </summary>
public class ControlInstructionUI : MonoBehaviour
{
    [SerializeField] private Image buttonIcon;
    [SerializeField] private TMP_Text keyNameText;
    [SerializeField] private TMP_Text actionText;

    public void Setup(ControlInstruction instruction)
    {
        if (instruction == null) return;

        if (buttonIcon != null && instruction.buttonIcon != null)
        {
            buttonIcon.sprite = instruction.buttonIcon;
            buttonIcon.gameObject.SetActive(true);
        }
        else if (buttonIcon != null)
        {
            buttonIcon.gameObject.SetActive(false);
        }

        if (keyNameText != null)
        {
            keyNameText.text = instruction.keyName;
            keyNameText.gameObject.SetActive(!string.IsNullOrEmpty(instruction.keyName));
        }

        if (actionText != null)
        {
            actionText.text = instruction.actionText;
        }
    }
}
