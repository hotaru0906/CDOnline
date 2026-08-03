using Fusion;
using UnityEngine;

public class TilePersistentVFX : NetworkBehaviour
{
    [Header("Persistent Tile VFX")]
    [SerializeField] private ParticleSystem vfxPrefab;
    [SerializeField] private Vector3 vfxOffset = new Vector3(0f, 0.2f, 0f);

    private ParticleSystem _vfxInstance;

    public override void Spawned()
    {
        base.Spawned();

        if (vfxPrefab == null)
            return;

        if (HasStateAuthority)
        {
            RPC_ShowPersistentVfx();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowPersistentVfx()
    {
        if (_vfxInstance != null)
            return;

        if (vfxPrefab == null)
            return;

        GameObject vfxObject = Instantiate(vfxPrefab.gameObject, transform, false);
        vfxObject.transform.localPosition = vfxOffset;
        vfxObject.transform.localRotation = Quaternion.identity;

        _vfxInstance = vfxObject.GetComponent<ParticleSystem>();

        if (_vfxInstance == null)
        {
            Debug.LogWarning("TilePersistentVFX: prefab does not contain a ParticleSystem.");
            return;
        }

        var main = _vfxInstance.main;
        main.loop = true;
        main.playOnAwake = false;

        _vfxInstance.Play(true);
    }
}
