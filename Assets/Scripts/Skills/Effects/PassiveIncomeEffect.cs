using UnityEngine;

[CreateAssetMenu(menuName = "SkillTree/Effects/PassiveIncome")]
public class PassiveIncomeEffect : SkillEffect
{
    public float incomePerSecond = 10f; //saniyede kazanılacak para

    public override void Apply()
    {
        if (SkillTreeManager.Instance != null)
        {
            SkillTreeManager.Instance.RegisterPassiveIncome(incomePerSecond);
        }
    }
}
