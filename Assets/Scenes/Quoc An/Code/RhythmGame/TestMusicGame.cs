using UnityEngine;
using RhythmGame;
   public class _ClockTest : MonoBehaviour 
   {
       void Start() => Conductor.Instance.StartSong();
       void Update() => Debug.Log(Conductor.Instance.RawSongPosition.ToString("F3"));
   }