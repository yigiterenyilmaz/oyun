using UnityEngine;

[CreateAssetMenu(menuName = "SkillTree/PassiveIncomeProduct")]
public class PassiveIncomeProduct : ScriptableObject
{
    public string id;
    public string displayName;
    [TextArea(2, 4)] public string description;
    public Sprite icon;

    public int cost; //satın alma fiyatı
    [Range(0f, 0.99f)] public float sellRatio = 0.6f; //satış fiyatı = cost * sellRatio

    public float minIncomePerTick; //her tick'te kazanılacak minimum para
    public float maxIncomePerTick; //her tick'te kazanılacak maximum para
}
