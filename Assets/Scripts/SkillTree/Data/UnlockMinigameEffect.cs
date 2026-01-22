using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "SkillTree/Effects/UnlockMinigameEffect")]
public class UnlockMinigameEffect : SkillEffect
{
    public List<MiniGameData> minigamesToBeUnlocked;

    public override void Apply()
    {
        foreach (MiniGameData minigame in minigamesToBeUnlocked)
        {
            MinigameManager.Instance.UnlockMinigame(minigame);
        }
    }
}
