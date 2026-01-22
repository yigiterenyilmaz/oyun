using System;
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
        Skill skill = database.GetById(skillId);
        
        if(skill == null) 
          return false;
        if(CanUnlock(skill) == false) 
          return false;

        unlockedSkillIds.Add(skillId);
        SkillEvents.OnSkillUnlocked?.Invoke(skill);

        return true;
    }

    public List<Skill> GetAvailableSkills()
    {
        return null;
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
