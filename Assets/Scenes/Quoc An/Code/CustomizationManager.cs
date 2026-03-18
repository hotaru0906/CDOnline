using UnityEngine;
using UnityEngine.UI;

public class CustomizationManager : MonoBehaviour
{
    [Header("Panels bên phải")]
    public GameObject panelAccessory;
    public GameObject panelCustomCharacter;

    [Header("Buttons")]
    public Button btnAccessory;
    public Button btnCustomCharacter;

    void Start()
    {
        // Mặc định mở Accessory như ảnh trên của bạn
        ShowAccessory();

        btnAccessory.onClick.AddListener(ShowAccessory);
        btnCustomCharacter.onClick.AddListener(ShowCustomCharacter);
    }

    public void ShowAccessory()
    {
        panelAccessory.SetActive(true);
        panelCustomCharacter.SetActive(false);
    }

    public void ShowCustomCharacter()
    {
        panelAccessory.SetActive(false);
        panelCustomCharacter.SetActive(true);
    }
}