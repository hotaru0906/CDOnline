using UnityEngine;
using UnityEngine.Audio;

public class MusicManager : MonoBehaviour
{
    public AudioSource audioSource;
    public AudioClip[] musicTracks;
    public AudioMixer audioMixer;

    private int currentTrack = 0;

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        PlayTrack(currentTrack);
    }

    void Update()
    {
        if (!audioSource.isPlaying)
        {
            NextTrack();
        }
    }

    void PlayTrack(int index)
    {
        audioSource.clip = musicTracks[index];
        audioSource.Play();
    }

    void NextTrack()
    {
        currentTrack = (currentTrack + 1) % musicTracks.Length;
        PlayTrack(currentTrack);
    }

    public void SetVolume(float value)
    {
        audioMixer.SetFloat("MusicVolume", Mathf.Log10(value) * 20);
    }
}
