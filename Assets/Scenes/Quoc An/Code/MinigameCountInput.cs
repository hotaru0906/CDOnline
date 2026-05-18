using UnityEngine;
using TMPro; // Nếu dùng TextMeshPro
// using UnityEngine.UI; // Uncomment nếu dùng Legacy InputField

/// <summary>
/// Giới hạn số lượng minigame: chỉ nhập số, giá trị hợp lệ từ 5 đến 10.
/// Gắn script này vào GameObject chứa TMP_InputField.
/// </summary>
public class MinigameCountInput : MonoBehaviour
{
    [Header("UI Reference")]
    [SerializeField] private TMP_InputField inputField;
    // Nếu dùng Legacy UI: thay bằng InputField inputField;

    [Header("Validation Config")]
    [SerializeField] private int minValue = 5;
    [SerializeField] private int maxValue = 10;

    [Header("Debug (Read-only)")]
    [SerializeField] private int currentValidValue;

    // ─────────────────────────────────────────────
    // LIFECYCLE
    // ─────────────────────────────────────────────

    private void Awake()
    {
        // Bước 1: Validate reference
        // Lý do: Phát hiện lỗi thiếu tham chiếu sớm nhất có thể, tránh NullReferenceException lúc runtime.
        if (inputField == null)
        {
            Debug.LogError($"[MinigameCountInput] InputField chưa được gán trên {gameObject.name}.");
            enabled = false;
            return;
        }

        // Bước 2: Cấu hình InputField chỉ nhận ký tự số
        // Lý do: ContentType.IntegerNumber tự động chặn mọi ký tự không phải số và dấu '-'.
        //        Đây là cách Unity-native, không cần viết regex thủ công.
        inputField.contentType = TMP_InputField.ContentType.IntegerNumber;

        // Bước 3: Giới hạn số ký tự tối đa là 2 (vì max = 10, tối đa 2 chữ số)
        // Lý do: Ngăn người dùng nhập số vô nghĩa như "9999" trước khi validate.
        inputField.characterLimit = 2;

        // Bước 4: Đặt giá trị mặc định hợp lệ
        // Lý do: Đảm bảo trạng thái khởi tạo luôn hợp lệ, tránh trường hợp field trống gây lỗi logic.
        currentValidValue = minValue;
        inputField.text = minValue.ToString();
    }

    private void OnEnable()
    {
        // Bước 5: Đăng ký event listener
        // Lý do: onEndEdit kích hoạt khi người dùng bấm Enter hoặc click ra ngoài,
        //        đây là thời điểm hợp lý nhất để validate — không validate từng keystroke
        //        để tránh UX bị gián đoạn khi đang gõ dở.
        inputField.onEndEdit.AddListener(OnInputEndEdit);
    }

    private void OnDisable()
    {
        // Bước 6: Hủy đăng ký khi disabled
        // Lý do: Tránh memory leak và callback bị gọi khi object không còn active.
        inputField.onEndEdit.RemoveListener(OnInputEndEdit);
    }

    // ─────────────────────────────────────────────
    // VALIDATION LOGIC
    // ─────────────────────────────────────────────

    /// <summary>
    /// Được gọi khi người dùng kết thúc nhập liệu.
    /// </summary>
    private void OnInputEndEdit(string rawInput)
    {
        // Bước 7: Kiểm tra trường hợp rỗng
        // Lý do: int.TryParse("") trả về false, nhưng ta muốn thông báo rõ ràng.
        if (string.IsNullOrWhiteSpace(rawInput))
        {
            ApplyFallback("Vui lòng nhập một số.");
            return;
        }

        // Bước 8: Parse chuỗi sang số nguyên
        // Lý do: Dù contentType đã lọc ký tự, vẫn cần TryParse để xử lý an toàn
        //        (ví dụ: chuỗi chỉ có dấu '-').
        if (!int.TryParse(rawInput, out int parsedValue))
        {
            ApplyFallback("Giá trị không hợp lệ. Chỉ nhập số nguyên.");
            return;
        }

        // Bước 9: Kiểm tra khoảng giá trị [minValue, maxValue]
        // Lý do: Đây là nghiệp vụ cốt lõi — clamp về giá trị hợp lệ gần nhất
        //        thay vì chỉ báo lỗi, để UX mượt mà hơn.
        int clampedValue = Mathf.Clamp(parsedValue, minValue, maxValue);

        if (clampedValue != parsedValue)
        {
            Debug.LogWarning($"[MinigameCountInput] Giá trị {parsedValue} nằm ngoài [{minValue}, {maxValue}]. Tự động điều chỉnh về {clampedValue}.");
        }

        // Bước 10: Cập nhật UI và lưu giá trị hợp lệ
        currentValidValue = clampedValue;
        inputField.text = clampedValue.ToString();

        Debug.Log($"[MinigameCountInput] Số minigame hợp lệ: {currentValidValue}");
    }

    /// <summary>
    /// Khôi phục về giá trị hợp lệ cuối cùng kèm thông báo lý do.
    /// </summary>
    private void ApplyFallback(string reason)
    {
        Debug.LogWarning($"[MinigameCountInput] {reason} Khôi phục về: {currentValidValue}");
        inputField.text = currentValidValue.ToString();
    }

    // ─────────────────────────────────────────────
    // PUBLIC API
    // ─────────────────────────────────────────────

    /// <summary>
    /// Trả về số minigame đã được xác nhận hợp lệ.
    /// Dùng trong GameManager hoặc các hệ thống khác.
    /// </summary>
    public int GetMinigameCount() => currentValidValue;
}