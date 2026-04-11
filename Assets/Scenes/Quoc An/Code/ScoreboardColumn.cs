using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ScoreboardColumn : MonoBehaviour
{
    [Header("=== UI References ===")]
    [SerializeField] private RectTransform   barContainer;  // Bar_Container
    [SerializeField] private Image           barImage;      // Bar (con của Container)
    [SerializeField] private Image           iconImage;     // Icon (con của Container)
    [SerializeField] private TextMeshProUGUI scoreText;     // Score_Text
    [SerializeField] private TextMeshProUGUI nameText;      // Name_Text

    // Private
    private float _targetHeight;
    private float _currentHeight;
    private float _animSpeed = 3f;
    private bool  _isSetup   = false;

    // ==============================
    //         UNITY EVENTS
    // ==============================
    private void Awake()
    {
        AutoFindReferences();

        // ✅ Bắt đầu từ chiều cao 0
        SetContainerHeight(0f);
    }

    private void Update()
    {
        if (!_isSetup) return;

        // Lerp mượt lên target
        if (Mathf.Abs(_currentHeight - _targetHeight) > 0.5f)
        {
            _currentHeight = Mathf.Lerp(
                _currentHeight,
                _targetHeight,
                Time.deltaTime * _animSpeed
            );
        }
        else
        {
            _currentHeight = _targetHeight;
        }

        SetContainerHeight(_currentHeight);
    }

    // ==============================
    //     TỰ ĐỘNG TÌM REFERENCES
    // ==============================
    private void AutoFindReferences()
    {
        // Tìm Bar_Container
        if (barContainer == null)
        {
            Transform t = transform.Find("Bar_Container");
            if (t != null) barContainer = t.GetComponent<RectTransform>();
        }

        // Tìm Bar (con của Bar_Container)
        if (barImage == null && barContainer != null)
        {
            Transform t = barContainer.Find("Bar");
            if (t != null) barImage = t.GetComponent<Image>();
        }

        // Tìm Icon (con của Bar_Container)
        if (iconImage == null && barContainer != null)
        {
            Transform t = barContainer.Find("Icon");
            if (t != null) iconImage = t.GetComponent<Image>();
        }

        // Tìm Score_Text
        if (scoreText == null)
        {
            Transform t = transform.Find("Score_Text");
            if (t != null) scoreText = t.GetComponent<TextMeshProUGUI>();
        }

        // Tìm Name_Text
        if (nameText == null)
        {
            Transform t = transform.Find("Name_Text");
            if (t != null) nameText = t.GetComponent<TextMeshProUGUI>();
        }

        // Log
        Debug.Log($"[{gameObject.name}] References:\n" +
                  $"  barContainer: {(barContainer != null ? "✅" : "❌ NULL")}\n" +
                  $"  barImage:     {(barImage     != null ? "✅" : "❌ NULL")}\n" +
                  $"  iconImage:    {(iconImage    != null ? "✅" : "❌ NULL")}\n" +
                  $"  scoreText:    {(scoreText    != null ? "✅" : "❌ NULL")}\n" +
                  $"  nameText:     {(nameText     != null ? "✅" : "❌ NULL")}");
    }

    // ==============================
    //         PUBLIC API
    // ==============================
    public void Setup(
        string playerName,
        Sprite icon,
        Color  barColor,
        float  targetHeight,
        float  animSpeed = 3f)
    {
        AutoFindReferences();

        _targetHeight = targetHeight;
        _animSpeed    = animSpeed;
        _isSetup      = true;

        if (nameText  != null) nameText.text  = playerName;
        if (barImage  != null) barImage.color = barColor;

        if (iconImage != null)
        {
            iconImage.gameObject.SetActive(icon != null);
            if (icon != null) iconImage.sprite = icon;
        }
    }

    public void SetScore(int score)
    {
        if (scoreText != null)
            scoreText.text = score.ToString("N0");
    }

    public void SetTargetHeight(float height)
    {
        _targetHeight = height;
    }

    // ==============================
    //      SET CHIỀU CAO CỘT
    // ==============================
    private void SetContainerHeight(float height)
    {
        if (barContainer == null) return;

        // ✅ Chỉ thay đổi chiều cao của Bar_Container
        // Pivot Y = 0 → sẽ tăng từ dưới lên
        barContainer.sizeDelta = new Vector2(
            barContainer.sizeDelta.x,
            height
        );
    }
}