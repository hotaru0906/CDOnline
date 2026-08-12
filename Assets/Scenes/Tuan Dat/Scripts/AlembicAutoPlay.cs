using UnityEngine;
using UnityEngine.Formats.Alembic.Importer;

public class AlembicAutoPlay : MonoBehaviour
{
    private AlembicStreamPlayer alembic;

    [SerializeField] private float speed = 1f;

    private void Awake()
    {
        alembic = GetComponent<AlembicStreamPlayer>();
    }

    private void Update()
    {
        if (alembic == null)
            return;

        float duration = alembic.Duration;

        if (duration <= 0f)
            return;

        alembic.CurrentTime += Time.deltaTime * speed;

        if (alembic.CurrentTime >= duration)
        {
            alembic.CurrentTime = 0f;
        }
    }
}