using System.Collections;
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
    [SerializeField] private RectTransform wheelRoot;

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
                ? MinigameVotingManager.Instance.GetMinigameByAvailableIndex(slotCandidateIndex)
                : null;

            if (candidateIcons[i] != null)
            {
                candidateIcons[i].sprite = data != null ? data.icon : null;
            }

        }

        if (statusText != null)
        {
            statusText.text = "Selecting...";
        }
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
        float targetAngle = -(winnerSlot * wheelSlotAngle);
        float extraSpin = 1080f + Random.Range(0f, 360f);
        float startAngle = wheelRoot != null ? wheelRoot.localEulerAngles.z : 0f;
        float targetRotation = startAngle + extraSpin + (wheelSlotAngle * 0.5f) + (winnerSlot * wheelSlotAngle);

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
            wheelRoot.localRotation = Quaternion.Euler(0f, 0f, targetRotation + targetAngle);
        }

        HighlightSlot(winnerSlot, true);
        ShowWinner(winnerIndex);
        spinCoroutine = null;
    }

    private int[] BuildWheelOrder(int[] candidateIndices)
    {
        int[] wheelOrder = new int[candidateIcons != null ? candidateIcons.Length : 6];

        if (candidateIndices == null || candidateIndices.Length == 0)
        {
            return wheelOrder;
        }

        int candidateCount = candidateIndices.Length;

        if (candidateCount == 2)
        {
            for (int i = 0; i < wheelOrder.Length; i++)
            {
                wheelOrder[i] = candidateIndices[i % 2];
            }
        }
        else if (candidateCount == 3)
        {
            for (int i = 0; i < wheelOrder.Length; i++)
            {
                wheelOrder[i] = candidateIndices[i % 3];
            }
        }
        else
        {
            for (int i = 0; i < wheelOrder.Length; i++)
            {
                wheelOrder[i] = candidateIndices[i % candidateCount];
            }
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
            ? MinigameVotingManager.Instance.GetMinigameByAvailableIndex(winnerIndex)
            : null;

        if (statusText != null)
        {
            string winnerName = winnerData != null ? winnerData.minigameName : $"Minigame #{winnerIndex + 1}";
            statusText.text = $"Minigame selected: {winnerName}";
        }
    }
}
