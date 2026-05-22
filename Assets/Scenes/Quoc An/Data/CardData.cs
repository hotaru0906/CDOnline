using UnityEngine;

[CreateAssetMenu(fileName = "NewCard", menuName = "Inventory/Card Data")]
public class CardData : ScriptableObject
{
    public string cardName;
    public Sprite cardArt;
    public string description;
    public int quantity;
}