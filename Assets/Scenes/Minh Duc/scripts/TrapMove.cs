using UnityEngine;

public class TrapMove : MonoBehaviour
{
    [Header("Movement")]
    public float speed = 10f;

    [Header("Push Settings")]
    [SerializeField] private float pushDistance = 2f; // Khoảng cách đẩy theo Z

    void Update()
    {
        transform.Translate(Vector3.forward * speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<PlayerController>(out var player))
        {
            // Chỉ xử lý trên local player
            if (!player.Object.HasInputAuthority) return;

            // Kiểm tra hit cooldown - nếu đang trong cooldown thì bỏ qua
            if (player.IsInHitCooldown) return;

            // Đơn giản: lấy vị trí player và + Z
            Vector3 currentPos = player.transform.position;
            Vector3 newPos = new Vector3(currentPos.x, currentPos.y, currentPos.z + pushDistance);

            // Di chuyển player đến vị trí mới
            player.RequestTeleport(newPos);
            
            Debug.Log($"[TrapMove] Player pushed! From {currentPos} to {newPos}");
        }
    }
}