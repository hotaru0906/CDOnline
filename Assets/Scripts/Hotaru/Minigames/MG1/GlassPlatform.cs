using UnityEngine;

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

        if (!other.TryGetComponent(out PlayerController player)) return;

        // Chỉ xử lý nếu đây là LOCAL player (người đang control)
        if (!player.Object.HasInputAuthority) return;

        Debug.Log($"[GlassPlatform] Local player stepped on row {rowIndex}, isLeft: {isLeft}");

        // Gửi request đến Host thông qua GlassBridge
        bridge.RequestCheckPlatform(rowIndex, isLeft);
    }
    public void Break()
    {
        if (isBroken) return;

        isBroken = true;
        gameObject.SetActive(false);

        Debug.Log($"[GlassPlatform] Platform BROKEN at row {rowIndex}, isLeft: {isLeft}");
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