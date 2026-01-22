using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "SkillTree/SkillDataBase")]
public class SkillDatabase : ScriptableObject
{
    public List<Skill> allSkills;

    public Skill GetById(string id)
    {
        foreach(Skill skill in allSkills)
        {
            if(skill.id == id)
            {
                return skill;
            }
        }

        Debug.LogError($"Skill not found with id: {id}");
        return null;
    }

    public List<Skill> GetRootSkills()
    {
        List<Skill> rootSkills = new List<Skill>();

        foreach (Skill skill in allSkills)
        {
            if (skill.prerequisites == null || skill.prerequisites.Count == 0)
            {
                rootSkills.Add(skill);
            }
        }

        return rootSkills;
    }
}
