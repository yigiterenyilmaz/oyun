using UnityEngine;

[CreateAssetMenu(menuName = "SkillTree/Effects/UnlockFeedSlow")]
public class UnlockFeedSlowEffect : SkillEffect
{
    public override void Apply()
    {
        if (SocialMediaManager.Instance != null)
        {
            SocialMediaManager.Instance.UnlockSlowAbility();
        }
    }
}
