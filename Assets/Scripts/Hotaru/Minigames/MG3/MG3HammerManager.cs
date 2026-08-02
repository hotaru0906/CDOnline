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

    [Networked, OnChangedRender(nameof(OnHammerTimerChanged))]
    public TickTimer HammerTimer { get; private set; }

    [Networked]
    public float HammerTimeLeft { get; set; }

    [Networked]
    public NetworkBool HammerExists { get; private set; }

    private MG3ItemPickup currentHammer;

    public override void Spawned()
    {
        Instance = this;
    }

    public void SpawnHammer()
    {
        if (!HasStateAuthority)
            return;

        if (HammerTimer.IsRunning)
        {
            HammerTimeLeft = HammerTimer.RemainingTime(Runner) ?? 0f;
        }
        else
        {
            HammerTimeLeft = 0f;
        }

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

        // Bắt đầu đếm thời gian
        HammerTimer = TickTimer.CreateFromSeconds(Runner, hammerLifeTime);

        HammerTimeLeft = hammerLifeTime;

        // Player cầm búa
        brawlData.PickupItem();

        // Hiện timer cho tất cả máy
        RPC_ShowHammerTimer();

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

        // Dừng timer
        HammerTimer = TickTimer.None;

        HammerTimeLeft = 0f;

        // Ẩn timer trên tất cả máy
        RPC_HideHammerTimer();

        Debug.Log("[HammerManager] Holder Cleared");
    }

    public override void FixedUpdateNetwork()
    {
        if (HasStateAuthority)
        {
            if (HammerTimer.IsRunning)
            {
                HammerTimeLeft = HammerTimer.RemainingTime(Runner) ?? 0f;

                if (HammerTimer.Expired(Runner))
                {
                    EliminateHammerHolder();
                    return;
                }
            }
            else
            {
                HammerTimeLeft = 0f;
            }
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
        HammerTimeLeft = 0f;

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

        RemoveCurrentHammer();
    }

    private void OnHammerTimerChanged()
    {
        if (HammerTimer.IsRunning)
        {
            MinigameHUDController.Instance?.ShowHammerTimer();
        }
        else
        {
            MinigameHUDController.Instance?.HideHammerTimer();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowHammerTimer()
    {
        MinigameHUDController.Instance?.ShowHammerTimer();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_HideHammerTimer()
    {
        MinigameHUDController.Instance?.HideHammerTimer();
    }

    public override void Render()
    {
        if (MinigameHUDController.Instance == null)
            return;

        MinigameHUDController.Instance.UpdateHammerTimer(HammerTimeLeft);
    }
}