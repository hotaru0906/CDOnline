using System.Collections;
using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

public class TrapTile : NetworkBehaviour
{
    [Header("VFX")]
    [SerializeField] private GameObject explosionPrefab;
    [SerializeField] private GameObject keyBurstPrefab;
    [SerializeField] private GameObject flyingKeyPrefab;

    [SerializeField]
    [Range(1,20)]
    private int flyingKeyCount = 8;

    [SerializeField] private float keyBurstDelay = 0.4f;

    public void Trigger(Vector3 position, int lostKeys)
    {
        Debug.Log($"Trigger called on: {gameObject.name}");
        Debug.Log($"Lost Keys = {lostKeys}");

        if (!HasStateAuthority)
            return;

        RPC_PlayTrap(position, lostKeys);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayTrap(Vector3 position, int lostKeys)
    {
        StartCoroutine(PlayTrapSequence(position, lostKeys));
    }

    private IEnumerator PlayTrapSequence(Vector3 position, int lostKeys)
    {
        if (explosionPrefab != null)
        {
            GameObject obj = Instantiate(
                explosionPrefab,
                position,
                Quaternion.identity);

            Destroy(obj, 5f);
        }

        yield return new WaitForSeconds(keyBurstDelay);

        if (keyBurstPrefab != null)
        {
            GameObject obj = Instantiate(
                keyBurstPrefab,
                position + Vector3.up * 0.2f,
                Quaternion.identity);

            Destroy(obj, 5f);
        }

        if (flyingKeyPrefab != null)
        {
            Debug.Log("=== START SPAWN FLYING KEY ===");

            for (int i = 0; i < lostKeys; i++)
            {
                Vector3 spawnPos = position + Vector3.up * 1.5f;

                GameObject spawnedKey = Instantiate(
                    flyingKeyPrefab,
                    spawnPos,
                    Quaternion.identity);

                Debug.Log($"Spawned FlyingKey {i}: {spawnedKey.name}");
            }
        }
        else
        {
            Debug.LogError("FlyingKey Prefab is NULL!");
        }
    }
}