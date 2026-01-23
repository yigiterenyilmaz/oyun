using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "SkillTree/Event")]
public class Event : ScriptableObject
{
    public string id;
    public string displayName;
    public string description;
    public Sprite icon;
    public List<EventChoice> choices;

    public bool isRepeatable = true;
    public float weight = 1f;
    public List<Skill> requiredSkills;
    public List<StatCondition> statConditions;
    public int minGamePhase = 0;
    public int maxGamePhase = 99;
}
