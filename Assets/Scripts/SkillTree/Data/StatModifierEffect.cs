using UnityEngine;

[CreateAssetMenu(menuName = "SkillTree/Effects/StatModifier")]
public class StatModifierEffect : SkillEffect
{
    public StatType statType;
    public float changeValue;

    public override void Apply()
    {
        GameStatManager.Instance.ModifyStat(statType, changeValue);
    }
}
