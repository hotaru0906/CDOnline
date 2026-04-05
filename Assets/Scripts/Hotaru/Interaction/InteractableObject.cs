using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Base class cho các object có thể tương tác
/// Đặt object vào layer "Interactable" để player có thể detect
/// </summary>
public class InteractableObject : MonoBehaviour
{
    [Header("Interaction Settings")]
    [Tooltip("Phím để tương tác (mặc định E)")]
    [SerializeField] protected KeyCode interactionKey = KeyCode.E;
    
    [Header("UI Display")]
    [Tooltip("Text hiển thị trên prompt (vd: 'Customize Character')")]
    [SerializeField] protected string promptText = "Interact";
    
    [Tooltip("Hiển thị phím tắt (vd: 'E')")]
    [SerializeField] protected bool showKeyHint = true;
    
    [Tooltip("Icon cho prompt (optional)")]
    [SerializeField] protected Sprite promptIcon;
    
    [Header("Interaction Range")]
    [Tooltip("Khoảng cách tối đa để tương tác")]
    [SerializeField] protected float interactionRange = 3f;
    
    [Header("Events")]
    public UnityEvent OnInteract;
    
    // Properties
    public string PromptText => promptText;
    public KeyCode InteractionKey => interactionKey;
    public bool ShowKeyHint => showKeyHint;
    public Sprite PromptIcon => promptIcon;
    public float InteractionRange => interactionRange;
    
    protected bool _isBeingInteracted;
    
    /// <summary>
    /// Kiểm tra xem có thể tương tác không (override để thêm điều kiện)
    /// </summary>
    public virtual bool CanInteract()
    {
        return enabled && gameObject.activeInHierarchy;
    }
    
    /// <summary>
    /// Gọi khi player nhấn phím tương tác
    /// </summary>
    public virtual void Interact()
    {
        if (!CanInteract()) return;
        
        _isBeingInteracted = true;
        OnInteract?.Invoke();
        _isBeingInteracted = false;
    }
    
    /// <summary>
    /// Clear target (dùng khi cần)
    /// </summary>
    public virtual void EndInteraction()
    {
        _isBeingInteracted = false;
    }
    
    public string GetFullPromptText()
    {
        if (showKeyHint)
            return $"[{interactionKey}] {promptText}";
        return promptText;
    }

#if UNITY_EDITOR
    protected virtual void OnDrawGizmosSelected()
    {
        // Vẽ range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
    
    protected virtual void OnValidate()
    {
        // Đảm bảo object ở layer Interactable
        if (gameObject.layer != LayerMask.NameToLayer("Interactable"))
        {
            Debug.LogWarning($"[InteractableObject] {gameObject.name} should be on 'Interactable' layer for detection to work!");
        }
    }
#endif
}
