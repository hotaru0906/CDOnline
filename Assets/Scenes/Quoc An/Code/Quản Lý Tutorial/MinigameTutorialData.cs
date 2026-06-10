using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

[CreateAssetMenu(fileName = "TutorialData_", menuName = "Game/Minigame Tutorial Data")]
public class MinigameTutorialData : ScriptableObject
{
    [Header("Video")]
    public VideoClip tutorialVideo;     // kéo VideoClip vào đây

    [Header("Control Lines")]
    public List<TutorialControlData> controls;  // thêm bớt tùy ý trong Inspector
}