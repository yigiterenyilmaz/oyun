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
<<<<<<< HEAD
    public List<SkillEffect> effects;
=======
>>>>>>> 41e649cc898f2d8fc79d28a95260f6f2a584cbb5
}
