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

    public void InitializeHands()
    {
        var bm = BoardManager.Instance;
        if (bm == null) return;

        int localId = GetLocalPlayerId();

        Debug.Log($"[BoardInventoryUI] LocalId = {localId}");

        for (int i = 0; i < hands.Length; i++)
        {
            if (hands[i] == null) continue;

            int pid = bm.GetPlayerIDAtSlot(i);

            Debug.Log($"[BoardInventoryUI] slot={i} pid={pid} isLocal={pid == localId}");
            
            if (pid < 0)
            {
                hands[i].gameObject.SetActive(false);
                continue;
            }

            hands[i].gameObject.SetActive(true);
            hands[i].Initialize(pid, pid == localId);
            hands[i].Hide();
        }
    }

    public void RefreshAfterRestore()
    {
        if (!_initialized)
        {
            InitializeHands();
            _initialized = true;
        }

        foreach (var h in hands)
            h?.RefreshHand();
    }

    // =====================================================================
    // TURN
    // =====================================================================

    private void OnTurnStarted(int playerId)
    {
        var bm = BoardManager.Instance;
        if (bm == null) return;

        if (!_initialized)
        {
            InitializeHands();
            _initialized = true;
        }

        // Reset item used flag cho tất cả hands
        foreach (var h in hands)
            h?.ResetItemUsed();

        // Refresh tất cả hands từ inventory
        foreach (var h in hands)
            h?.RefreshHand();

        // Expand/collapse
        for (int i = 0; i < hands.Length; i++)
        {
            if (hands[i] == null)
                continue;

            bool isMyTurn =
                hands[i].IsLocalPlayer &&
                bm.GetPlayerIDAtSlot(i) == playerId;

            if (isMyTurn)
            {
                hands[i].Show();
                hands[i].Expand();
            }
            else
            {
                hands[i].Collapse();
                hands[i].Hide();
            }
        }
    }

    public void OnItemUsed(int playerId)
    {
        var bm = BoardManager.Instance;
        if (bm == null) return;

        for (int i = 0; i < hands.Length; i++)
        {
            if (hands[i] == null) continue;
            if (bm.GetPlayerIDAtSlot(i) == playerId)
            {
                hands[i].SetItemUsed();
                hands[i].RefreshHand(); // sync từ inventory
                return;
            }
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