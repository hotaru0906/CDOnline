using UnityEngine;
using System.Collections;

/// <summary>
/// Token đại diện cho 1 player trên bàn cờ.
/// Phase 0: MonoBehaviour thuần — movement được điều khiển hoàn toàn bởi BoardManager qua RPC.
/// Cần pre-place 4 token trong BoardScene, mỗi token set sẵn playerSlotIndex (0-3).
/// </summary>
public class BoardPlayerToken : MonoBehaviour
{
    [Header("Identity")]
    public int ownerPlayerId = -1;    // PlayerId của player sở hữu
    public int playerSlotIndex = 0;   // 0-3, khớp với slot trong BoardManager.TurnOrder

    [Header("Visual")]
    [SerializeField] private Renderer tokenRenderer;
    [SerializeField] private Transform modelAnchor;
    [SerializeField] private Transform diceAnchor;
    [SerializeField] private bool usePlayerModelVisual = true;
    [SerializeField] private bool hideTokenWhenModelLoaded = true;
    [SerializeField] private GameObject[] fallbackCharacterModels;
    [SerializeField] private float visualSetupTimeout = 4f;
    [SerializeField] private float visualRetryInterval = 0.15f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 4f;    // nodes per second
    [SerializeField] private float hopHeight = 0.4f;  // độ cao nhảy mỗi ô

    [Header("Debug")]
    [SerializeField] private bool showLabel = true;

    public int CurrentNodeID { get; private set; } = 0;
    public bool IsMoving { get; private set; } = false;

    public Transform DiceAnchor => diceAnchor;

    // Callback khi animation di chuyển xong
    public System.Action<BoardPlayerToken> OnMoveFinished;

    private static readonly Color[] SlotColors =
    {
        new Color(0.9f, 0.2f, 0.2f),   // slot 0 — đỏ
        new Color(0.2f, 0.4f, 0.9f),   // slot 1 — xanh dương
        new Color(0.2f, 0.8f, 0.2f),   // slot 2 — xanh lá
        new Color(0.95f, 0.8f, 0.1f)   // slot 3 — vàng
    };

    private GameObject _spawnedModelVisual;
    private int _currentCharacterIndex = -1;
    private Coroutine _visualSetupRoutine;
    private Coroutine _moveRoutine;

    /// <summary>
    /// Gọi bởi BoardManager khi board phase bắt đầu để gán player và snap về node 0.
    /// </summary>
    public void Initialize(int playerId, int slotIndex, int startNodeID)
    {
        ownerPlayerId = playerId;
        playerSlotIndex = slotIndex;
        _currentCharacterIndex = -1;

        if (_spawnedModelVisual != null)
        {
            Destroy(_spawnedModelVisual);
            _spawnedModelVisual = null;
        }

        if (tokenRenderer != null)
            tokenRenderer.material.color = SlotColors[Mathf.Clamp(slotIndex, 0, 3)];

        if (_visualSetupRoutine != null)
            StopCoroutine(_visualSetupRoutine);

        if (usePlayerModelVisual && tokenRenderer != null)
            tokenRenderer.enabled = false;

        _visualSetupRoutine = StartCoroutine(EnsureVisualReady());
        SnapToNode(startNodeID);
    }

    private void OnDestroy()
    {
        if (_visualSetupRoutine != null)
            StopCoroutine(_visualSetupRoutine);

        if (_moveRoutine != null)
            StopCoroutine(_moveRoutine);

        if (_spawnedModelVisual != null)
            Destroy(_spawnedModelVisual);
    }

    /// <summary>Teleport ngay lập tức đến node chỉ định.</summary>
    public void SnapToNode(int nodeID)
    {
        CurrentNodeID = nodeID;
        var node = BoardNodePath.Instance?.GetNodeByID(nodeID);
        if (node != null)
            transform.position = node.WorldPosition + Vector3.up * 0.5f;
    }

    /// <summary>
    /// Gọi bởi BoardManager (qua RPC) để chạy animation di chuyển.
    /// pathNodeIDs: danh sách nodeID cần đi qua theo thứ tự.
    /// </summary>
    public void AnimateMovement(int[] pathNodeIDs)
    {
        if (_moveRoutine != null)
            StopCoroutine(_moveRoutine);

        _moveRoutine = StartCoroutine(MoveCoroutine(pathNodeIDs));
    }

    private IEnumerator EnsureVisualReady()
    {
        if (!usePlayerModelVisual)
        {
            ShowTokenVisual(true);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < visualSetupTimeout)
        {
            if (TryBuildPlayerVisual())
                yield break;

            elapsed += visualRetryInterval;
            yield return new WaitForSeconds(visualRetryInterval);
        }

        Debug.LogWarning($"[BoardPlayerToken] Failed to bind player visual for P{ownerPlayerId}, falling back to token mesh.");
        ShowTokenVisual(true);
    }

