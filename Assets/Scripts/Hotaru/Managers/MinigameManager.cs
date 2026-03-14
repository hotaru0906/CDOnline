using UnityEngine;
using System.Collections.Generic;

public class MinigameManager : MonoBehaviour
{
    [SerializeField] private List<MinigameData> allMinigames;

    private List<MinigameData> votingMinigames = new List<MinigameData>();

    public List<MinigameData> GetVotingMinigames(int count)
    {
        votingMinigames.Clear();

        List<MinigameData> pool = new List<MinigameData>(allMinigames);

        for (int i = 0; i < count; i++)
        {
            int index = Random.Range(0, pool.Count);

            votingMinigames.Add(pool[index]);

            pool.RemoveAt(index);
        }

        return votingMinigames;
    }
}