using UnityEngine;
using TMPro;

/// <summary>
/// Hiển thị tên player trên World Space Canvas
/// Attach vào GameObject có Canvas (World Space) và TMP_Text
/// CHÚ Ý: Script này phải attach vào Canvas child, KHÔNG phải player root!
/// </summary>
public class PlayerNameDisplay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Canvas nameCanvas;
    
    [Header("Billboard Settings")]
    [Tooltip("Xoay canvas luôn hướng về camera")]
    [SerializeField] private bool lookAtCamera = true;
    
    [Tooltip("Chỉ xoay theo trục Y (giữ text thẳng đứng)")]
    [SerializeField] private bool onlyYAxis = true;
    
    [Header("First Person Settings")]
    [Tooltip("Ẩn tên của local player khi ở First Person")]
    [SerializeField] private bool hideInFirstPerson = true;
    
    private Transform _cameraTransform;
    private Transform _canvasTransform; // Transform riêng cho canvas, không dùng this.transform
    private PlayerNetworkData _playerData;
    private bool _isLocalPlayer;

    private void Start()
    {
        // Cache camera reference
        if (Camera.main != null)
        {
            _cameraTransform = Camera.main.transform;
        }
        
        // Tìm PlayerNetworkData trên parent
        _playerData = GetComponentInParent<PlayerNetworkData>();
        
        // Tìm Canvas nếu chưa assign
        if (nameCanvas == null)
        {
            nameCanvas = GetComponent<Canvas>();
        }
        
        // Cache canvas transform - CHỈ xoay canvas, không xoay player!
        if (nameCanvas != null)
        {
            _canvasTransform = nameCanvas.transform;
        }
        else
        {
            // Fallback - dùng transform hiện tại nhưng warn
            _canvasTransform = transform;
            Debug.LogWarning("[PlayerNameDisplay] No Canvas found! Billboard might rotate player!");
        }
        
        if (_playerData != null)
        {
            // Set tên ban đầu
            UpdateNameText(_playerData.PlayerName.ToString());
            
            // Check xem có phải local player không
            _isLocalPlayer = _playerData.Object != null && _playerData.Object.HasInputAuthority;
        }
    }

    private void LateUpdate()
    {
        // Ẩn tên local player khi ở First Person
        if (hideInFirstPerson && _isLocalPlayer && CameraManager.Instance != null)
        {
            bool shouldHide = CameraManager.Instance.CurrentMode == CameraMode.FirstPerson;
            SetNameVisible(!shouldHide);
        }
        
        if (lookAtCamera && _cameraTransform != null)
        {
            BillboardEffect();
        }
    }
    
    /// <summary>
    /// Ẩn/hiện tên
    /// </summary>
    private void SetNameVisible(bool visible)
    {
        if (nameCanvas != null)
        {
            nameCanvas.enabled = visible;
        }
        else if (nameText != null)
        {
            nameText.enabled = visible;
        }
    }

    /// <summary>
    /// Xoay canvas hướng về camera
    /// CHÚ Ý: Chỉ xoay _canvasTransform, KHÔNG xoay player!
    /// </summary>
    private void BillboardEffect()
    {
        if (_canvasTransform == null || _cameraTransform == null) return;
        
        if (onlyYAxis)
        {
            // Chỉ xoay theo trục Y - giữ text thẳng đứng
            // Tính hướng từ canvas đến camera
            Vector3 lookDir = _cameraTransform.position - _canvasTransform.position;
            lookDir.y = 0; // Chỉ xoay Y
            
            if (lookDir.sqrMagnitude > 0.001f)
            {
                // Xoay canvas để MẶT TRƯỚC hướng về camera
                // Dùng lookDir (không phải -lookDir) vì text cần quay MẶT hướng về camera
                _canvasTransform.rotation = Quaternion.LookRotation(lookDir);
            }
        }
        else
        {
            // Xoay hoàn toàn hướng về camera (kể cả pitch)
            _canvasTransform.LookAt(
                _canvasTransform.position + _cameraTransform.forward,
                _cameraTransform.up
            );
        }
    }

    /// <summary>
    /// Cập nhật text hiển thị tên
    /// Được gọi từ PlayerNetworkData khi tên thay đổi
    /// </summary>
    public void UpdateNameText(string playerName)
    {
        if (nameText != null)
        {
            nameText.text = playerName;
        }
    }

    /// <summary>
    /// Set màu text
    /// </summary>
    public void SetTextColor(Color color)
    {
        if (nameText != null)
        {
            nameText.color = color;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Auto-find TMP_Text nếu chưa assign
        if (nameText == null)
        {
            nameText = GetComponentInChildren<TMP_Text>();
        }
        
        // Auto-find Canvas nếu chưa assign
        if (nameCanvas == null)
        {
            nameCanvas = GetComponent<Canvas>();
        }
    }
#endif
}
