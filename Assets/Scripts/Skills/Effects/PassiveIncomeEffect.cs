using UnityEngine;

[System.Serializable]
public class PassiveIncomeEffect : SkillEffect
{
    public float minIncomePerTick = 5f; //her tick'te kazanılacak minimum para
    public float maxIncomePerTick = 15f; //her tick'te kazanılacak maximum para

    public override void Apply()
    {
        if (SkillTreeManager.Instance != null)
        {
            SkillTreeManager.Instance.RegisterPassiveIncome(minIncomePerTick, maxIncomePerTick);
        }
    }
}
