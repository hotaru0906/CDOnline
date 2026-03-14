using Fusion;
using UnityEngine;

public class PlayerNetworkData : NetworkBehaviour
{
    public static PlayerNetworkData Local;

    [Networked, OnChangedRender(nameof(OnPlayerNameChanged))]
    public NetworkString<_16> PlayerName { get; set; }

    [Networked] public int ColorID { get; set; }
    [Networked] public bool IsReady { get; set; }

    public override void Spawned()
    {
        // xác định player local
        if (Object.HasInputAuthority)
        {
            Local = this;
        }

        // Host sets default name for new players
        if (HasStateAuthority)
        {
            if (string.IsNullOrEmpty(PlayerName.ToString()))
            {
                // Generate default name based on player ref
                int playerNumber = Object.InputAuthority.PlayerId;
                PlayerName = $"Player {playerNumber}";
            }
        }
    }

    private void OnPlayerNameChanged()
    {
        // Called when name changes on any client
        Debug.Log($"[PlayerNetworkData] Name changed to: {PlayerName}");
    }

    public void ToggleReady()
    {
        if (!HasInputAuthority) return;

        RPC_SetReady(!IsReady);
    }

    public void SetPlayerName(string newName)
    {
        if (!HasInputAuthority) return;

        RPC_SetPlayerName(newName);
    }

    public void SetColor(int colorID)
    {
        if (!HasInputAuthority) return;

        RPC_SetColor(colorID);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SetReady(bool value)
    {
        IsReady = value;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SetPlayerName(string value)
    {
        PlayerName = value;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SetColor(int value)
    {
        ColorID = value;
    }
}