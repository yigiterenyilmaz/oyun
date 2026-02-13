using UnityEngine;

[CreateAssetMenu(menuName = "SkillTree/Science Section/ScientistData")]
public class ScientistData : ScriptableObject
{
    public string id;                          //benzersiz kimlik
    public string displayName;                 //bilim adamının adı
    public float completionLevel;              //eğitimin tamamlanması için gereken seviye
}
