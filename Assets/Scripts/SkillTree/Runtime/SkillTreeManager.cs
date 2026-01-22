using System;
using System.Collections.Generic;
using UnityEngine;

public class SkillTreeManager : MonoBehaviour
{
    public SkillDatabase database;
    private HashSet<string> unlockedSkillIds = new HashSet<string>();

    public bool IsUnlocked(string skillId)
    {
        return unlockedSkillIds.Contains(skillId);
    }

    public bool CanUnlock(Skill skill)
    {
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
    }

    public bool TryUnlock(string skillId)
    {
        Skill skill = database.GetById(skillId);

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

        SkillEvents.OnSkillUnlocked?.Invoke(skill);

        return true;
    }

    public List<Skill> GetAvailableSkills()
    {
        List<Skill> availableSkills = new List<Skill>();

        foreach (Skill skill in database.allSkills)
        {
            if (CanUnlock(skill))
            {
                availableSkills.Add(skill);
            }
        }

        return availableSkills;
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
