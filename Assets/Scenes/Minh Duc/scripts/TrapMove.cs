using UnityEngine;

public class TrapMove : MonoBehaviour
{
    [Header("Movement")]
    public float speed = 10f;

    void Update()
    {
        transform.Translate(Vector3.forward * speed * Time.deltaTime);
    }

    void OnTriggerEnter(Collider other)
    {
        // Kiểm tra có phải player không
        if (!other.TryGetComponent(out PlayerController player)) return;
        
        // Chỉ xử lý trên local player
        if (!player.Object.HasInputAuthority) return;
        
        // Lấy PlayerMinigameData và cho player thua
        PlayerMinigameData minigameData = player.GetComponent<PlayerMinigameData>();
        if (minigameData != null)
        {
            minigameData.Die();
            Debug.Log("[TrapMove] Player hit trap - eliminated!");
        }
    }
}