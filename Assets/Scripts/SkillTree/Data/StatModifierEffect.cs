using UnityEngine;

[CreateAssetMenu(menuName = "SkillTree/Effects/StatModifier")]
public class StatModifierEffect : SkillEffect
{
    public StatType statType;
    public float changeValue;

    public override void Apply()
    {
<<<<<<< HEAD
        GameStatManager.Instance.ModifyStat(statType, changeValue);
=======
        
>>>>>>> 41e649cc898f2d8fc79d28a95260f6f2a584cbb5
    }
}
