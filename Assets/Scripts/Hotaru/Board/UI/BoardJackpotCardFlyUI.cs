using UnityEngine;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

/// <summary>
/// Hiệu ứng 2 lá bài jackpot xuất hiện giữa màn hình rồi bay xuống hand.
/// Chỉ chạy trên client của player vừa ăn jackpot (đã filter ở RPC gọi vào đây).
/// SETUP:
///   1. Tạo GameObject "JackpotCardFly" ở center Canvas (ngang hàng BoardCardDisplayUI)
///   2. centerAnchor: RectTransform ở giữa màn hình
///   3. flyCardPrefab: dùng chung prefab BoardCardUI (card thật trong hand)
/// </summary>
public class BoardJackpotCardFlyUI : MonoBehaviour
{
    public static BoardJackpotCardFlyUI Instance { get; private set; }

    [Header("References")]
    [SerializeField] private RectTransform centerAnchor;
    [SerializeField] private GameObject flyCardPrefab;
    [SerializeField] private Canvas canvas;

    [Header("Timing")]
    [SerializeField] private float revealStagger = 0.15f; // độ trễ giữa lá 1 và lá 2 xuất hiện
    [SerializeField] private float spawnScaleDuration = 0.3f;
    [SerializeField] private float holdAtCenter = 0.7f;
    [SerializeField] private float flyDuration = 0.5f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void PlayJackpotReveal(BoardItemEffect[] effects)
    {
        StartCoroutine(RevealRoutine(effects));
    }

    private IEnumerator RevealRoutine(BoardItemEffect[] effects)
    {
        var targetHand = BoardInventoryUI.Instance?.GetLocalHandTransform();
        if (targetHand == null)
        {
            Debug.LogWarning("[BoardJackpotCardFlyUI] Không tìm thấy hand của local player.");
            yield break;
        }

        var spawned = new List<RectTransform>();

        foreach (var effect in effects)
        {
            var data = BoardItemPool.Current?.GetByEffect(effect);

            var go = Instantiate(flyCardPrefab, centerAnchor);
            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.zero;

            var card = go.GetComponent<BoardCardUI>();
            // isLocal = true để hiện FrontFace (icon thật), sau đó tự tắt raycast để không bấm được
            card?.Initialize(data, effect, -1, true);
            foreach (var g in go.GetComponentsInChildren<Graphic>())
                g.raycastTarget = false;

            spawned.Add(rect);
            rect.DOScale(1f, spawnScaleDuration).SetEase(Ease.OutBack);

            yield return new WaitForSeconds(revealStagger);
        }

        yield return new WaitForSeconds(holdAtCenter);

        int remaining = spawned.Count;
        foreach (var rect in spawned)
        {
            rect.SetParent(canvas.transform, true); // đổi parent để bay tự do, không bị clip bởi centerAnchor
            Vector3 targetPos = targetHand.position;

            rect.DOMove(targetPos, flyDuration).SetEase(Ease.InQuad);
            rect.DOScale(0.4f, flyDuration).SetEase(Ease.InQuad)
                .OnComplete(() =>
                {
                    Destroy(rect.gameObject);
                    remaining--;
                    if (remaining == 0)
                        BoardInventoryUI.Instance?.RefreshAfterRestore();
                });
        }
    }
}