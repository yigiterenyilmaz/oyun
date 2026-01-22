using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "SkillTree/Event")]
public class Event : ScriptableObject
{
    public string id;
    public string displayName;
    public string description;
    public Sprite icon;
    public List<SkillEffect> effects;
}
