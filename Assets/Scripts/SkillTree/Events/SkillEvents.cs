using System;

public static class SkillEvents
{
    public static Action<Skill> OnSkillUnlocked;
    public static Action<string> OnSkillUnlockRequested;
}
