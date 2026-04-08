using UnityEngine;
using System.Collections;

public class VFX : MonoBehaviour
{
    private ParticleSystem[] effects;
    private bool isAiming;

    void Start()
    {
        // Lấy tất cả VFX con
        effects = GetComponentsInChildren<ParticleSystem>();
    }

    void Update()
    {
        isAiming = Input.GetMouseButton(1);

        if (isAiming && Input.GetMouseButtonDown(0))
        {
            StartCoroutine(PlayVFXDelay());
        }
    }

    IEnumerator PlayVFXDelay()
    {
        yield return new WaitForSeconds(0.3f); // ⏱ delay 1 giây

        foreach (ParticleSystem ps in effects)
        {
            if (ps != null)
            {
                ps.Play();
            }
        }
    }
}