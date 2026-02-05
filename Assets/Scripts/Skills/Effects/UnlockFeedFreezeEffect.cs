using UnityEngine;

[CreateAssetMenu(menuName = "SkillTree/Effects/UnlockFeedFreeze")]
public class UnlockFeedFreezeEffect : SkillEffect
{
    public override void Apply()
    {
        if (SocialMediaManager.Instance != null)
        {
            SocialMediaManager.Instance.UnlockFreezeAbility();
        }
    }
}
