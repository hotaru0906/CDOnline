using UnityEngine;
using UnityEngine.Video;

[CreateAssetMenu(menuName = "Game/Tutorial Data")]
public class TutorialData : ScriptableObject
{
    [Header("Minigame Info")]
    public string minigameName;
    
    [TextArea(2, 4)]
    public string description;

    [Header("Control Instructions")]
    public ControlInstruction[] controls;

    [Header("Tutorial Video")]
    public VideoClip tutorialVideo;

    [Header("UI Settings")]
    public float videoDuration = 10f;
}

[System.Serializable]
public class ControlInstruction
{
    [Tooltip("Icon representing the button/key")]
    public Sprite buttonIcon;
    
    [Tooltip("Short description of what this control does")]
    public string actionText;
    
    [Tooltip("Optional: Key code for display")]
    public string keyName;
}
