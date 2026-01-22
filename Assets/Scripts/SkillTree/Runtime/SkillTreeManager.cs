using System;
using System.Collections.Generic;
using UnityEngine;

public class SkillTreeManager : MonoBehaviour
{
    public SkillDatabase database;
    private HashSet<string> unlockedSkillIds = new HashSet<string>();

    public bool IsUnlocked(string skillId)
    {
<<<<<<< HEAD
        return unlockedSkillIds.Contains(skillId);
=======
        return false;
>>>>>>> 41e649cc898f2d8fc79d28a95260f6f2a584cbb5
    }

    public bool CanUnlock(Skill skill)
    {
<<<<<<< HEAD
        if (skill == null)
            return false;

        if (IsUnlocked(skill.id))
            return false;

        if (!GameStatManager.Instance.HasEnoughWealth(skill.cost))
            return false;

        if (skill.prerequisites != null)
        {
            foreach (Skill prerequisite in skill.prerequisites)
            {
                if (!IsUnlocked(prerequisite.id))
                    return false;
            }
        }

        return true;
=======
        return false;
>>>>>>> 41e649cc898f2d8fc79d28a95260f6f2a584cbb5
    }

    public bool TryUnlock(string skillId)
    {
        Skill skill = database.GetById(skillId);
<<<<<<< HEAD

        if (skill == null)
            return false;
        if (!CanUnlock(skill))
            return false;

        GameStatManager.Instance.TrySpendWealth(skill.cost);

        unlockedSkillIds.Add(skillId);

        if (skill.effects != null)
        {
            foreach (SkillEffect effect in skill.effects)
            {
                effect.Apply();
            }
        }

=======
        
        if(skill == null) 
          return false;
        if(CanUnlock(skill) == false) 
          return false;

        unlockedSkillIds.Add(skillId);
>>>>>>> 41e649cc898f2d8fc79d28a95260f6f2a584cbb5
        SkillEvents.OnSkillUnlocked?.Invoke(skill);

        return true;
    }

    public List<Skill> GetAvailableSkills()
    {
<<<<<<< HEAD
        List<Skill> availableSkills = new List<Skill>();

        foreach (Skill skill in database.allSkills)
        {
            if (CanUnlock(skill))
            {
                availableSkills.Add(skill);
            }
        }

        return availableSkills;
=======
        return null;
>>>>>>> 41e649cc898f2d8fc79d28a95260f6f2a584cbb5
    }

    private void OnEnable()
    {
        SkillEvents.OnSkillUnlockRequested += HandleUnlockRequest;
    }

    private void HandleUnlockRequest(String skillId)
    {
        TryUnlock(skillId);
    }
}
