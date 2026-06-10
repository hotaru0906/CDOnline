using System.Collections;
using UnityEngine;
public class BoardInventoryUI : MonoBehaviour
{
    [SerializeField] private BoardHandUI[] hands = new BoardHandUI[4];
    private bool _initialized = false;
    private void Start()
    {
        StartCoroutine(WaitForBoardManager());
    }

    private IEnumerator WaitForBoardManager()
    {
        while (BoardManager.Instance == null)
            yield return null;

        BoardManager.Instance.OnTurnStarted += OnTurnStarted;
        Debug.Log("[BoardInventoryUI] Subscribed to OnTurnStarted");
    }

    private void OnDestroy()
    {
        if (BoardManager.Instance != null)
            BoardManager.Instance.OnTurnStarted -= OnTurnStarted;
    }

    // =====================================================================
    // INIT
    // =====================================================================

    private void InitializeHands()
    {
        var bm = BoardManager.Instance;
        if (bm == null) return;

        int localId = GetLocalPlayerId();

        for (int i = 0; i < hands.Length; i++)
        {
            if (hands[i] == null) continue;

            int pid = bm.GetPlayerIDAtSlot(i);
            if (pid < 0)
            {
                hands[i].gameObject.SetActive(false);
                continue;
            }

            hands[i].gameObject.SetActive(true);
            hands[i].Initialize(pid, pid == localId);
        }
    }

    // =====================================================================
    // TURN
    // =====================================================================

    private void OnTurnStarted(int playerId)
    {
        var bm = BoardManager.Instance;
        if (bm == null) return;

        // Init lần đầu
        if (!_initialized)
        {
            InitializeHands();
            _initialized = true;
        }

        // Refresh tất cả hands
        foreach (var h in hands)
            h?.RefreshHand();

        // Expand/collapse
        for (int i = 0; i < hands.Length; i++)
        {
            if (hands[i] == null) continue;
            if (bm.GetPlayerIDAtSlot(i) == playerId)
                hands[i].Expand();
            else
                hands[i].Collapse();
        }
    }

    // =====================================================================
    // HELPERS
    // =====================================================================

    private void CollapseAll()
    {
        foreach (var h in hands)
            h?.Collapse();
    }

    private int GetLocalPlayerId()
    {
        if (PlayerNetworkData.Local != null && PlayerNetworkData.Local.Object != null)
            return PlayerNetworkData.Local.Object.InputAuthority.PlayerId;
        return -1;
    }
}