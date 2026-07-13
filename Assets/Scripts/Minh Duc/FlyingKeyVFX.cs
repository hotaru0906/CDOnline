using UnityEngine;

public class FlyingKeyVFX : MonoBehaviour
{
    [Header("Force")]
    [SerializeField] private float minForce = 4f;
    [SerializeField] private float maxForce = 7f;

    [Header("Spin")]
    [SerializeField] private float spinForce = 10f;

    [Header("Life Time")]
    [SerializeField] private float destroyAfter = 2.5f;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponentInChildren<Rigidbody>();

        Debug.Log("RB = " + rb);
    }

    private void Start()
    {
        Debug.Log("FlyingKey Start");

        if (rb == null)
        {
            Debug.LogError("NO RIGIDBODY FOUND!");
            return;
        }

        Vector3 direction = Random.onUnitSphere;
        direction.y = Mathf.Abs(direction.y) + 0.6f;
        direction.Normalize();

        float force = Random.Range(minForce, maxForce);

        rb.AddForce(direction * force, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * spinForce, ForceMode.Impulse);

        Destroy(gameObject, destroyAfter);
    }
}