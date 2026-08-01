using Fusion;
using UnityEngine;

public class MG3HammerManager : NetworkBehaviour
{
    public static MG3HammerManager Instance { get; private set; }

    [Header("Hammer")]
    [SerializeField] private MG3ItemPickup hammerPrefab;
    [SerializeField] private Transform[] spawnPoints;

    [Header("Rule")]
    [SerializeField] private float hammerLifeTime = 20f;

    [Networked]
    public PlayerRef HammerHolder { get; private set; }

    [Networked]
    public NetworkId HammerHolderObjectId { get; private set; }

    [Networked]
    public TickTimer HammerTimer { get; private set; }

    [Networked]
    public NetworkBool HammerExists { get; private set; }

    private MG3ItemPickup currentHammer;

    public override void Spawned()
    {
        Instance = this;
    }

    private void SpawnHammer()
    {
        if (!HasStateAuthority)
            return;

        if (hammerPrefab == null)
        {
            Debug.LogError("[HammerManager] Hammer Prefab chưa được gán!");
            return;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("[HammerManager] Chưa có Spawn Point!");
            return;
        }

        Transform spawn = spawnPoints[Random.Range(0, spawnPoints.Length)];

        currentHammer = Runner.Spawn(
            hammerPrefab,
            spawn.position,
            Quaternion.identity);

        HammerExists = true;

        Debug.Log("[HammerManager] Hammer Spawned");
    }

    private void RemoveCurrentHammer()
    {
        if (!HasStateAuthority)
            return;

        if (currentHammer == null)
            return;

        Runner.Despawn(currentHammer.Object);

        currentHammer = null;

        HammerExists = false;

        Debug.Log("[HammerManager] Hammer Removed");
    }

    public void AssignHammer(PlayerController player)
    {
        if (!HasStateAuthority)
            return;

        if (player == null)
            return;

        var brawlData = player.GetComponent<MG3PlayerBrawlData>();
        if (brawlData == null)
            return;

        HammerHolder = player.Object.InputAuthority;

        HammerHolderObjectId = player.Object.Id;

        HammerTimer = TickTimer.CreateFromSeconds(Runner, hammerLifeTime);

        brawlData.PickupItem();
        MinigameHUDController.Instance?.ShowHammerTimer();
        RPC_ShowTimerUI();

        Debug.Log($"[HammerManager] Hammer assigned to P{HammerHolder.PlayerId}");
    }

    public void RemoveHammerFromHolder()
    {
        if (!HasStateAuthority)
            return;

        if (HammerHolder == PlayerRef.None)
            return;

        NetworkObject playerObj = Runner.FindObject(HammerHolderObjectId);

        if (playerObj != null)
        {
            MG3PlayerBrawlData brawl = playerObj.GetComponent<MG3PlayerBrawlData>();

            if (brawl != null)
            {
                brawl.DropItem();
            }
        }

        HammerHolder = PlayerRef.None;

        HammerHolderObjectId = default;

        HammerTimer = TickTimer.None;

        Debug.Log("[HammerManager] Holder Cleared");
    }

    public float RemainingTime
    {
        get
        {
            if (!HammerTimer.IsRunning)
                return 0f;

            float? remaining = HammerTimer.RemainingTime(Runner);

            return remaining ?? 0f;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (HammerTimer.IsRunning)
        {
            MinigameHUDController.Instance?.UpdateHammerTimer(RemainingTime);
        }

        if (!HasStateAuthority)
            return;

        if (!HammerTimer.IsRunning)
            return;

        if (HammerTimer.Expired(Runner))
        {
            EliminateHammerHolder();
            return;
        }
    }

    private void EliminateHammerHolder()
    {
        if (HammerHolder == PlayerRef.None)
            return;

        NetworkObject playerObj = Runner.FindObject(HammerHolderObjectId);

        if (playerObj == null)
            return;

        var mgData = playerObj.GetComponent<PlayerMinigameData>();

        if (mgData == null)
            return;

        Debug.Log($"[HammerManager] Time out! Eliminate P{HammerHolder.PlayerId}");

        mgData.EliminateImmediately();

        FinishHammerRound();
    }

    public void StartHammerRound()
    {
        if (!HasStateAuthority)
            return;

        SpawnHammer();

        HammerHolder = PlayerRef.None;
        HammerTimer = TickTimer.None;

        Debug.Log("[HammerManager] Hammer Round Started");
    }

    public void ResetHammerTimer()
    {
        if (!HasStateAuthority)
            return;

        if (HammerHolder == PlayerRef.None)
            return;

        HammerTimer = TickTimer.CreateFromSeconds(Runner, hammerLifeTime);

        Debug.Log("[HammerManager] Timer Reset");
    }

    public void FinishHammerRound()
    {
        if (!HasStateAuthority)
            return;

        Debug.Log("[HammerManager] Finish Hammer Round");

        RemoveHammerFromHolder();

        MinigameHUDController.Instance?.HideHammerTimer();

        RPC_HideTimerUI();

        RemoveCurrentHammer();

        SpawnHammer();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowTimerUI()
    {
        MG3HammerTimerUI.Instance?.Show();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_HideTimerUI()
    {
        MG3HammerTimerUI.Instance?.Hide();
    }
}