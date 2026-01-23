using UnityEngine;

[CreateAssetMenu(menuName = "SkillTree/MiniGame")]
public class MiniGameData : ScriptableObject
{
    public string id; //mini game in idsi
    public string displayName; //mini game in ismi
    public Sprite icon; //mini game in iconu
    public string description; //mini game in açıklaması.
    public Skill requiredSkill; //mini game için gerekli olan skill.
}
