using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryManager : MonoBehaviour
{
    [Header("Card Setup")]
    public CardData[] cards;           // Kéo 5 CardData asset vào đây
    public GameObject cardPrefab;      // Kéo Prefab Card vào đây
    public Transform handArea;         // Kéo HandArea vào đây

    [Header("Selected Info UI")]
    public TextMeshProUGUI selectedNameText;
    public TextMeshProUGUI selectedDescText;
    public Button useButton;

    [Header("Fan Settings")]
    public float[] rotationAngles = { -24f, -12f, 0f, 12f, 24f };
    public float[] yOffsets       = {  18f,   6f, 0f,  6f, 18f };
    public float cardSpacing = 60f;

    private CardUI[] cardUIs;
    private int selectedIndex = -1;
    private bool isOpen = false;

    void Start()
    {
        gameObject.SetActive(false);   // Đóng mặc định
        useButton.onClick.AddListener(UseSelectedCard);
        useButton.gameObject.SetActive(false);
        SpawnCards();
    }

    void SpawnCards()
    {
        cardUIs = new CardUI[cards.Length];

        for (int i = 0; i < cards.Length; i++)
        {
            GameObject obj = Instantiate(cardPrefab, handArea);
            CardUI ui = obj.GetComponent<CardUI>();
            ui.Setup(cards[i], i, this);
            cardUIs[i] = ui;
        }

        ArrangeFan();
    }

void ArrangeFan()
{
    int count = cardUIs.Length;

    // Khoảng cách ngang giữa các lá
    float totalWidth = (count - 1) * cardSpacing;
    float startX = -totalWidth / 2f;

    for (int i = 0; i < count; i++)
    {
        RectTransform rt = cardUIs[i].GetComponent<RectTransform>();

        // Pivot (0.5, 0) — xoay từ cạnh dưới
        rt.pivot = new Vector2(0.5f, 0f);

        // Vị trí gốc — xếp hàng ngang ở phía dưới HandArea
        float xPos = startX + i * cardSpacing;
        rt.localPosition = new Vector3(xPos, 0f, 0f);

        // Góc xoay: lá giữa thẳng, lá hai bên nghiêng ra
        rt.localRotation = Quaternion.Euler(0f, 0f, rotationAngles[i]);

        // Z-order: lá giữa hiện trên cùng
        rt.SetSiblingIndex(i);

        cardUIs[i].SaveBaseTransform();
    }
}

    public void OnCardClicked(int index)
    {
        // Bỏ chọn lá cũ
        if (selectedIndex >= 0 && selectedIndex < cardUIs.Length)
            cardUIs[selectedIndex].SetSelected(false);

        // Nếu click lại lá đang chọn → bỏ chọn
        if (selectedIndex == index)
        {
            selectedIndex = -1;
            selectedNameText.text = "Chọn lá bài để xem thông tin";
            selectedDescText.text = "";
            useButton.gameObject.SetActive(false);
            return;
        }

        // Chọn lá mới
        selectedIndex = index;
        cardUIs[index].SetSelected(true);

        CardData d = cards[index];
        selectedNameText.text = $"{d.cardName}  ×{d.quantity}";
        selectedDescText.text = d.description;
        useButton.gameObject.SetActive(true);
    }

    void UseSelectedCard()
    {
        if (selectedIndex < 0) return;

        CardData d = cards[selectedIndex];
        if (d.quantity <= 0)
        {
            Debug.Log("Hết bài!");
            return;
        }

        // Trừ số lượng
        d.quantity--;
        cardUIs[selectedIndex].RefreshQuantity();

        Debug.Log($"Đã dùng: {d.cardName}. Còn lại: {d.quantity}");

        // Nếu hết bài → bỏ chọn
        if (d.quantity <= 0)
        {
            selectedNameText.text = $"{d.cardName}  ×0 (hết)";
            useButton.gameObject.SetActive(false);
        }
        else
        {
            selectedNameText.text = $"{d.cardName}  ×{d.quantity}";
        }
    }

    public void ToggleInventory()
    {
        isOpen = !isOpen;
        gameObject.SetActive(isOpen);

        if (isOpen)
        {
            selectedIndex = -1;
            selectedNameText.text = "Chọn lá bài để xem thông tin";
            selectedDescText.text = "";
            useButton.gameObject.SetActive(false);
            ResetAllCards();
        }
    }

    void ResetAllCards()
    {
        foreach (var c in cardUIs)
            c.SetSelected(false);
    }
}