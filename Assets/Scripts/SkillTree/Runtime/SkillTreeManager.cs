using System;
using System.Collections.Generic;
using UnityEngine;

public class SkillTreeManager : MonoBehaviour
{
    public static SkillTreeManager Instance { get; private set; }

    public SkillDatabase database;
    private HashSet<string> unlockedSkillIds = new HashSet<string>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public bool IsUnlocked(string skillId)
    {
        return unlockedSkillIds.Contains(skillId); //skill açık mı diye kontrol eder
    }

    public bool CanUnlock(Skill skill) //açılabilir mi diye bakar
    {
        if (skill == null)
            return false;

        if (IsUnlocked(skill.id))
            return false; //zaten açılmışsa false

        if (!GameStatManager.Instance.HasEnoughWealth(skill.cost))
            return false; //yeterli para yoksa false

        if (skill.prerequisites != null)
        {
            foreach (Skill prerequisite in skill.prerequisites)
            {
                if (!IsUnlocked(prerequisite.id))
                    return false; //öncelikler açılmış mı?
            }
        }

        return true;
    }

    public bool TryUnlock(string skillId)
    {
        Skill skill = database.GetById(skillId); //id den skilli çeker

        if (skill == null)
            return false;
        if (!CanUnlock(skill))
            return false;//açılamıyorsa olmaz.

        GameStatManager.Instance.TrySpendWealth(skill.cost);//parayı  öder

        unlockedSkillIds.Add(skillId);//açılan skillere eklenir.

        if (skill.effects != null)
        {
            foreach (SkillEffect effect in skill.effects)
            {
                effect.Apply(); //skillerin etkilerini işler.
            }
        }

        SkillEvents.OnSkillUnlocked?.Invoke(skill);//skill açma action u atar

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

    private void OnDisable()
    {
        SkillEvents.OnSkillUnlockRequested -= HandleUnlockRequest;
    }

    private void HandleUnlockRequest(String skillId)
    {
        TryUnlock(skillId);
    }
}
