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
        var networkData = GetComponent<PlayerNetworkData>();
        if (networkData != null)
        {
            SetCharacterModel(networkData.CharacterIndex);
        }
        else
        {
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

        // Nếu là local player và đang ở First Person (KHÔNG phải Minigame), ẩn model
        if (Object != null && Object.HasInputAuthority && CameraManager.Instance != null)
        {
            var mode = CameraManager.Instance.CurrentMode;
            // Chỉ ẩn khi THỰC SỰ ở First Person, không ẩn khi đang chờ switch sang Minigame
            if (mode == CameraMode.FirstPerson && !CameraManager.Instance.IsPendingSharedCamera)
            {
                SetModelVisible(false);
            }
            else
            {
                // Đảm bảo model được hiện khi không ở First Person
                SetModelVisible(true);
            }
        }
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

        // Thêm debug chi tiết
        bool isLocal = Object != null && Object.HasInputAuthority;
        Debug.Log($"[PlayerModelSwitcher] Switched to model {_currentIndex} | IsLocal: {isLocal} | PlayerName: {gameObject.name}");
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

    /// <summary>
    /// Ẩn/hiện model cho First Person mode
    /// CHỈ được gọi cho local player - KHÔNG ẩn remote players
    /// </summary>
    public void SetModelVisible(bool visible)
    {
        // QUAN TRỌNG: Chỉ ẩn model của local player
        if (Object != null && !Object.HasInputAuthority)
        {
            // Remote player - KHÔNG ẩn, luôn hiển thị
            Debug.Log($"[PlayerModelSwitcher] Skipping SetModelVisible for remote player");
            return;
        }

        var activeModel = GetActiveModel();
        if (activeModel != null)
        {
            // Ẩn tất cả renderers trong model
            var renderers = activeModel.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                renderer.enabled = visible;
            }

            Debug.Log($"[PlayerModelSwitcher] Local player model visibility set to: {visible}");
        }
    }

    /// <summary>
    /// Set layer cho model (để camera culling)
    /// </summary>
    public void SetModelLayer(int layer)
    {
        var activeModel = GetActiveModel();
        if (activeModel != null)
        {
            SetLayerRecursively(activeModel, layer);
        }
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    public int GetCurrentModelIndex() => _currentIndex;
    public void HideCharacter()
    {
        var activeModel = GetActiveModel();
        if (activeModel != null)
        {
            activeModel.SetActive(false);
        }
    }

    public void ShowCharacter()
    {
        var activeModel = GetActiveModel();
        if (activeModel != null)
        {
            activeModel.SetActive(true);
        }

        NotifyAnimator();
    }
}