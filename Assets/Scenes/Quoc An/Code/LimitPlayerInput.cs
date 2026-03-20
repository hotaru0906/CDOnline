using UnityEngine;
using TMPro;

/// <summary>
/// Giới hạn input số người chơi từ 2-4
/// </summary>
public class LimitPlayerInput : MonoBehaviour
{
    private const int MIN_PLAYERS = 2;
    private const int MAX_PLAYERS = 4;
    
    private TMP_InputField _inputField;

    private void Awake()
    {
        _inputField = GetComponent<TMP_InputField>();
        _inputField.onValidateInput = ValidateInput;
        _inputField.text = MIN_PLAYERS.ToString();
    }

    private void OnEnable() => _inputField.onEndEdit.AddListener(OnEndEdit);
    private void OnDisable() => _inputField.onEndEdit.RemoveListener(OnEndEdit);

    private char ValidateInput(string text, int charIndex, char addedChar)
    {
        if (!char.IsDigit(addedChar))
            return '\0';

        var newText = text.Insert(charIndex, addedChar.ToString()).TrimStart('0');
        if (string.IsNullOrEmpty(newText)) 
            newText = "0";

        if (!int.TryParse(newText, out int value) || value < MIN_PLAYERS || value > MAX_PLAYERS)
            return '\0';

        return addedChar;
    }

    private void OnEndEdit(string value)
    {
        if (!int.TryParse(value, out int num))
        {
            _inputField.text = MIN_PLAYERS.ToString();
            return;
        }
        
        _inputField.text = Mathf.Clamp(num, MIN_PLAYERS, MAX_PLAYERS).ToString();
    }
}