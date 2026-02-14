[System.Serializable]
public class DirectPassiveIncomeEffect : SkillEffect
{
    public float incomePerSecond; //bu skill açılınca saniye başına eklenen sabit gelir

    public override void Apply()
    {
        if (SkillTreeManager.Instance != null)
        {
            SkillTreeManager.Instance.AddDirectPassiveIncome(incomePerSecond);
        }
    }
}
