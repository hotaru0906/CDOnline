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
        if (_inputField == null)
        {
            Debug.LogError("[LimitPlayerInput] TMP_InputField not found.");
            return;
        }

        _inputField.onValidateInput = ValidateInput;
        _inputField.text = MIN_PLAYERS.ToString();
    }

    private void OnEnable()
    {
        if (_inputField != null)
            _inputField.onEndEdit.AddListener(OnEndEdit);
    }

    private void OnDisable()
    {
        if (_inputField != null)
            _inputField.onEndEdit.RemoveListener(OnEndEdit);
    }

    public int GetValue()
    {
        if (_inputField == null)
            return MIN_PLAYERS;

        if (!int.TryParse(_inputField.text, out int value))
            return MIN_PLAYERS;

        return Mathf.Clamp(value, MIN_PLAYERS, MAX_PLAYERS);
    }

    public void SetValue(int value)
    {
        if (_inputField == null)
            return;

        _inputField.text = Mathf.Clamp(value, MIN_PLAYERS, MAX_PLAYERS).ToString();
    }

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