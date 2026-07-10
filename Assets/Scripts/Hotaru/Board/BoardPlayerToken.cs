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
    public int ownerPlayerId = -1;

    // Slot cố định của Token trong scene
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

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip jumpSound;

    [SerializeField] private float rotateSpeed = 14f; // toc độ xoay khi di chuyển (độ/giây)

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

    public void RefreshCharacter()
    {
        _currentCharacterIndex = -1;

        if (_spawnedModelVisual != null)
        {
            Destroy(_spawnedModelVisual);
            _spawnedModelVisual = null;
        }

        if (_visualSetupRoutine != null)
            StopCoroutine(_visualSetupRoutine);

        _visualSetupRoutine = StartCoroutine(EnsureVisualReady());
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

        var path = BoardNodePath.Instance;
        var node = path?.GetNodeByID(nodeID);

        if (node == null)
            return;

        transform.position = node.GetSpawnPosition(playerSlotIndex) + Vector3.up * 0.5f;

        // Luôn nhìn về node kế tiếp
        var nextNode = path.GetNodeAfterSteps(node, 1, out _);

        if (nextNode != null && nextNode != node)
        {
            Vector3 dir = nextNode.WorldPosition - node.WorldPosition;
            dir.y = 0f;

            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(dir);
        }
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
            Debug.LogWarning($"[BoardPlayerToken] Cannot find PlayerNetworkData for {ownerPlayerId}");
            return false;
        }

        int characterIndex = 0;

        if (GameManager.Instance != null)
        {
            characterIndex = GameManager.Instance.GetPlayerCharacter(ownerPlayerId);
        }

        Debug.Log(
            $"[BOARD TOKEN] " +
            $"Player={ownerPlayerId} " +
            $"CharacterIndex={characterIndex}");

        if (_spawnedModelVisual != null &&
            _currentCharacterIndex == characterIndex)
        {
            return true;
        }

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

        if (fallbackCharacterModels != null &&
            characterIndex >= 0 &&
            characterIndex < fallbackCharacterModels.Length)
        {
            source = fallbackCharacterModels[characterIndex];
        }

        if (source == null)
        {
            Debug.LogError(
                $"[BoardToken] Missing fallback model for CharacterIndex={characterIndex}");
            return false;
        }

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

        var currentNode = BoardNodePath.Instance?.GetNodeByID(CurrentNodeID);

    if (currentNode != null)
    {
        Vector3 start = transform.position;
        Vector3 center = currentNode.GetCenterPosition() + Vector3.up * 0.5f;

        float elapsed = 0f;
        const float duration = 0.15f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            transform.position = Vector3.Lerp(
                start,
                center,
                elapsed / duration);

            yield return null;
        }

        transform.position = center;
    }

        foreach (int nodeID in pathNodeIDs)
        {
            var node = BoardNodePath.Instance?.GetNodeByID(nodeID);
            if (node == null) continue;

            Vector3 from = transform.position;
            Vector3 to = node.WorldPosition + Vector3.up * 0.5f;

            //========================
            // Quay người trước
            //========================

            Vector3 dir = to - from;
            dir.y = 0f;

            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(dir);

                while (Quaternion.Angle(transform.rotation, targetRotation) > 1f)
                {
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation,
                        targetRotation,
                        Time.deltaTime * rotateSpeed);

                    yield return null;
                }

                transform.rotation = targetRotation;
            }

            float duration = 1f / moveSpeed;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                float hop = Mathf.Sin(t * Mathf.PI) * hopHeight;
                transform.position = Vector3.Lerp(from, to, t) + Vector3.up * hop;

                yield return null;
            }

            transform.position = to;
            CurrentNodeID = nodeID;

            PlayStepSound();
        }

        var finalNode = BoardNodePath.Instance?.GetNodeByID(CurrentNodeID);

        if (finalNode != null)
        {
            int count = BoardManager.Instance.GetPlayerCountOnNode(CurrentNodeID);

            if (count <= 1)
            {
                transform.position =
                    finalNode.GetCenterPosition()
                    + Vector3.up * 0.5f;
            }
            else
            {
                transform.position =
                    finalNode.GetSpawnPosition(playerSlotIndex)
                    + Vector3.up * 0.5f;
            }
        }

        IsMoving = false;

        OnMoveFinished?.Invoke(this);
    }

    
    public void PlayJumpAnimation()
    {
        PlayStepSound();
        StartCoroutine(JumpRoutine());
    }

    private void PlayStepSound()
    {
        if (audioSource != null && jumpSound != null)
        {
            audioSource.PlayOneShot(jumpSound);
        }
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
