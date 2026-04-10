using UnityEngine;

public class ChairTrigger : MonoBehaviour
{
    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Player") && Input.GetKeyDown(KeyCode.E))
        {
            PlayerSit player = other.GetComponent<PlayerSit>();

            if (player != null)
            {
                // GỌI HÀM ĐÚNG (tùy bạn đặt tên)
                player.SendMessage("SitDown"); // cách an toàn nếu chưa public
            }
        }
    }
}