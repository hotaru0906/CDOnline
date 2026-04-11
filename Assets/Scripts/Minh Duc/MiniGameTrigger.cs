using UnityEngine;

public class MiniGameTrigger : MonoBehaviour
{
    public bool isMiniGame1;

    private void OnTriggerEnter(Collider other)
    {
        PlayerMovement1 player = other.GetComponent<PlayerMovement1>();

        if (player != null)
        {
            if (isMiniGame1)
                player.SetMiniGame1();
            else
                player.SetMiniGame2();
        }
    }
}