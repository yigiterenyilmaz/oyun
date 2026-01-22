using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "SkillTree/Skill")]
public class Skill : ScriptableObject
{
    public string id;
    public string displayName;
    public Sprite icon;
    public int cost;
    public List<Skill> prerequisites;
}
