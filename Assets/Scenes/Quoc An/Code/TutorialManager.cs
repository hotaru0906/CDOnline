using UnityEngine;
using UnityEngine.Video;
using TMPro;
using UnityEngine.UI;

public class TutorialManager : MonoBehaviour
{
    [Header("Video")]
    public VideoPlayer videoPlayer;

    [Header("Button")]
    public Button startButton;

    void Start()
    {
        startButton.onClick.AddListener(OnStartClicked);

        // Tự động play video khi mở tutorial
        if (videoPlayer != null)
            videoPlayer.Play();
    }

    private void OnStartClicked()
    {
        Debug.Log("BẮT ĐẦU GAME TỪ TUTORIAL!");
        // TODO: Sau này bạn sẽ thay bằng Load scene game hoặc chuyển canvas
    }
}
