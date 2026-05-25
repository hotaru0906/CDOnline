using UnityEngine;
using System.Collections;

/// <summary>
/// Khởi động board phase sau khi BoardScene load xong.
/// Đặt component này trên 1 GameObject trong BoardScene (không cần NetworkBehaviour).
/// Tương tự RouletteSceneController.
/// </summary>
public class BoardSceneController : MonoBehaviour
{
    private void Start()
    {
        StartCoroutine(InitializeDelayed());
    }

    private IEnumerator InitializeDelayed()
    {
        // Đợi 1 frame để đảm bảo tất cả NetworkObjects đã spawn xong
        yield return null;

        Debug.Log("[BoardSceneController] Board scene ready — notifying GameManager");

        if (GameManager.Instance != null)
            GameManager.Instance.OnBoardSceneReady();
        else
            Debug.LogError("[BoardSceneController] GameManager.Instance is NULL!");
    }
}
