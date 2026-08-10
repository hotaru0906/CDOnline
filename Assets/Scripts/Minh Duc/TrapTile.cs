using System.Collections;
using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

public class TrapTile : NetworkBehaviour
{
    [Header("VFX")]
    [SerializeField] private GameObject explosionPrefab;

    public void Trigger(Vector3 position)
    {
        Debug.Log($"Trigger called on: {gameObject.name}");

        if (!HasStateAuthority)
            return;

        RPC_PlayTrap(position);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayTrap(Vector3 position)
    {
        StartCoroutine(PlayTrapSequence(position));
    }

    private IEnumerator PlayTrapSequence(Vector3 position)
    {
        if (explosionPrefab != null)
        {
            GameObject obj = Instantiate(
                explosionPrefab,
                position,
                Quaternion.identity);

            Destroy(obj, 5f);
        }

        yield return new WaitForSeconds(0.5f);
    }
}