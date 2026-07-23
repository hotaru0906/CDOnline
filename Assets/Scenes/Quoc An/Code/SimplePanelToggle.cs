using UnityEngine;
using UnityEngine.UI;

public class PanelToggle : MonoBehaviour
{
    [SerializeField] private GameObject panel;      // Panel cần bật/tắt
    [SerializeField] private GameObject toggleButton; // Button trên scene
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

    private bool isOpen = false;

    void Start()
    {
        SetState(false); // Bắt đầu: panel tắt, button hiện
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            Toggle();
    }

    public void Toggle()   // Gán hàm này vào OnClick của Button
    {
        SetState(!isOpen);
    }

    private void SetState(bool open)
    {
        isOpen = open;
        panel.SetActive(open);
        toggleButton.SetActive(!open); // Panel hiện thì button ẩn và ngược lại
    }
}