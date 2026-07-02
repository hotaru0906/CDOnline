using Fusion;
using UnityEngine;

/// <summary>
/// Base class cho tất cả collectable trên Board.
/// </summary>
public abstract class BoardCollectable : NetworkBehaviour
{
    [SerializeField]
    protected GameObject visual;

    [Networked]
    public NetworkBool IsCollected { get; set; }

    [Header("Hiệu ứng")]

    [SerializeField]
    protected float rotateSpeed = 90f;

    [SerializeField]
    protected float floatAmplitude = 0.15f;

    [SerializeField]
    protected float floatSpeed = 2f;

    private Vector3 initialLocalPosition;

    protected virtual void Start()
    {
        if (visual != null)
        {
            initialLocalPosition = visual.transform.localPosition;
        }
    }

    protected virtual void Update()
    {
        if (visual == null)
            return;

        if (visual == null || !visual.activeSelf)
            return;

        // Quay quanh trục Y
        visual.transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.Self);

        // Hiệu ứng lơ lửng
        float offsetY = Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;

        visual.transform.localPosition =
            initialLocalPosition + Vector3.up * offsetY;
    }

    public override void Render()
    {
        if (visual == null)
            return;

        visual.SetActive(!IsCollected);
    }

    public virtual void Show()
    {
        if (visual != null)
            visual.SetActive(true);
    }

    public virtual void Hide()
    {
        if (visual != null)
            visual.SetActive(false);
    }
}