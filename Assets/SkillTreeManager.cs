using System.Collections.Generic;
using UnityEngine;

public class SkillTreeManager : MonoBehaviour
{
    public SkillDatabase database;
    private HashSet<string> unlockedSkillIds = new HashSet<string>();

    public bool IsUnlocked(string skillId)
    {
        return false;
    }

    public bool CanUnlock(Skill skill)
    {
        return false;
    }

    public bool TryUnlock(string skillId)
    {

        return false;
    }

    public List<Skill> GetAvailableSkills()
    {
        return null;
    }
}
