using System.Collections.Generic;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyUI: MonoBehaviour
{
    [SerializeField] private LobbyRunner lobbyRunner;

    [Header("UI References")]
    [SerializeField] TMP_InputField roomNameInput;
    [SerializeField] Transform roomListParent;
    [SerializeField] RoomItems roomListItemPrefab;
    [SerializeField] Button createRoomButton;

    readonly List<RoomItems> _roomItems = new List<RoomItems>();
    public void UpdateRoomList(List<SessionInfo> sessions)
    {
        // Clear existing room items
        foreach (var item in _roomItems)
        {
            Destroy(item.gameObject);
        }
        _roomItems.Clear();

        // Create new room items
        foreach (var session in sessions)
        {
            var newItem = Instantiate(roomListItemPrefab, roomListParent);
            newItem.SetUp(session, lobbyRunner);
            newItem.gameObject.SetActive(true);
            _roomItems.Add(newItem);
        }
    }

    private void Start()
    {
        createRoomButton.onClick.AddListener(CreateRoom);
    }
    private void CreateRoom()
    {
        var roomName = roomNameInput.text;
        if (string.IsNullOrEmpty(roomName))
        {
            Debug.LogWarning("Room name cannot be empty.");
            return;
        }
        lobbyRunner.CreateSession(roomName);
    }
}