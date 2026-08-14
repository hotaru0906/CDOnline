using UnityEngine;
using System.Collections;

public class panelroom : MonoBehaviour
{
    private Animator animator;
    private Coroutine closeCoroutine;

    void Awake()
    {
        animator = GetComponent<Animator>();

        if (animator == null)
        {
            Debug.LogError("Không tìm thấy Animator component trên " + gameObject.name);
        }
    }

    // =========================
    // OPEN
    // =========================
    void OnEnable()
    {
        if (animator == null)
            return;

        animator.enabled = true;

        // Xóa trigger cũ
        animator.ResetTrigger("Close");

        // Gọi animation Open
        animator.SetTrigger("Open");
    }

    // =========================
    // CLOSE
    // =========================
    public void ClosePanel()
    {
        if (animator == null)
            return;

        // Nếu đang có coroutine đóng thì dừng nó
        if (closeCoroutine != null)
        {
            StopCoroutine(closeCoroutine);
        }

        closeCoroutine = StartCoroutine(CloseAndDisable());
    }

    private IEnumerator CloseAndDisable()
    {
        animator.enabled = true;

        // Xóa trigger Open
        animator.ResetTrigger("Open");

        // Gọi animation Close
        animator.SetTrigger("Close");

        // Chờ 1 frame để Animator chuyển sang Close
        yield return null;

        // Lấy state hiện tại
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);

        // Chờ animation Close chạy hết
        yield return new WaitForSeconds(stateInfo.length);

        // Tắt Animator
        animator.enabled = false;

        // Tắt panel sau khi animation kết thúc
        gameObject.SetActive(false);

        closeCoroutine = null;
    }
}