using UnityEngine;
using TMPro;

public class LimitPlayerInput : MonoBehaviour
{
    private TMP_InputField inputField;

    void Awake()
    {
        inputField = GetComponent<TMP_InputField>();
        // Gán hàm kiểm tra ký tự
        inputField.onValidateInput = ValidateInput;
        // Optional: đặt mặc định là 2 hoặc 4
        inputField.text = "2";
    }

    // Hàm này chạy mỗi khi người chơi gõ 1 ký tự
    private char ValidateInput(string text, int charIndex, char addedChar)
    {
        // Chỉ cho phép số 0-9
        if (!char.IsDigit(addedChar))
        {
            return '\0'; // \0 nghĩa là "không cho thêm ký tự này"
        }

        // Dự đoán text sau khi thêm ký tự mới
        string newText = text.Substring(0, charIndex) + addedChar + text.Substring(charIndex);

        // Xóa số 0 ở đầu (ví dụ: 02 → 2)
        newText = newText.TrimStart('0');
        if (string.IsNullOrEmpty(newText)) newText = "0";

        // Chuyển thành số để kiểm tra
        if (int.TryParse(newText, out int value))
        {
            // Nếu nhỏ hơn 2 hoặc lớn hơn 4 → không cho nhập
            if (value < 2 || value > 4)
            {
                return '\0';
            }
        }
        else
        {
            // Không parse được (trường hợp lạ) → chặn
            return '\0';
        }

        return addedChar; // Cho phép ký tự này
    }

    // Optional: Khi người dùng bỏ focus (End Edit) thì sửa lại giá trị cho chắc chắn
    void OnEnable()
    {
        inputField.onEndEdit.AddListener(OnEndEdit);
    }

    void OnDisable()
    {
        inputField.onEndEdit.RemoveListener(OnEndEdit);
    }

    private void OnEndEdit(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            inputField.text = "2";
            return;
        }

        if (int.TryParse(value, out int num))
        {
            if (num < 2) inputField.text = "2";
            else if (num > 4) inputField.text = "4";
            else inputField.text = num.ToString(); // loại bỏ số 0 thừa
        }
        else
        {
            inputField.text = "2";
        }
    }
}