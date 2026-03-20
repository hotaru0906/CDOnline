using Fusion;
using UnityEngine;

/// <summary>
/// Quản lý các model nhân vật trên Player prefab
/// Bật/tắt model dựa trên index đã chọn
/// </summary>
public class PlayerModelSwitcher : NetworkBehaviour
{
    [Header("Character Models")]
    [Tooltip("Danh sách 4 model nhân vật (index 0-3)")]
    [SerializeField] private GameObject[] characterModels;

    private int _currentIndex = -1;
    private PlayerAnimator _playerAnimator;

    private void Awake()
    {
        _playerAnimator = GetComponent<PlayerAnimator>();
    }

    public override void Spawned()
    {
        // Đọc CharacterIndex từ PlayerNetworkData và hiện model tương ứng
        var networkData = GetComponent<PlayerNetworkData>();
        if (networkData != null)
        {
            SetCharacterModel(networkData.CharacterIndex);
        }
        else
        {
            // Fallback: hiện model đầu tiên
            SetCharacterModel(0);
        }
    }

    /// <summary>
    /// Đổi model nhân vật theo index
    /// </summary>
    public void SetCharacterModel(int index)
    {
        if (characterModels == null || characterModels.Length == 0) return;
        
        int clampedIndex = Mathf.Clamp(index, 0, characterModels.Length - 1);
        
        _currentIndex = clampedIndex;
        UpdateModelVisibility();
        
        // Notify PlayerAnimator về model mới
        NotifyAnimator();
    }

    private void UpdateModelVisibility()
    {
        for (int i = 0; i < characterModels.Length; i++)
        {
            if (characterModels[i] != null)
            {
                characterModels[i].SetActive(i == _currentIndex);
            }
        }
        
        Debug.Log($"[PlayerModelSwitcher] Switched to model {_currentIndex}");
    }

    private void NotifyAnimator()
    {
        if (_playerAnimator == null)
        {
            _playerAnimator = GetComponent<PlayerAnimator>();
        }

        if (_playerAnimator != null)
        {
            var activeModel = GetActiveModel();
            _playerAnimator.UpdateAnimatorReference(activeModel);
        }
    }

    /// <summary>
    /// Lấy GameObject của model đang active
    /// </summary>
    public GameObject GetActiveModel()
    {
        if (characterModels == null || _currentIndex < 0 || _currentIndex >= characterModels.Length)
            return null;
        
        return characterModels[_currentIndex];
    }

    private void HideAllModels()
    {
        if (characterModels == null) return;
        
        foreach (var model in characterModels)
        {
            if (model != null)
                model.SetActive(false);
        }
    }

    public int GetCurrentModelIndex() => _currentIndex;
}