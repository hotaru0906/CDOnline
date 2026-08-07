using System.Collections.Generic;
using UnityEngine;

public class JackpotChest : MonoBehaviour
{
    [SerializeField] private int nodeID; // phải khớp với nodeID của BoardNode gắn trên tile này
    [SerializeField] private Animator chestAnimator;
    [SerializeField] private string openTrigger = "ChestOpen";

    private static readonly Dictionary<int, JackpotChest> _registry = new();

    private void Awake()
    {
        _registry[nodeID] = this;
    }

    private void OnDestroy()
    {
        if (_registry.TryGetValue(nodeID, out var c) && c == this)
            _registry.Remove(nodeID);
    }

    public static bool TryGet(int nodeID, out JackpotChest chest) =>
        _registry.TryGetValue(nodeID, out chest);

    public void PlayOpen()
    {
        if (chestAnimator != null)
            chestAnimator.SetTrigger(openTrigger);
    }
}