using UnityEngine;

public class TrapSpawner : MonoBehaviour
{
    public GameObject trapLowPrefab;
    public GameObject trapHighPrefab;

    public Transform spawnLow;
    public Transform spawnHigh;

    public float gameDuration = 90f;

    public float startDelay = 2f;
    public float endDelay = 0.5f;

    private float timer;
    private float gameTimer;

    void Update()
    {
        if (trapLowPrefab == null || trapHighPrefab == null) return;
        if (spawnLow == null || spawnHigh == null) return;

        if (gameTimer >= gameDuration) return;

        gameTimer += Time.deltaTime;

        float t = Mathf.Clamp01(gameTimer / gameDuration);
        float currentDelay = Mathf.Lerp(startDelay, endDelay, t);

        timer += Time.deltaTime;

        while (timer >= currentDelay)
        {
            SpawnTrap();
            timer -= currentDelay;
        }
    }

    void SpawnTrap()
    {
        int random = Random.Range(0, 2);

        if (random == 0)
            Instantiate(trapLowPrefab, spawnLow.position, Quaternion.Euler(0, 0, 0));
        else
            Instantiate(trapHighPrefab, spawnHigh.position, spawnHigh.rotation);
    }
}