using UnityEngine;

[CreateAssetMenu(menuName = "SkillTree/MiniGame")]
public class MiniGameData : ScriptableObject
{
    public string id;
    public string displayName;
    public Sprite icon;
    public string description;
    public Skill requiredSkill;
}
