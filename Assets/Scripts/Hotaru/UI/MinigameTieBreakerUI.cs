using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI tie-break khi voting hoa phieu.
/// Host da chot winner truoc, panel nay hien thi icon cac minigame va ket qua duoc chon.
/// </summary>
public class MinigameTieBreakerUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image[] candidateIcons;
    [SerializeField] private TMP_Text statusText;

    [Header("Spin Settings")]
    [SerializeField] private float highlightInterval = 0.1f;
    [SerializeField] private float spinDuration = 2.2f;
    [SerializeField] private float wheelSlotAngle = 60f;
    [SerializeField] private float wheelArrowOffsetAngle = 0f;
    [SerializeField] private RectTransform wheelRoot;
    [SerializeField] private RectTransform arrowRoot;

    [Header("Sprite Settings")]
    [SerializeField] private bool useOverrideSprites = false;
    [SerializeField] private Sprite[] overrideSprites;

    private Coroutine spinCoroutine;
    private bool subscribed;

    private void OnEnable()
    {
        TrySubscribe();
    }
    private void OnDisable()
    {
        if (spinCoroutine != null)
        {
            StopCoroutine(spinCoroutine);
            spinCoroutine = null;
        }
        Unsubscribe();
    }
    private void OnDestroy()
    {
        Unsubscribe();
    }

    private IEnumerator WaitAndSubscribe()
    {
        while (!subscribed)
        {
            TrySubscribe();
            yield return null;
        }
    }
    private void TrySubscribe()
    {
        if (VotingManager.Instance == null || !VotingManager.Instance.IsReady)
        {
            StartCoroutine(WaitAndSubscribe());
            return;
        }

        Unsubscribe(); // gỡ trước (an toàn nếu lỡ subscribe 2 lần) rồi mới đăng ký lại
        VotingManager.Instance.OnTieBreakStarted += HandleTieBreakStarted;
        VotingManager.Instance.OnTieBreakEnded += HandleTieBreakEnded;
        subscribed = true;
    }
    private void Unsubscribe()
    {
        if (VotingManager.Instance == null) { subscribed = false; return; }
        VotingManager.Instance.OnTieBreakStarted -= HandleTieBreakStarted;
        VotingManager.Instance.OnTieBreakEnded -= HandleTieBreakEnded;
        subscribed = false;
    }
    private void HandleTieBreakStarted(int[] candidateIndices, int winnerIndex, float delayBeforeSpin, float spinDurationFromHost)
    {
        PopulateCandidates(candidateIndices);

        if (spinCoroutine != null)
            StopCoroutine(spinCoroutine);

        Debug.Log($"[MinigameTieBreakerUI] HandleTieBreakStarted fired. subscribed={subscribed}");
        spinCoroutine = StartCoroutine(RunSpin(candidateIndices, winnerIndex, delayBeforeSpin, spinDurationFromHost));
    }

    private void HandleTieBreakEnded(int winnerIndex)
    {
        ShowWinner(winnerIndex);
    }

    private void PopulateCandidates(int[] candidateIndices)
    {
        int[] wheelOrder = BuildWheelOrder(candidateIndices);

        for (int i = 0; i < candidateIcons.Length; i++)
        {
            bool active = i < wheelOrder.Length;

            if (candidateIcons[i] != null)
            {
                candidateIcons[i].gameObject.SetActive(active);
                candidateIcons[i].color = Color.white;
            }

            if (!active)
                continue;

            int slotCandidateIndex = wheelOrder[i];
            MinigameData data = MinigameVotingManager.Instance != null
                ? MinigameVotingManager.Instance.GetMinigameByActualIndex(slotCandidateIndex)
                : null;

            if (candidateIcons[i] != null)
            {
                if (useOverrideSprites && overrideSprites != null && slotCandidateIndex >= 0 && slotCandidateIndex < overrideSprites.Length && overrideSprites[slotCandidateIndex] != null)
                {
                    candidateIcons[i].sprite = overrideSprites[slotCandidateIndex];
                }
                else
                {
                    candidateIcons[i].sprite = data != null ? data.icon : null;
                }
            }

        }

        if (statusText != null)
        {
            statusText.text = "Selecting...";
        }
    }

    private Sprite[] GetOrderedSprites(Sprite[] sourceSprites, int count)
    {
        if (sourceSprites == null || sourceSprites.Length == 0)
            return null;

        Sprite[] result = new Sprite[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = sourceSprites[i % sourceSprites.Length];
        }

        return result;
    }

    private IEnumerator RunSpin(int[] wheelOrderRaw, int winnerIndex, float delayBeforeSpin, float spinDurationFromHost)
    {
        if (wheelOrderRaw == null || wheelOrderRaw.Length == 0)
        {
            spinCoroutine = null;
            yield break;
        }

        if (delayBeforeSpin > 0f)
            yield return new WaitForSeconds(delayBeforeSpin);

        // Dùng duration từ host để mọi client đồng bộ chính xác cùng 1 thời lượng - tránh lệch
        // với thời gian host đợi trước khi ẩn panel (nguyên nhân gây "đứng hình" ở client).
        float effectiveSpinDuration = spinDurationFromHost > 0f ? spinDurationFromHost : spinDuration;

        int[] wheelOrder = BuildWheelOrder(wheelOrderRaw);
        int winnerSlot = GetWinnerSlot(wheelOrder, winnerIndex);
        if (winnerSlot < 0)
        {
            winnerSlot = 0;
            Debug.LogWarning($"[MinigameTieBreakerUI] Winner index {winnerIndex} not found in wheel order; falling back to slot 0.");
        }

        float startAngle = wheelRoot != null ? wheelRoot.localEulerAngles.z : 0f;

        float arrowAngle = 90f;
        if (arrowRoot != null && wheelRoot != null)
        {
            Vector3 arrowVector = arrowRoot.position - wheelRoot.position;
            arrowAngle = Mathf.Atan2(arrowVector.y, arrowVector.x) * Mathf.Rad2Deg;
        }

        // QUAN TRỌNG: đo góc HIỆN TẠI (trước khi xoay) của đúng icon sẽ thắng, để biết cần xoay
        // bao nhiêu độ mới đưa icon đó về đúng vị trí mũi tên. Thiếu bước này là lý do bánh xe
        // trước đây luôn dừng ở một góc gần-như-ngẫu-nhiên, không liên quan tới winnerIndex.
        float winnerCurrentAngle = arrowAngle;
        if (candidateIcons != null && winnerSlot < candidateIcons.Length && candidateIcons[winnerSlot] != null && wheelRoot != null)
        {
            Vector3 iconWorldPos = candidateIcons[winnerSlot].rectTransform.position;
            Vector2 vec = new Vector2(iconWorldPos.x - wheelRoot.position.x, iconWorldPos.y - wheelRoot.position.y);
            winnerCurrentAngle = Mathf.Atan2(vec.y, vec.x) * Mathf.Rad2Deg;
        }

        float neededDelta = Mathf.DeltaAngle(winnerCurrentAngle, arrowAngle + wheelArrowOffsetAngle);

        // Số vòng quay thêm chỉ để đẹp mắt - LUÔN là bội số 360 nên không ảnh hưởng ô dừng lại,
        // mỗi client random số vòng khác nhau cũng không sao vì đích đến cuối cùng vẫn giống nhau.
        int extraFullTurns = 3 + Random.Range(0, 2);
        float targetRotation = startAngle + extraFullTurns * 360f + neededDelta;

        float elapsed = 0f;
        while (elapsed < effectiveSpinDuration)
        {
            float t = elapsed / effectiveSpinDuration;
            float easedT = 1f - Mathf.Pow(1f - t, 3f);
            float angle = startAngle + (targetRotation - startAngle) * easedT;

            if (wheelRoot != null)
                wheelRoot.localRotation = Quaternion.Euler(0f, 0f, angle);

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (wheelRoot != null)
            wheelRoot.localRotation = Quaternion.Euler(0f, 0f, targetRotation);

        HighlightSlot(winnerSlot, true);
        ShowWinner(winnerIndex); // luôn dùng winnerIndex từ host - không tự dò/tự quyết định lại

        spinCoroutine = null;
    }

    private int[] BuildWheelOrder(int[] candidateIndices)
    {
        int[] wheelOrder = new int[candidateIcons != null ? candidateIcons.Length : 6];

        if (candidateIndices == null || candidateIndices.Length == 0)
        {
            return wheelOrder;
        }

        for (int i = 0; i < wheelOrder.Length; i++)
        {
            wheelOrder[i] = candidateIndices[i % candidateIndices.Length];
        }

        return wheelOrder;
    }

    private int GetWinnerSlot(int[] wheelOrder, int winnerIndex)
    {
        if (wheelOrder == null)
            return -1;

        for (int i = 0; i < wheelOrder.Length; i++)
        {
            if (wheelOrder[i] == winnerIndex)
            {
                return i;
            }
        }

        return -1;
    }

    private void HighlightSlot(int slot, bool active)
    {
        if (slot < 0 || slot >= candidateIcons.Length)
            return;

        if (candidateIcons[slot] == null)
            return;

        candidateIcons[slot].color = active ? new Color(1f, 1f, 1f, 1f) : new Color(0.7f, 0.7f, 0.7f, 1f);
    }

    private void ShowWinner(int winnerIndex)
    {
        MinigameData winnerData = MinigameVotingManager.Instance != null
            ? MinigameVotingManager.Instance.GetMinigameByActualIndex(winnerIndex)
            : null;

        if (statusText != null)
        {
            string winnerName = winnerData != null ? winnerData.minigameName : $"Minigame #{winnerIndex + 1}";
            statusText.text = $"Minigame selected: {winnerName}";
        }
    }
}
