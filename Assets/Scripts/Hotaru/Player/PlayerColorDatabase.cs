using UnityEngine;

public static class PlayerColorDatabase
{
    public static Color[] Colors =
    {
        Color.red,
        Color.blue,
        Color.green,
        Color.yellow,
        Color.cyan,
        Color.magenta
    };

    public static int ColorCount => Colors.Length;

    public static Color GetColor(int index)
    {
        if (index < 0 || index >= Colors.Length)
            return Color.white;

        return Colors[index];
    }
}