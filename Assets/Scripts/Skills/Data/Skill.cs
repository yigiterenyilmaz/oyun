using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "SkillTree/Skill")]
public class Skill : ScriptableObject
{
    public string id; //skill id si
    public string displayName; //skill
    public Sprite icon; //skill in iconu
    public int cost; //skill in bedeli
    public List<Skill> prerequisites; //skill in ön koşulları
    [SerializeReference] public List<SkillEffect> effects = new List<SkillEffect>(); //skill açılınca oluşan efektler
    public List<Skill> blocksSkills; //bu skill açılınca hangi skillerin sonsuza kadar kilitlenmesi gerektiği
}
