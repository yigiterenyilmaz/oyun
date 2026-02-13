using UnityEngine;

[CreateAssetMenu(menuName = "SkillTree/Science Section/ScientistData")]
public class ScientistData : ScriptableObject
{
    public string id;                              //benzersiz kimlik
    public string displayName;                     //bilim adamının adı
    [TextArea(2, 8)] public string description;    //bilim adamının açıklaması
    public float intelligenceLevel;                //zeka seviyesi
    public float loyaltyLevel;                     //bağlılık seviyesi
    public float completeCost;                     //eğitimi tamamlamak için gereken efektif para
}
