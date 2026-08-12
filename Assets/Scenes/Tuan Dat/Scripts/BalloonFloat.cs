using UnityEngine;
using DG.Tweening;

public class BalloonFloat : MonoBehaviour
{
    public float moveHeight = 3f;      // Bay lên bao nhiêu
    public float duration = 3f;        // Thời gian bay
    public float respawnDelay = 1f;    // Thời gian chờ trước khi xuất hiện lại

    private Vector3 startPos;

    void Start()
    {
        startPos = transform.position;
        PlayAnimation();
    }

    void PlayAnimation()
    {
        // Đưa về vị trí ban đầu
        transform.position = startPos;

        // Bay lên
        transform.DOMoveY(startPos.y + moveHeight, duration)
            .SetEase(Ease.OutSine)
            .OnComplete(() =>
            {
                // Biến mất
                gameObject.SetActive(false);

                // Chờ rồi xuất hiện lại
                DOVirtual.DelayedCall(respawnDelay, () =>
                {
                    gameObject.SetActive(true);
                    PlayAnimation();
                });
            });
    }

    void OnDestroy()
    {
        DOTween.Kill(transform);
    }
}