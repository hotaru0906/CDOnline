using Fusion;
using UnityEngine;

/// <summary>
/// Simple visual representation for the player using a capsule.
/// Handles basic visual feedback (color for local/remote, jump squash/stretch).
/// Replace this with proper character model and animator later.
/// </summary>
public class PlayerVisual : NetworkBehaviour
{
    #region Configuration
    [Header("Visual Settings")]
    [SerializeField] private Color localPlayerColor = Color.green;
    [SerializeField] private Color remotePlayerColor = Color.blue;

    [Header("References")]
    [SerializeField] private Renderer bodyRenderer;
    #endregion

    #region Components
    private PlayerController _playerController;
    private MaterialPropertyBlock _propertyBlock;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        _playerController = GetComponent<PlayerController>();
        _propertyBlock = new MaterialPropertyBlock();

        // Auto-find renderer if not assigned
        if (bodyRenderer == null)
        {
            bodyRenderer = GetComponentInChildren<Renderer>();
        }
    }

    public override void Spawned()
    {
        // Set color based on whether this is local player or remote
        SetPlayerColor(HasInputAuthority ? localPlayerColor : remotePlayerColor);
    }
    #endregion

    #region Visual Updates
    /// <summary>
    /// Set player body color.
    /// </summary>
    public void SetPlayerColor(Color color)
    {
        if (bodyRenderer == null) return;

        _propertyBlock.SetColor("_Color", color);
        _propertyBlock.SetColor("_BaseColor", color); // For URP
        bodyRenderer.SetPropertyBlock(_propertyBlock);
    }

    /// <summary>
    /// Flash color effect (e.g., when taking damage).
    /// </summary>
    public void FlashColor(Color flashColor, float duration = 0.1f)
    {
        // TODO: Implement flash coroutine
        StartCoroutine(FlashColorCoroutine(flashColor, duration));
    }

    private System.Collections.IEnumerator FlashColorCoroutine(Color flashColor, float duration)
    {
        Color originalColor = HasInputAuthority ? localPlayerColor : remotePlayerColor;
        SetPlayerColor(flashColor);
        yield return new WaitForSeconds(duration);
        SetPlayerColor(originalColor);
    }
    #endregion

    #region Editor Setup Helper
#if UNITY_EDITOR
    /// <summary>
    /// Helper to create a basic capsule visual in editor.
    /// </summary>
    [ContextMenu("Setup Capsule Visual")]
    private void SetupCapsuleVisual()
    {
        // Create capsule child
        GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        capsule.name = "Body";
        capsule.transform.SetParent(transform);
        capsule.transform.localPosition = new Vector3(0, 1, 0);
        capsule.transform.localRotation = Quaternion.identity;
        capsule.transform.localScale = Vector3.one;

        // Remove collider (CharacterController handles collision)
        DestroyImmediate(capsule.GetComponent<Collider>());

        // Assign renderer
        bodyRenderer = capsule.GetComponent<Renderer>();

        Debug.Log("[PlayerVisual] Capsule visual created!");
    }
#endif
    #endregion
}
