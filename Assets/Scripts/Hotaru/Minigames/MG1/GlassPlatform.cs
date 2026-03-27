using UnityEngine;

/// <summary>
/// Component gắn sẵn vào từng platform glass trên map.
/// Xử lý collision với player và báo về GlassBridge.
/// </summary>
[RequireComponent(typeof(Collider))]
public class GlassPlatform : MonoBehaviour
{
    [Header("Platform Settings")]
    [SerializeField] private GlassBridge bridge;
    [SerializeField] private int rowIndex;
    [SerializeField] private bool isLeft;
    
    private bool isBroken = false;

    public int RowIndex => rowIndex;
    public bool IsLeft => isLeft;

    private void OnTriggerEnter(Collider other)
    {
        if (bridge == null || isBroken) return;
        
        // Kiểm tra có phải player không
        if (!other.TryGetComponent(out PlayerController player)) return;
        
        // Chỉ xử lý nếu player có state authority
        if (!player.Object.HasStateAuthority) return;
        
        Debug.Log($"[GlassPlatform] Player stepped on row {rowIndex}, isLeft: {isLeft}");
        
        // Kiểm tra platform có an toàn không
        bool isSafe = bridge.IsPlatformSafe(rowIndex, isLeft);
        
        if (!isSafe)
        {
            Debug.Log($"[GlassPlatform] Platform is UNSAFE! Breaking...");
            isBroken = true;
            bridge.BreakPlatform(this);
            gameObject.SetActive(false);
        }
        else
        {
            Debug.Log($"[GlassPlatform] Platform is SAFE");
        }
    }

    private void OnEnable()
    {
        isBroken = false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = isLeft ? Color.cyan : Color.magenta;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.3f);
        
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 0.5f, 
            $"Row {rowIndex}\n{(isLeft ? "Left" : "Right")}"
        );
    }
#endif
}
