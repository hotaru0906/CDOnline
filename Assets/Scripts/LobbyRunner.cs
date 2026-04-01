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

        // Show loading when game starts - waiting for lobby connection
        LoadingScreen.Show("Connecting to server...");

        await _BasicSpawner.StartLobbyAndRunner();
    }

    public async void CreateSession(string sessionName)
    {
        Debug.Log($"[LobbyRunner] Creating session: {sessionName}");
        
        // Show loading when creating room
        LoadingScreen.Show("Creating room...");
        
        if (_BasicSpawner == null)
        {
            _BasicSpawner = BasicSpawner.Instance ?? FindAnyObjectByType<BasicSpawner>();
        }

        if (_BasicSpawner == null)
        {
            Debug.LogError("[LobbyRunner] Cannot create session without BasicSpawner.");
            LoadingScreen.Hide();
            return;
        }
        var sceneToLoad = SceneRef.FromIndex(1);
        await _BasicSpawner.StartHost(sessionName, sceneToLoad);
    }

    public async void JoinSession(string sessionName)
    {
        Debug.Log($"[LobbyRunner] Joining session: {sessionName}");
        
        // Show loading when joining room
        LoadingScreen.Show("Joining room...");
        
        if (_BasicSpawner == null)
        {
            _BasicSpawner = BasicSpawner.Instance ?? FindAnyObjectByType<BasicSpawner>();
        }

        if (_BasicSpawner == null)
        {
            Debug.LogError("[LobbyRunner] Cannot join session without BasicSpawner.");
            LoadingScreen.Hide();
            return;
        }
        await _BasicSpawner.StartClient(sessionName);
    }
}
