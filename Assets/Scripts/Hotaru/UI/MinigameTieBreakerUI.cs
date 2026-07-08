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
    [SerializeField] private TMP_Text[] candidateNames;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Image selectedMinigameIcon;
    [SerializeField] private TMP_Text selectedMinigameName;

    [Header("Spin Settings")]
    [SerializeField] private float highlightInterval = 0.1f;

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
        for (int i = 0; i < candidateIcons.Length; i++)
        {
            bool active = candidateIndices != null && i < candidateIndices.Length;

            if (candidateIcons[i] != null)
            {
                candidateIcons[i].gameObject.SetActive(active);
                candidateIcons[i].color = Color.white;
            }

            if (candidateNames != null && i < candidateNames.Length && candidateNames[i] != null)
            {
                candidateNames[i].gameObject.SetActive(active);
            }

            if (!active)
                continue;

            MinigameData data = MinigameVotingManager.Instance != null
                ? MinigameVotingManager.Instance.GetMinigameByAvailableIndex(candidateIndices[i])
                : null;

            if (candidateIcons[i] != null)
            {
                candidateIcons[i].sprite = data != null ? data.icon : null;
            }

            if (candidateNames != null && i < candidateNames.Length && candidateNames[i] != null)
            {
                candidateNames[i].text = data != null ? data.minigameName : $"Minigame #{candidateIndices[i] + 1}";
            }
        }

        if (statusText != null)
        {
            statusText.text = "Dang quay de chon minigame...";
        }

        if (selectedMinigameIcon != null)
        {
            selectedMinigameIcon.sprite = null;
            selectedMinigameIcon.color = new Color(1f, 1f, 1f, 0f);
        }

        if (selectedMinigameName != null)
        {
            selectedMinigameName.text = string.Empty;
        }
    }

    private IEnumerator RunSpin(int[] candidateIndices, int winnerIndex, float duration)
    {
        if (candidateIndices == null || candidateIndices.Length == 0)
            yield break;

        float elapsed = 0f;
        int lastHighlighted = -1;

        while (elapsed < duration)
        {
            int randomSlot = Random.Range(0, candidateIndices.Length);
            HighlightSlot(lastHighlighted, false);
            HighlightSlot(randomSlot, true);
            lastHighlighted = randomSlot;

            yield return new WaitForSeconds(highlightInterval);
            elapsed += highlightInterval;
        }

        HighlightSlot(lastHighlighted, false);

        int winnerSlot = -1;
        for (int i = 0; i < candidateIndices.Length; i++)
        {
            if (candidateIndices[i] == winnerIndex)
            {
                winnerSlot = i;
                break;
            }
        }

        if (winnerSlot >= 0)
        {
            HighlightSlot(winnerSlot, true);
        }

        ShowWinner(winnerIndex);
        spinCoroutine = null;
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

        if (selectedMinigameIcon != null)
        {
            selectedMinigameIcon.sprite = winnerData != null ? winnerData.icon : null;
            selectedMinigameIcon.color = Color.white;
        }

        if (selectedMinigameName != null)
        {
            selectedMinigameName.text = winnerData != null ? winnerData.minigameName : $"Minigame #{winnerIndex + 1}";
        }

        if (statusText != null)
        {
            statusText.text = "Minigame duoc chon:";
        }
    }
}
