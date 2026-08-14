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
    public int CharacterIndex { get; private set; }


    [Networked, OnChangedRender(nameof(OnScoreChanged))]
    public int Score { get; set; }

    // ===== Battle (Phase D/E - Final Battle Core) =====
    // Không dùng OnChangedRender ở đây — detect thay đổi + trigger ragdoll/frozen
    // được xử lý trong Render() (so sánh với _lastBattleEliminatedState), đúng pattern
    // PlayerMinigameData đang dùng cho IsDead/IsEliminated.
    [Networked] public float BattleHP { get; private set; }
    [Networked] public NetworkBool IsBattleEliminated { get; private set; }

    private PlayerController _playerController;
    private bool _lastBattleEliminatedState;

    public override void Spawned()
    {
        // xác định player local
        if (Object.HasInputAuthority)
        {
            Local = this;
            LoadingScreen.Hide();

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

        // Cập nhật tên hiển thị
        UpdateNameDisplay();

        _playerController = GetComponent<PlayerController>();
        _lastBattleEliminatedState = IsBattleEliminated;
    }

    public override void Render()
    {
        if (_lastBattleEliminatedState != IsBattleEliminated)
        {
            _lastBattleEliminatedState = IsBattleEliminated;

            if (IsBattleEliminated)
            {
                _playerController?.ActivateRagdoll();
                _playerController?.SetFrozen(true, false); // false = khong hien freezeVFX, dung ragdoll lam feedback
            }
            else
            {
                _playerController?.DeactivateRagdoll();
                _playerController?.SetFrozen(false);
            }
        }
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
        Debug.Log($"[PlayerNetworkData] Name changed to: {PlayerName}");

        UpdateNameDisplay();
        if (BoardHUDController.Instance != null)
        {
            BoardHUDController.Instance.RefreshPlayerNames();
        }
    }

    public void UpdateNameDisplay()
    {
        var nameDisplay = GetComponentInChildren<PlayerNameDisplay>();
        if (nameDisplay != null)
        {
            nameDisplay.UpdateNameText(PlayerName.ToString());
        }
    }

    private void OnCharacterIndexChanged()
    {
        var modelSwitcher = GetComponent<PlayerModelSwitcher>();
        if (modelSwitcher != null)
        {
            modelSwitcher.SetCharacterModel(CharacterIndex);
        }

        Debug.Log($"[PlayerNetworkData] Character index changed to: {CharacterIndex}");
    }

    private void OnScoreChanged()
    {
        Debug.Log($"[PlayerNetworkData] {PlayerName} score changed to: {Score}");

        if (ScoreboardManager.Instance != null)
        {
            ScoreboardManager.Instance.RefreshFromPlayers();
        }
    }

    public void AddScore(int amount)
    {
        if (!HasStateAuthority) return;
        Score += amount;
    }

    public void SetScore(int value)
    {
        if (!HasStateAuthority) return;
        Score = value;
    }

    public void ResetScore()
    {
        if (!HasStateAuthority) return;
        Score = 0;
    }

    // ===== Battle API (Host only) =====

    /// <summary>
    /// Set BattleHP tối đa khi bắt đầu battle (Host only).
    /// </summary>
    public void ResetBattleHP(float maxHP)
    {
        if (!HasStateAuthority) return;
        BattleHP = maxHP;
        IsBattleEliminated = false;
    }

    /// <summary>
    /// Trừ/set BattleHP (Host only). Không tự trigger elimination —
    /// PlayerBattleController là nơi quyết định khi nào BattleHP <= 0 thì gọi SetBattleEliminated.
    /// </summary>
    public void SetBattleHP(float value)
    {
        if (!HasStateAuthority) return;
        BattleHP = Mathf.Max(0f, value);
    }

    /// <summary>
    /// Đánh dấu đã bị loại trong battle (Host only). Cờ riêng, không liên quan
    /// PlayerMinigameData.IsEliminated. Ragdoll/frozen được trigger ở Render()
    /// trên MỌI client khi cờ này đổi, giống pattern PlayerMinigameData.
    /// </summary>
    public void SetBattleEliminated(bool eliminated)
    {
        if (!HasStateAuthority) return;
        IsBattleEliminated = eliminated;
    }

    public void ToggleReady()
    {
        if (!HasInputAuthority) return;

        RPC_SetReady(!IsReady);
    }

    public void SetReady(bool ready)
    {
        if (!HasInputAuthority) return;

        RPC_SetReady(ready);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SetCharacterIndex(int value)
    {
        CharacterIndex = Mathf.Clamp(value, 0, 3);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SavePlayerCharacter(
                Object.InputAuthority.PlayerId,
                CharacterIndex);

            Debug.Log(
                $"[Character Save] Player={Object.InputAuthority.PlayerId} Character={CharacterIndex}");
        }
        else
        {
            Debug.LogWarning("[Character Save] GameManager.Instance == null");
        }
    }
    
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SetReady(bool value)
    {
        IsReady = value;
        Debug.Log($"[PlayerNetworkData] Player {Object.InputAuthority.PlayerId} IsReady = {value}");
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