using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Gắn script này lên PlayerRow prefab.
/// Nhấn nút "Setup Layout" trong Inspector để tự động
/// tạo và định vị tất cả các child đúng vị trí.
/// Sau khi setup xong có thể xóa script này khỏi prefab.
/// </summary>
[ExecuteInEditMode]
public class PlayerRowSetup : MonoBehaviour
{
    [Header("Nhấn nút bên dưới để setup layout")]
    public bool setupNow = false;

    void OnValidate()
    {
        if (!setupNow) return;
        setupNow = false;
        SetupLayout();
    }

    void SetupLayout()
    {
        RectTransform root = GetComponent<RectTransform>();

        // ── Root ─────────────────────────────────────────────
        root.anchorMin = new Vector2(0, 0);
        root.anchorMax = new Vector2(1, 0);
        root.pivot     = new Vector2(0.5f, 0.5f);
        root.offsetMin = new Vector2(0, 0);
        root.offsetMax = new Vector2(0, 70);

        // ── Avatar ───────────────────────────────────────────
        RectTransform avatar = GetOrCreate<Image>("Avatar", root);
        avatar.anchorMin = new Vector2(0, 0.5f);
        avatar.anchorMax = new Vector2(0, 0.5f);
        avatar.pivot     = new Vector2(0, 0.5f);
        avatar.anchoredPosition = new Vector2(10, 0);
        avatar.sizeDelta = new Vector2(50, 50);
        var avatarImg = avatar.GetComponent<Image>();
        avatarImg.preserveAspect = true;

        // Corner radius via outline nếu muốn round — dùng Image type Simple
        avatarImg.type = Image.Type.Simple;

        // ── PlayerNameText ────────────────────────────────────
        RectTransform nameT = GetOrCreate<TextMeshProUGUI>("PlayerNameText", root);
        nameT.anchorMin = new Vector2(0, 0.5f);
        nameT.anchorMax = new Vector2(1, 1);
        nameT.pivot     = new Vector2(0, 1);
        nameT.offsetMin = new Vector2(70, 0);
        nameT.offsetMax = new Vector2(-70, 0);
        var nameTmp = nameT.GetComponent<TextMeshProUGUI>();
        nameTmp.fontSize        = 16;
        nameTmp.fontStyle       = FontStyles.Bold;
        nameTmp.alignment       = TextAlignmentOptions.MidlineLeft;
        nameTmp.overflowMode    = TextOverflowModes.Truncate;
        nameTmp.enableAutoSizing = false;

        // ── StatusText ────────────────────────────────────────
        RectTransform statusT = GetOrCreate<TextMeshProUGUI>("StatusText", root);
        statusT.anchorMin = new Vector2(0, 0);
        statusT.anchorMax = new Vector2(1, 0.5f);
        statusT.pivot     = new Vector2(0, 0);
        statusT.offsetMin = new Vector2(70, 0);
        statusT.offsetMax = new Vector2(-70, 0);
        var statusTmp = statusT.GetComponent<TextMeshProUGUI>();
        statusTmp.fontSize        = 13;
        statusTmp.fontStyle       = FontStyles.Normal;
        statusTmp.alignment       = TextAlignmentOptions.MidlineLeft;
        statusTmp.overflowMode    = TextOverflowModes.Truncate;
        statusTmp.enableAutoSizing = false;
        statusTmp.color = new Color(0.97f, 0.44f, 0.44f);
        statusTmp.text  = "Not Ready";

        // ── HostBadge ─────────────────────────────────────────
        RectTransform badge = GetOrCreate<Image>("HostBadge", root);
        badge.anchorMin = new Vector2(1, 0.5f);
        badge.anchorMax = new Vector2(1, 0.5f);
        badge.pivot     = new Vector2(1, 0.5f);
        badge.anchoredPosition = new Vector2(-10, 0);
        badge.sizeDelta = new Vector2(55, 24);
        var badgeImg = badge.GetComponent<Image>();
        badgeImg.color = new Color(0.06f, 0.43f, 0.33f);

        // HostText bên trong badge
        RectTransform hostText = GetOrCreate<TextMeshProUGUI>("HostText", badge);
        hostText.anchorMin = Vector2.zero;
        hostText.anchorMax = Vector2.one;
        hostText.offsetMin = Vector2.zero;
        hostText.offsetMax = Vector2.zero;
        var hostTmp = hostText.GetComponent<TextMeshProUGUI>();
        hostTmp.text      = "HOST";
        hostTmp.fontSize  = 11;
        hostTmp.fontStyle = FontStyles.Bold;
        hostTmp.alignment = TextAlignmentOptions.Center;
        hostTmp.color     = Color.white;

        Debug.Log("[PlayerRowSetup] Layout setup hoàn tất!");

        // Đánh dấu prefab dirty để Unity lưu
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(gameObject);
#endif
    }

    RectTransform GetOrCreate<T>(string childName, RectTransform parent) where T : Component
    {
        Transform existing = parent.Find(childName);
        if (existing != null) return existing.GetComponent<RectTransform>();

        GameObject go = new GameObject(childName);
        go.transform.SetParent(parent, false);
        go.AddComponent<T>();
        return go.GetComponent<RectTransform>();
    }
}