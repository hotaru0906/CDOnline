using UnityEngine;
using Fusion;

public class LobbyRunner : MonoBehaviour
{
    public BasicSpawner _BasicSpawner;
    private void Awake()
    {
        if (_BasicSpawner == null)
        {
            _BasicSpawner = BasicSpawner.Instance ?? FindAnyObjectByType<BasicSpawner>();
        }
    }
    async void Start()
    {
        if (_BasicSpawner == null)
        {
            Debug.LogError("[LobbyRunner] No BasicSpawner found, cannot start lobby.");
            return;
        }

        await _BasicSpawner.StartLobbyAndRunner();
    }

    public async void CreateSession(string sessionName)
    {
        Debug.Log($"[LobbyRunner] Creating session: {sessionName}");
        if (_BasicSpawner == null)
        {
            _BasicSpawner = BasicSpawner.Instance ?? FindAnyObjectByType<BasicSpawner>();
        }

        if (_BasicSpawner == null)
        {
            Debug.LogError("[LobbyRunner] Cannot create session without BasicSpawner.");
            return;
        }
        var sceneToLoad = SceneRef.FromIndex(1);
        await _BasicSpawner.StartHost(sessionName, sceneToLoad);
    }

    public async void JoinSession(string sessionName)
    {
        Debug.Log($"[LobbyRunner] Joining session: {sessionName}");
        if (_BasicSpawner == null)
        {
            _BasicSpawner = BasicSpawner.Instance ?? FindAnyObjectByType<BasicSpawner>();
        }

        if (_BasicSpawner == null)
        {
            Debug.LogError("[LobbyRunner] Cannot join session without BasicSpawner.");
            return;
        }
        await _BasicSpawner.StartClient(sessionName);
    }
}
