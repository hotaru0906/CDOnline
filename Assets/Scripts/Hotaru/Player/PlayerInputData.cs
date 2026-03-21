using Fusion;
using UnityEngine;

public struct PlayerInputData : INetworkInput
{
    public Vector2 MoveDirection;
    public Vector3 CameraForward; // Hướng camera để tính movement trên server

    public NetworkButtons Buttons;

    public const int BUTTON_JUMP = 0;
    public const int BUTTON_PUNCH = 1;
    public const int BUTTON_SLIDE = 2;

    public bool IsButtonPressed(int button) => Buttons.IsSet(button);
}