using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "SkillTree/Effects/UnlockEventEffect")]
public class UnlockEventEffect : SkillEffect
{
<<<<<<< HEAD
    public List<Event> eventsToBeUnlocked;

    public override void Apply()
    {
        foreach (Event evt in eventsToBeUnlocked)
        {
            RandomEventManager.Instance.UnlockEvent(evt);
        }
=======
    public List<string> eventsToBeUnlocked;

    public override void Apply()
    {
        
>>>>>>> 41e649cc898f2d8fc79d28a95260f6f2a584cbb5
    }
}
