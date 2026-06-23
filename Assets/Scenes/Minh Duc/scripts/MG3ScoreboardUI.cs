using TMPro;
using UnityEngine;

public class MG3ScoreboardUI : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;

    private void Awake()
    {
        Debug.Log("MG3ScoreboardUI AWAKE");
    }

    private void OnEnable()
    {
        Debug.Log("MG3ScoreboardUI ENABLE");

        if(titleText != null)
            titleText.text = "MG4 RESULTS";

        ShowPlayers();
    }

    private void ShowPlayers()
    {
        Debug.Log("SHOW PLAYERS");

        var players = FindObjectsByType<PlayerMinigameData>(
            FindObjectsSortMode.None);

        Debug.Log("PLAYERS FOUND = " + players.Length);
    }
}