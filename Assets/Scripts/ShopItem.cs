using UnityEngine;

[CreateAssetMenu(fileName = "NewShopItem", menuName = "GunBlackMarket/ShopItem")]
public class ShopItem : ScriptableObject
{
    public string itemName;
    public int cost;
    [Min(1)] public int unlockDay = 1;
    public GameObject prefab; // The physical object to spawn
    // public Sprite icon; // Can add later for UI
    
    [TextArea]
    public string description;
}
