using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public List<GameObject> allPlayers = new List<GameObject>();
    private List<GameObject> finishedPlayers = new List<GameObject>();

    private int currentSpectateIndex = 0;
    private List<GameObject> remainingPlayers = new List<GameObject>();

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        // 🎮 CHUYỂN CAMERA BẰNG PHÍM
        if (remainingPlayers.Count == 0) return;

        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            currentSpectateIndex++;
            if (currentSpectateIndex >= remainingPlayers.Count)
                currentSpectateIndex = 0;

            SwitchToPlayer(currentSpectateIndex);
        }

        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            currentSpectateIndex--;
            if (currentSpectateIndex < 0)
                currentSpectateIndex = remainingPlayers.Count - 1;

            SwitchToPlayer(currentSpectateIndex);
        }
    }

    public void PlayerFinished(GameObject player)
    {
        if (finishedPlayers.Contains(player)) return;

        finishedPlayers.Add(player);

        int rank = finishedPlayers.Count;
        Debug.Log(player.name + " hang: " + rank);

        //  người đầu tiên
        if (rank == 1)
        {
            Debug.Log(" WINNER");

            PlayerMovement1 movement = player.GetComponent<PlayerMovement1>();
            if (movement != null)
                movement.enabled = false;

            UpdateRemainingPlayers();
            SwitchToPlayer(0);
        }
        else
        {
            // cập nhật lại danh sách khi có người finish thêm
            UpdateRemainingPlayers();
        }
    }

    void UpdateRemainingPlayers()
    {
        remainingPlayers.Clear();

        foreach (GameObject p in allPlayers)
        {
            if (!finishedPlayers.Contains(p))
            {
                remainingPlayers.Add(p);
            }
        }

        currentSpectateIndex = 0;
    }

    void SwitchToPlayer(int index)
    {
        if (remainingPlayers.Count == 0) return;

        ThirdPersonCamera cam = FindObjectOfType<ThirdPersonCamera>();

        if (cam != null)
        {
            cam.SetTarget(remainingPlayers[index].transform);
            Debug.Log(" Dang spectate: " + remainingPlayers[index].name);
        }
    }
}