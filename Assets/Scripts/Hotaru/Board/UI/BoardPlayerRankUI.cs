using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoardPlayerRankUI : MonoBehaviour
{
    [SerializeField] private BoardPlayerRankEntryUI[] entries = new BoardPlayerRankEntryUI[4];

    private static readonly Color[] SlotColors =
    {
        new Color(0.9f, 0.2f, 0.2f),
        new Color(0.2f, 0.4f, 0.9f),
        new Color(0.2f, 0.8f, 0.2f),
        new Color(0.95f, 0.8f, 0.1f)
    };

    private void Start()
    {
        StartCoroutine(WaitForBoardManager());
    }

    private void OnDestroy()
    {
        PlayerItemInventory.OnInventoryRegistered -= OnInventoryRegistered;
        PlayerItemInventory.OnInventoryUnregistered -= OnInventoryUnregistered;
        PlayerItemInventory.OnResourceChanged -= OnResourceChanged;

        if (BoardManager.Instance != null)
            BoardManager.Instance.OnTurnStarted -= OnTurnStarted;
    }

    private IEnumerator WaitForBoardManager()
    {
        while (BoardManager.Instance == null)
            yield return null;

        PlayerItemInventory.OnInventoryRegistered += OnInventoryRegistered;
        PlayerItemInventory.OnInventoryUnregistered += OnInventoryUnregistered;
        PlayerItemInventory.OnResourceChanged += OnResourceChanged;
        BoardManager.Instance.OnTurnStarted += OnTurnStarted;
        Refresh();
        Debug.Log("[BoardPlayerRankUI] Subscribed");
    }

    // =====================================================================
    // EVENTS
    // =====================================================================

    private void OnTurnStarted(int playerId)
    {
        SetActiveTurn(playerId);
    }

    private void OnResourceChanged(int playerId, int keyCount, int chestCount)
    {
        var bm = BoardManager.Instance;
        if (bm == null)
            return;

        for (int i = 0; i < bm.ActivePlayerCount; i++)
        {
            if (entries[i] == null)
                continue;

            if (bm.GetPlayerIDAtSlot(i) != playerId)
                continue;

            entries[i].SetResourceData(keyCount, chestCount);
            return;
        }

        // Inventory update can arrive before ActivePlayerCount/slot mapping is ready.
        Refresh();
    }

    private void OnInventoryRegistered(int playerId)
    {
        Refresh();
    }

    private void OnInventoryUnregistered(int playerId)
    {
        Refresh();
    }

    // =====================================================================
    // REFRESH
    // =====================================================================

    public void Refresh()
    {
        var bm = BoardManager.Instance;
        if (bm == null) return;

        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i] == null) continue;

            if (i >= bm.ActivePlayerCount)
            {
                entries[i].gameObject.SetActive(false);
                continue;
            }

            entries[i].gameObject.SetActive(true);

            int    pid  = bm.GetPlayerIDAtSlot(i);
            string name = BoardHUDController.Instance?.GetPlayerName(pid) ?? $"P{pid}";

            var inventory = PlayerItemInventory.GetForPlayer(pid);
            int keyCount = inventory != null ? inventory.GetKeyCount() : 0;
            int chestCount = inventory != null ? inventory.GetChestCount() : 0;

            entries[i].SetData(
                playerName  : name,
                keyCount    : keyCount,
                chestCount  : chestCount,
                isActiveTurn: false
            );
        }
    }

    public void SetActiveTurn(int playerId)
    {
        Refresh();

        var bm = BoardManager.Instance;
        if (bm == null) return;

        for (int i = 0; i < bm.ActivePlayerCount; i++)
        {
            if (entries[i] == null) continue;
            entries[i].SetTurnActive(bm.GetPlayerIDAtSlot(i) == playerId);
        }
    }
}