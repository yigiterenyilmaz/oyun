using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "SkillTree/Effects/UnlockEventEffect")]
public class UnlockEventEffect : SkillEffect
{
    public List<Event> eventsToBeUnlocked;

    public override void Apply()
    {
        foreach (Event evt in eventsToBeUnlocked)
        {
            RandomEventManager.Instance.UnlockEvent(evt);
        }
    }
}
