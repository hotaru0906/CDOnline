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
        if (!subscribed)
        {
            StartCoroutine(WaitAndSubscribe());
        }
    }

    private void OnDisable()
    {
        if (spinCoroutine != null)
        {
            StopCoroutine(spinCoroutine);
            spinCoroutine = null;
        }
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
        if (subscribed)
            return;

        if (VotingManager.Instance == null || !VotingManager.Instance.IsReady)
            return;

        VotingManager.Instance.OnTieBreakStarted += HandleTieBreakStarted;
        VotingManager.Instance.OnTieBreakEnded += HandleTieBreakEnded;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || VotingManager.Instance == null)
            return;

        VotingManager.Instance.OnTieBreakStarted -= HandleTieBreakStarted;
        VotingManager.Instance.OnTieBreakEnded -= HandleTieBreakEnded;
        subscribed = false;
    }

    private void HandleTieBreakStarted(int[] candidateIndices, int winnerIndex, float duration)
    {
        PopulateCandidates(candidateIndices);

        if (spinCoroutine != null)
        {
            StopCoroutine(spinCoroutine);
        }

        spinCoroutine = StartCoroutine(RunSpin(candidateIndices, winnerIndex, duration));
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

    private IEnumerator RunSpin(int[] candidateIndices, int winnerIndex, float delayBeforeSpin)
    {
        if (candidateIndices == null || candidateIndices.Length == 0)
        {
            spinCoroutine = null;
            yield break;
        }

        if (delayBeforeSpin > 0f)
        {
            yield return new WaitForSeconds(delayBeforeSpin);
        }

        int[] wheelOrder = BuildWheelOrder(candidateIndices);
        int winnerSlot = GetWinnerSlot(wheelOrder, winnerIndex);
        if (winnerSlot < 0)
        {
            winnerSlot = 0;
            Debug.LogWarning($"[MinigameTieBreakerUI] Winner index {winnerIndex} not found in wheel order; falling back to slot 0.");
        }

        int resolvedWinnerIndex = wheelOrder[winnerSlot];
        float extraSpin = 1080f + Random.Range(0f, 360f);
        float startAngle = wheelRoot != null ? wheelRoot.localEulerAngles.z : 0f;

        float arrowAngle = 90f;
        if (arrowRoot != null && wheelRoot != null)
        {
            Vector3 arrowVector = arrowRoot.position - wheelRoot.position;
            arrowAngle = Mathf.Atan2(arrowVector.y, arrowVector.x) * Mathf.Rad2Deg;
            Debug.Log($"[MinigameTieBreakerUI] arrowVector={arrowVector} arrowAngle={arrowAngle:F2} offset={wheelArrowOffsetAngle}");
        }

        float targetDelta = Mathf.DeltaAngle(0f, arrowAngle + wheelArrowOffsetAngle);
        float targetRotation = startAngle + extraSpin + targetDelta;

        Debug.Log($"[MinigameTieBreakerUI] startAngle={startAngle:F2} winnerSlot={winnerSlot} resolvedWinner={resolvedWinnerIndex} arrowAngle={arrowAngle:F2} targetDelta={targetDelta:F2} targetRotation={targetRotation:F2}");

        float elapsed = 0f;
        while (elapsed < spinDuration)
        {
            float t = elapsed / spinDuration;
            float easedT = 1f - Mathf.Pow(1f - t, 3f);
            float angle = startAngle + (targetRotation - startAngle) * easedT;

            if (wheelRoot != null)
            {
                wheelRoot.localRotation = Quaternion.Euler(0f, 0f, angle);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (wheelRoot != null)
        {
            wheelRoot.localRotation = Quaternion.Euler(0f, 0f, targetRotation);
        }

        // Actual winner must be determined by the icon physically under the arrow, not by the preselected winner index.
        int actualSlotUnderArrow = winnerSlot;
        float bestDelta = float.MaxValue;
        if (candidateIcons != null && wheelRoot != null)
        {
            Vector3 wheelWorldPos = wheelRoot.position;
            for (int i = 0; i < candidateIcons.Length; i++)
            {
                if (candidateIcons[i] == null)
                    continue;

                Vector3 iconWorldPos = candidateIcons[i].rectTransform.position;
                Vector2 vec = new Vector2(iconWorldPos.x - wheelWorldPos.x, iconWorldPos.y - wheelWorldPos.y);
                float iconAngle = Mathf.Atan2(vec.y, vec.x) * Mathf.Rad2Deg;
                float delta = Mathf.Abs(Mathf.DeltaAngle(iconAngle, arrowAngle + wheelArrowOffsetAngle));
                Debug.Log($"[MinigameTieBreakerUI] slotCheck[{i}] iconAngle={iconAngle:F2} delta={delta:F2} wheelOrderVal={wheelOrder[i]}");

                if (delta < bestDelta)
                {
                    bestDelta = delta;
                    actualSlotUnderArrow = i;
                }
            }
        }

        int finalResolvedWinner = wheelOrder[actualSlotUnderArrow];
        Debug.Log($"[MinigameTieBreakerUI] final winner slot={actualSlotUnderArrow} final winner actualIndex={finalResolvedWinner}");

        HighlightSlot(actualSlotUnderArrow, true);
        ShowWinner(finalResolvedWinner);

        if (VotingManager.Instance != null && VotingManager.Instance.IsReady)
        {
            VotingManager.Instance.ConfirmTieBreakResult(finalResolvedWinner);
        }

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
