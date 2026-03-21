using Fusion;
using UnityEngine;

public class PlayerNetworkData : NetworkBehaviour
{
    public static PlayerNetworkData Local;

    [Networked, OnChangedRender(nameof(OnPlayerNameChanged))]
    public NetworkString<_16> PlayerName { get; set; }

    [Networked] public int ColorID { get; set; }
    [Networked] public bool IsReady { get; set; }
    
    [Networked, OnChangedRender(nameof(OnCharacterIndexChanged))]
    public int CharacterIndex { get; set; }

    public override void Spawned()
    {
        // xác định player local
        if (Object.HasInputAuthority)
        {
            Local = this;
            
            // Load saved character selection từ CharacterSelectionData (PlayerPrefs)
            string savedName = CharacterSelectionData.PlayerName;
            int savedIndex = CharacterSelectionData.SelectedCharacterIndex;
            
            // Nếu tên là mặc định "Player" thì dùng Player + ID
            if (savedName == "Player")
            {
                savedName = $"Player {Object.InputAuthority.PlayerId}";
            }
            
            // Sync lên network cho người khác thấy
            RPC_SetPlayerName(savedName);
            RPC_SetCharacterIndex(savedIndex);
        }

        // Host sets default name for new players
        if (HasStateAuthority)
        {
            if (string.IsNullOrEmpty(PlayerName.ToString()))
            {
                int playerNumber = Object.InputAuthority.PlayerId;
                PlayerName = $"Player {playerNumber}";
            }
        }
        
        // Cập nhật model ngay sau khi spawn (vì OnChangedRender không trigger nếu giá trị không đổi)
        UpdateCharacterModel();
    }
    
    /// <summary>
    /// Cập nhật model nhân vật - gọi trực tiếp khi cần
    /// </summary>
    public void UpdateCharacterModel()
    {
        var modelSwitcher = GetComponent<PlayerModelSwitcher>();
        if (modelSwitcher != null)
        {
            modelSwitcher.SetCharacterModel(CharacterIndex);
        }
    }

    private void OnPlayerNameChanged()
    {
        // Called when name changes on any client
        Debug.Log($"[PlayerNetworkData] Name changed to: {PlayerName}");
    }

    private void OnCharacterIndexChanged()
    {
        // Called when character index changes - update model visibility
        var modelSwitcher = GetComponent<PlayerModelSwitcher>();
        if (modelSwitcher != null)
        {
            modelSwitcher.SetCharacterModel(CharacterIndex);
        }
        Debug.Log($"[PlayerNetworkData] Character index changed to: {CharacterIndex}");
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

    public void SetCharacterIndex(int index)
    {
        if (!HasInputAuthority) return;

        RPC_SetCharacterIndex(index);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SetReady(bool value)
    {
        IsReady = value;
        Debug.Log($"[PlayerNetworkData] Player {Object.InputAuthority.PlayerId} IsReady = {value}");
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

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SetCharacterIndex(int value)
    {
        CharacterIndex = Mathf.Clamp(value, 0, 3);
    }
}