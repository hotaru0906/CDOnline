using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoomItems : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI roomNameText;
    [SerializeField] private TextMeshProUGUI playerCountText;
    [SerializeField] private Button joinButton;

    [Header("Room Data")]
    [SerializeField] private SessionInfo sessionInfo;
    [SerializeField] private LobbyRunner lobbyRunner;
    private string _roomName;
    private int _currentPlayers;
    private int _maxPlayers;

    public void SetUp(SessionInfo session, LobbyRunner runner)
    {
        sessionInfo = session;
        lobbyRunner = runner;

        _roomName = sessionInfo.Name;
        _currentPlayers = sessionInfo.PlayerCount;
        _maxPlayers = sessionInfo.MaxPlayers;

        roomNameText.text = _roomName;
        playerCountText.text = $"{_currentPlayers}/{_maxPlayers}";
        joinButton.onClick.AddListener(() => lobbyRunner.JoinSession(_roomName));
    }

    //public void Initialize(string roomName, int currentPlayers, int maxPlayers, Action onJoinClicked)
    //     {
    //         _roomName = roomName;
    //         _currentPlayers = currentPlayers;
    //         _maxPlayers = maxPlayers;

    //         roomNameText.text = roomName;
    //         playerCountText.text = $"{currentPlayers}/{maxPlayers}";
    //         joinButton.onClick.AddListener(() => onJoinClicked?.Invoke());
    //     }
}