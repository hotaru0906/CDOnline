using UnityEngine;
using System.Collections;
using System.Collections.Generic;

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
    [SerializeField] private string walkBoolParam = "isWalking";
    private Animator _modelAnimator;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;

    [Header("Shield VFX")]
    [SerializeField] private GameObject shieldVfxPrefab;

    [Header("Glove VFX")]
    [SerializeField] private GameObject gloveVfxInstance; // kéo sẵn object Glove (đã đặt vị trí trong scene) vào đây
    [SerializeField] private string gloveTrigger = "Active";
    [SerializeField] private float gloveTotalDuration = 2.5f;
    private Animator _gloveAnimator;
    private Coroutine _gloveRoutine;

    [Header("Teleport VFX (Position Swap)")]
    [SerializeField] private GameObject tpSelectionInstance; // pre-placed trong scene
    [SerializeField] private GameObject tpBurstInstance;     // pre-placed trong scene, particle tự chạy
    [SerializeField] private float tpBurstDuration = 2f;
    private Coroutine _tpBurstRoutine;

    [Header("Wings VFX (Rush Forward)")]
    [SerializeField] private GameObject wingsVfxInstance; // pre-placed trong scene, giống Glove/TP
    [SerializeField] private float flyRiseDuration = 0.5f;
    [SerializeField] private float flyHoldDuration = 0.5f;
    [SerializeField] private float flyDescendDuration = 1f;
    [SerializeField] private float flyRiseOffset = 2f; // độ cao nhấc lên so với vị trí hiện tại (VD y=1 -> y=3)
    private Coroutine _flyRoutine;

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
    private GameObject _shieldVfxInstance;
    private Coroutine _visualSetupRoutine;
    private Coroutine _moveRoutine;
    private readonly Queue<int> _movementQueue = new();
    private bool _movementRunning = false;

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

    public void SetShieldActive(bool active)
    {
        if (_shieldVfxInstance == null)
            CreateShieldVfxInstance();

        if (_shieldVfxInstance == null)
            return;

        _shieldVfxInstance.SetActive(active);
    }

    private void CreateShieldVfxInstance()
    {
        if (_shieldVfxInstance != null)
            return;

        if (shieldVfxPrefab == null)
        {
            Debug.LogWarning("Shield VFX Prefab is missing.");
            return;
        }

        _shieldVfxInstance = Instantiate(shieldVfxPrefab, transform);

        _shieldVfxInstance.transform.localPosition = Vector3.up * 1.2f;
        _shieldVfxInstance.transform.localRotation = Quaternion.identity;
        _shieldVfxInstance.transform.localScale = Vector3.one;

        _shieldVfxInstance.SetActive(false);
    }
    public void SetGlovePreview(bool active)
    {
        if (gloveVfxInstance == null) return;

        // Nếu đang chạy animation thật (confirm PushBack), không can thiệp
        if (_gloveRoutine != null) return;

        if (_gloveAnimator == null)
            _gloveAnimator = gloveVfxInstance.GetComponentInChildren<Animator>();

        gloveVfxInstance.SetActive(active);
    }
    private void CreateGloveVfxInstance()
    {
        if (gloveVfxInstance == null)
        {
            Debug.LogWarning("Glove VFX object is missing — kéo reference vào Inspector.");
            return;
        }

        if (_gloveAnimator == null)
            _gloveAnimator = gloveVfxInstance.GetComponentInChildren<Animator>();

        gloveVfxInstance.SetActive(false);
    }
    public void PlayGloveHit()
    {
        if (gloveVfxInstance == null)
        {
            Debug.LogWarning("Glove VFX object is missing — kéo reference vào Inspector.");
            return;
        }

        if (_gloveAnimator == null)
            CreateGloveVfxInstance();

        if (_gloveRoutine != null) StopCoroutine(_gloveRoutine);
        _gloveRoutine = StartCoroutine(GloveRoutine());
    }

    private IEnumerator GloveRoutine()
    {
        gloveVfxInstance.SetActive(true);
        _gloveAnimator?.SetTrigger(gloveTrigger);

        yield return new WaitForSeconds(gloveTotalDuration);

        gloveVfxInstance.SetActive(false);
        _gloveRoutine = null;
    }
    public void SetTPSelectionPreview(bool active)
    {
        if (tpSelectionInstance == null) return;
        tpSelectionInstance.SetActive(active);
    }

    public void PlayTPBurst()
    {
        if (tpBurstInstance == null) return;

        if (_tpBurstRoutine != null) StopCoroutine(_tpBurstRoutine);
        _tpBurstRoutine = StartCoroutine(TPBurstRoutine());
    }

    private IEnumerator TPBurstRoutine()
    {
        tpBurstInstance.SetActive(true);
        yield return new WaitForSeconds(tpBurstDuration);
        tpBurstInstance.SetActive(false);
        _tpBurstRoutine = null;
    }
    public void PlayRushForwardFly(int targetNodeID)
    {
        if (_flyRoutine != null) StopCoroutine(_flyRoutine);
        _flyRoutine = StartCoroutine(FlyRoutine(targetNodeID));
    }
    private IEnumerator FlyRoutine(int targetNodeID)
    {
        var path = BoardNodePath.Instance;
        var node = path?.GetNodeByID(targetNodeID);
        if (node == null) yield break;

        IsMoving = true;
        if (wingsVfxInstance != null) wingsVfxInstance.SetActive(true);

        Vector3 startPos = transform.position;
        Vector3 targetPos = node.GetSpawnPosition(playerSlotIndex) + Vector3.up * 0.5f; // khớp offset landing dùng chung toàn hệ thống
        Vector3 peakPos = new Vector3(startPos.x, startPos.y + flyRiseOffset, startPos.z);

        Vector3 dir = targetPos - startPos;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir);

        // 1. Rise — nhảy thẳng lên tại chỗ
        float elapsed = 0f;
        while (elapsed < flyRiseDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / flyRiseDuration);
            transform.position = Vector3.Lerp(startPos, peakPos, t);
            yield return null;
        }
        transform.position = peakPos;

        // 2. Hold — giữ nguyên trên không
        yield return new WaitForSeconds(flyHoldDuration);

        // 3. Descend — bay ngang + hạ xuống cùng lúc, đáp đúng ô đích
        elapsed = 0f;
        while (elapsed < flyDescendDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / flyDescendDuration);
            transform.position = Vector3.Lerp(peakPos, targetPos, t);
            yield return null;
        }
        transform.position = targetPos;

        CurrentNodeID = targetNodeID;

        if (wingsVfxInstance != null) wingsVfxInstance.SetActive(false);

        IsMoving = false;
        _flyRoutine = null;
        OnMoveFinished?.Invoke(this);
    }
    public void AnimateMovement(int[] pathNodeIDs)
    {
        if (pathNodeIDs == null || pathNodeIDs.Length == 0)
            return;

        foreach (int nodeID in pathNodeIDs)
        {
            _movementQueue.Enqueue(nodeID);
        }

        if (_movementRunning)
            return;

        _movementRunning = true;
        _moveRoutine = StartCoroutine(ProcessMovementQueue());
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

        _spawnedModelVisual = Instantiate(source, parent);
        _spawnedModelVisual.name = $"BoardVisual_P{ownerPlayerId}";
        _spawnedModelVisual.transform.localPosition = Vector3.zero;
        _spawnedModelVisual.transform.localRotation = Quaternion.identity;

        RemoveRuntimeComponents(_spawnedModelVisual);

        // NEW: cache Animator ngay sau khi model sẵn sàng
        _modelAnimator = _spawnedModelVisual.GetComponentInChildren<Animator>(true);

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
    private void SetWalking(bool active)
    {
        if (_modelAnimator == null) return;
        _modelAnimator.SetBool(walkBoolParam, active);
    }
    private IEnumerator ProcessMovementQueue()
    {
        IsMoving = true;
        SetWalking(true);

        while (_movementQueue.Count > 0)
        {
            int nodeID = _movementQueue.Dequeue();
            yield return MoveSingleStep(nodeID);
        }

        var finalNode = BoardNodePath.Instance?.GetNodeByID(CurrentNodeID);

        if (finalNode != null)
        {
            int count = BoardManager.Instance.GetPlayerCountOnNode(CurrentNodeID);

            if (count <= 1)
                transform.position = finalNode.GetCenterPosition() + Vector3.up * 0.5f;
            else
                transform.position = finalNode.GetSpawnPosition(playerSlotIndex) + Vector3.up * 0.5f;
        }

        SetWalking(false);
        IsMoving = false;
        _movementRunning = false;

        OnMoveFinished?.Invoke(this);
    }
    private IEnumerator MoveSingleStep(int nodeID)
    {
        var node = BoardNodePath.Instance?.GetNodeByID(nodeID);

        if (node == null)
            yield break;

        Vector3 from = transform.position;
        Vector3 to = node.WorldPosition + Vector3.up * 0.5f;

        Vector3 dir = to - from;
        dir.y = 0;

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
        float elapsed = 0;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            transform.position = Vector3.Lerp(from, to, t); // bỏ hop, chỉ Lerp thẳng

            yield return null;
        }

        transform.position = to;

        CurrentNodeID = nodeID;

        PlayStepSound();
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
