using System.Collections;
using UnityEngine;

public class BoardInventoryUI : MonoBehaviour
{
    public static BoardInventoryUI Instance { get; private set; }

    [SerializeField] private BoardHandUI[] hands = new BoardHandUI[4];

    private bool _initialized = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        StartCoroutine(WaitForBoardManager());
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (BoardManager.Instance != null)
            BoardManager.Instance.OnTurnStarted -= OnTurnStarted;
    }

    private IEnumerator WaitForBoardManager()
    {
        while (BoardManager.Instance == null)
            yield return null;

        BoardManager.Instance.OnTurnStarted += OnTurnStarted;
        Debug.Log("[BoardInventoryUI] Subscribed to OnTurnStarted");
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

        if (!_initialized)
        {
            InitializeHands();
            _initialized = true;
        }

        foreach (var h in hands)
            h?.ResetItemUsed();

        foreach (var h in hands)
            h?.RefreshHand();

        for (int i = 0; i < hands.Length; i++)
        {
            if (hands[i] == null) continue;
            if (bm.GetPlayerIDAtSlot(i) == playerId)
                hands[i].Expand();
            else
                hands[i].Collapse();
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
                hands[i].RefreshHand();
                return;
            }
        }
    }

    // =====================================================================
    // HOVER — nhận từ RPC BoardManager
    // =====================================================================

    public void OnCardHoverEnter(int playerId, int itemSlot)
    {
        GetHandForPlayer(playerId)?.SetCardHover(itemSlot, true);
    }

    public void OnCardHoverExit(int playerId, int itemSlot)
    {
        GetHandForPlayer(playerId)?.SetCardHover(itemSlot, false);
    }

    // =====================================================================
    // HELPERS
    // =====================================================================

    private BoardHandUI GetHandForPlayer(int playerId)
    {
        var bm = BoardManager.Instance;
        if (bm == null) return null;

        for (int i = 0; i < hands.Length; i++)
        {
            if (hands[i] == null) continue;
            if (bm.GetPlayerIDAtSlot(i) == playerId) return hands[i];
        }
        return null;
    }

    private int GetLocalPlayerId()
    {
        if (PlayerNetworkData.Local != null && PlayerNetworkData.Local.Object != null)
            return PlayerNetworkData.Local.Object.InputAuthority.PlayerId;
        return -1;
    }
}