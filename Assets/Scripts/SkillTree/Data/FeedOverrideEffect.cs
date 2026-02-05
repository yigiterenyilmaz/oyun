using UnityEngine;

[CreateAssetMenu(menuName = "SkillTree/Effects/FeedOverride")]
public class FeedOverrideEffect : SkillEffect
{
    public TopicType targetTopic; //feed'de hangi topic öne çıkacak

    public override void Apply()
    {
        if (SocialMediaManager.Instance != null)
        {
            SocialMediaManager.Instance.SetPlayerOverride(targetTopic);
        }
    }
}
