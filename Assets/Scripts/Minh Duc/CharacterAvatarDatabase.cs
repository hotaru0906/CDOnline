using UnityEngine;

public class CharacterAvatarDatabase : MonoBehaviour
{
    public static CharacterAvatarDatabase Instance;

    [SerializeField] private Sprite[] avatars;

    private void Awake()
    {
        Instance = this;
    }

    public Sprite GetAvatar(int index)
    {
        if (index < 0 || index >= avatars.Length)
            return null;

        return avatars[index];
    }
}