    private bool TryBuildPlayerVisual()
    {
        if (!usePlayerModelVisual)
            return true;

        var playerData = FindPlayerData(ownerPlayerId);
        if (playerData == null)
        {
            return false;
        }

        int characterIndex = Mathf.Clamp(playerData.CharacterIndex, 0, 3);
        if (_spawnedModelVisual != null && _currentCharacterIndex == characterIndex)
            return true;

        return BuildModelVisual(playerData, characterIndex);
    }

    private PlayerNetworkData FindPlayerData(int playerId)
    {
        var allPlayers = FindObjectsByType<PlayerNetworkData>(FindObjectsSortMode.None);
        foreach (var p in allPlayers)
        {
            if (p == null || p.Object == null) continue;
            if (p.Object.InputAuthority.PlayerId == playerId)
                return p;
        }
        return null;
    }

    private bool BuildModelVisual(PlayerNetworkData playerData, int characterIndex)
    {
        if (_spawnedModelVisual != null)
            Destroy(_spawnedModelVisual);

        var parent = modelAnchor != null ? modelAnchor : transform;
        GameObject source = null;

        var switcher = playerData.GetComponent<PlayerModelSwitcher>();
        if (switcher != null)
            source = switcher.GetActiveModel();

        if (source == null)
        {
            playerData.UpdateCharacterModel();
            if (switcher != null)
                source = switcher.GetActiveModel();
        }

        if (source == null && fallbackCharacterModels != null && characterIndex < fallbackCharacterModels.Length)
            source = fallbackCharacterModels[characterIndex];

        if (source == null)
        {
            _spawnedModelVisual = null;
            _currentCharacterIndex = -1;
            return false;
        }

        _spawnedModelVisual = Instantiate(source, parent);
        _spawnedModelVisual.name = $"BoardVisual_P{ownerPlayerId}";
        _spawnedModelVisual.transform.localPosition = Vector3.zero;
        _spawnedModelVisual.transform.localRotation = Quaternion.identity;

        RemoveRuntimeComponents(_spawnedModelVisual);

        _currentCharacterIndex = characterIndex;
        ShowTokenVisual(!hideTokenWhenModelLoaded);

        return true;
    }

    private void RemoveRuntimeComponents(GameObject go)
    {
        var colliders = go.GetComponentsInChildren<Collider>(true);
        foreach (var c in colliders)
            Destroy(c);

        var rigidbodies = go.GetComponentsInChildren<Rigidbody>(true);
        foreach (var rb in rigidbodies)
            Destroy(rb);

        var behaviours = go.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var b in behaviours)
        {
            if (b is Animator) continue;
            Destroy(b);
        }
    }

    private void ShowTokenVisual(bool visible)
    {
        if (tokenRenderer != null)
            tokenRenderer.enabled = visible;
    }

    private IEnumerator MoveCoroutine(int[] pathNodeIDs)
    {
        IsMoving = true;

        foreach (int nodeID in pathNodeIDs)
        {
            var node = BoardNodePath.Instance?.GetNodeByID(nodeID);
            if (node == null) continue;

            Vector3 from = transform.position;
            Vector3 to = node.WorldPosition + Vector3.up * 0.5f;
            float duration = 1f / moveSpeed;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                float hop = Mathf.Sin(t * Mathf.PI) * hopHeight;
                transform.position = Vector3.Lerp(from, to, t) + Vector3.up * hop;

                Vector3 flatDir = to - transform.position;
                flatDir.y = 0f;
                if (flatDir.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flatDir), t);

                yield return null;
            }

            transform.position = to;
            CurrentNodeID = nodeID;

            yield return new WaitForSeconds(0.08f); // pause nhỏ giữa mỗi ô
        }

        IsMoving = false;
        _moveRoutine = null;
        OnMoveFinished?.Invoke(this);
    }
    public void PlayJumpAnimation()
    {
        StartCoroutine(JumpRoutine());
    }

    private IEnumerator JumpRoutine()
    {
        Vector3 origin = transform.position;
        float duration = 0.35f;
        float height = 0.8f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float hop = Mathf.Sin(t * Mathf.PI) * height;
            transform.position = origin + Vector3.up * hop;
            yield return null;
        }

        transform.position = origin;
    }

    private void OnGUI()
    {
        if (!showLabel || Camera.main == null) return;

        Vector3 sp = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 1.2f);
        if (sp.z > 0)
            GUI.Label(
                new Rect(sp.x - 40, Screen.height - sp.y - 20, 80, 20),
                $"P{ownerPlayerId} N{CurrentNodeID}"
            );
    }
}
