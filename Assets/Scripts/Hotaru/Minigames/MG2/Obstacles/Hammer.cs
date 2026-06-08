using UnityEngine;
using Fusion;

public class Hammer : BaseObstacle
{
    [Header("Hammer Swing")]
    [SerializeField] private Transform hammerHead;

    [SerializeField] private float swingAngle = 60f;
    [SerializeField] private float swingSpeed = 2f;

    [Header("Audio")]
    [SerializeField] private AudioSource swingSound;
    [SerializeField] private AudioSource hitAudioSource;

    private Quaternion _restRotation;
    private float _previousAngle;

    private void Start()
    {
        if (hammerHead != null)
        {
            _restRotation = hammerHead.localRotation;
        }

        _previousAngle = 0f;
    }

    private void Update()
    {
        if (hammerHead == null)
            return;

        float time = Runner != null
            ? (float)Runner.SimulationTime
            : Time.time;

        float angle =
            Mathf.Sin(time * swingSpeed) * swingAngle;

        // Lắc theo trục Z
        hammerHead.localRotation =
            _restRotation *
            Quaternion.Euler(0, 0, angle);

        // Phát tiếng búa quơ khi swing từ tĩnh sang chuyển động
        if (swingSound != null && swingSound.clip != null && _previousAngle <= 0f && angle > 0f)
        {
            // Tạo temporary 3D audio tại vị trí hammer
            GameObject tempAudioObj = new GameObject("TempSwingAudio");
            tempAudioObj.transform.position = transform.position;
            
            AudioSource tempAudio = tempAudioObj.AddComponent<AudioSource>();
            tempAudio.clip = swingSound.clip;
            tempAudio.volume = swingSound.volume;
            tempAudio.pitch = swingSound.pitch;
            tempAudio.spatialBlend = 1f;
            tempAudio.minDistance = 5f;
            tempAudio.maxDistance = 50f;
            tempAudio.rolloffMode = AudioRolloffMode.Logarithmic;
            
            tempAudio.Play();
            
            Destroy(tempAudioObj, tempAudio.clip.length);
        }

        _previousAngle = angle;
    }

    protected override void ApplyEffect(PlayerController player)
    {
        if (!Object.HasStateAuthority)
            return;

        Vector3 pushDir =
            (player.transform.position - hammerHead.position).normalized;

        pushDir.y = 0f;

        Vector3 knockback =
            pushDir * 15f +
            Vector3.up * 3f;

        bool success =
            player.TryApplyHit(knockback);

        if (!success)
            return;

        player.ForceIdle();

        Debug.Log(
            $"[Hammer] Knockback {player.Object.InputAuthority}"
        );
    }

    protected override void PlaySFX()
    {
        if (hitAudioSource != null && hitAudioSource.clip != null)
        {
            // Tạo temporary 3D audio tại vị trí hammer
            GameObject tempAudioObj = new GameObject("TempHitAudio");
            tempAudioObj.transform.position = transform.position;
            
            AudioSource tempAudio = tempAudioObj.AddComponent<AudioSource>();
            tempAudio.clip = hitAudioSource.clip;
            tempAudio.volume = hitAudioSource.volume;
            tempAudio.pitch = hitAudioSource.pitch;
            tempAudio.spatialBlend = 1f;
            tempAudio.minDistance = 5f;
            tempAudio.maxDistance = 50f;
            tempAudio.rolloffMode = AudioRolloffMode.Logarithmic;
            
            tempAudio.Play();
            
            Destroy(tempAudioObj, tempAudio.clip.length);
        }
        else
        {
            base.PlaySFX();
        }
    }
}