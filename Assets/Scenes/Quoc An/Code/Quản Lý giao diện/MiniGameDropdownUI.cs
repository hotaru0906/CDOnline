using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class MiniGameDropdownUI : MonoBehaviour
{
    [Header("Button hiển thị (label)")]
    [SerializeField] private Button labelButton;
    [SerializeField] private TextMeshProUGUI labelText;

    [Header("Panel list (ẩn mặc định)")]
    [SerializeField] private GameObject listPanel;
    [SerializeField] private Transform itemContainer;
    [SerializeField] private GameObject itemPrefab;

    [Header("Chế độ số lượng (1 → maxCount)")]
    [SerializeField] private bool useNumberRange = true;
    [SerializeField] private int maxCount = 10;

    [Header("Danh sách option tên (chỉ dùng khi useNumberRange = false)")]
    [SerializeField] private List<string> options = new List<string>();

    [Header("Placeholder khi chưa chọn")]
    [SerializeField] private string placeholder = "MinigameAmount";

    private bool isOpen = false;
    private string selectedValue = "";

    public string SelectedValue => string.IsNullOrEmpty(selectedValue) ? placeholder : selectedValue;

    void Start()
    {
        if (labelText != null)
            labelText.text = placeholder;

        // QUAN TRỌNG: Bật listPanel trước khi spawn items
        // để Instantiate vào parent active được
        if (listPanel != null)
            listPanel.SetActive(true);

        BuildItems();

        // Sau khi spawn xong mới ẩn
        if (listPanel != null)
            listPanel.SetActive(false);

        if (labelButton != null)
            labelButton.onClick.AddListener(ToggleList);
    }

    void BuildItems()
    {
        if (itemContainer == null || itemPrefab == null)
        {
            Debug.LogWarning("[MiniGameDropdown] Thiếu itemContainer hoặc itemPrefab!");
            return;
        }

        foreach (Transform child in itemContainer)
            Destroy(child.gameObject);

        List<string> finalOptions = new List<string>();

        if (useNumberRange)
        {
            for (int i = 1; i <= maxCount; i++)
                finalOptions.Add(i.ToString());
        }
        else
        {
            finalOptions.AddRange(options);
        }

        foreach (string option in finalOptions)
        {
            GameObject go = Instantiate(itemPrefab, itemContainer);
            go.SetActive(true); // đảm bảo item active

            TextMeshProUGUI text = go.GetComponentInChildren<TextMeshProUGUI>();
            Button btn = go.GetComponent<Button>();

            if (text != null) text.text = option;

            if (btn != null)
            {
                string captured = option;
                btn.onClick.AddListener(() => OnSelectItem(captured));
            }
        }
    }

    void ToggleList()
    {
        isOpen = !isOpen;
        if (listPanel != null)
            listPanel.SetActive(isOpen);
    }

    void OnSelectItem(string value)
    {
        selectedValue = value;
        if (labelText != null)
            labelText.text = value;

        isOpen = false;
        if (listPanel != null)
            listPanel.SetActive(false);
    }

    public void CloseList()
    {
        isOpen = false;
        if (listPanel != null)
            listPanel.SetActive(false);
    }

    public void ResetSelection()
    {
        selectedValue = "";
        if (labelText != null)
            labelText.text = placeholder;
        CloseList();
    }
}