using System;
using System.Collections.Generic;
using UnityEngine;

public class SkillTreeManager : MonoBehaviour
{
    public static SkillTreeManager Instance { get; private set; }

    public SkillDatabase database;
    private HashSet<string> unlockedSkillIds = new HashSet<string>();
    private HashSet<string> blockedSkillIds = new HashSet<string>(); //sonsuza kadar kilitli skiller

    //passive income
    private List<PassiveIncomeRange> passiveIncomeRanges = new List<PassiveIncomeRange>();
    private float passiveIncomeTimer = 0f;
    private const float PASSIVE_INCOME_INTERVAL = 5f; //kaç saniyede bir gelir eklenir

    //events
    public static event Action<float> OnPassiveIncomeChanged; //yeni toplam pasif gelir

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Update()
    {
        //pasif geliri 5 saniyede bir uygula
        if (passiveIncomeRanges.Count > 0 && GameStatManager.Instance != null)
        {
            passiveIncomeTimer += Time.deltaTime;
            if (passiveIncomeTimer >= PASSIVE_INCOME_INTERVAL)
            {
                float totalIncome = 0f;
                foreach (var range in passiveIncomeRanges)
                {
                    totalIncome += UnityEngine.Random.Range(range.min, range.max);
                }
                GameStatManager.Instance.AddWealth(totalIncome * PASSIVE_INCOME_INTERVAL);
                passiveIncomeTimer = 0f;
            }
        }
    }

    public bool IsUnlocked(string skillId)
    {
        return unlockedSkillIds.Contains(skillId); //skill açık mı diye kontrol eder
    }

    public bool IsBlocked(string skillId)
    {
        return blockedSkillIds.Contains(skillId); //skill kalıcı olarak kilitli mi
    }

    public bool CanUnlock(Skill skill) //açılabilir mi diye bakar
    {
        if (skill == null)
            return false;

        if (IsUnlocked(skill.id))
            return false; //zaten açılmışsa false

        if (IsBlocked(skill.id))
            return false; //kalıcı olarak kilitliyse false

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

        //bu skill açılınca hangi skiller kalıcı olarak kilitlenecek
        if (skill.blocksSkills != null)
        {
            foreach (Skill blockedSkill in skill.blocksSkills)
            {
                if (blockedSkill != null && !IsUnlocked(blockedSkill.id))
                {
                    blockedSkillIds.Add(blockedSkill.id);
                    SkillEvents.OnSkillBlocked?.Invoke(blockedSkill);
                }
            }
        }

        SkillEvents.OnSkillUnlocked?.Invoke(skill);//skill açma action u atar

        return true;
    }

    //effect'ler bu metodu çağırarak pasif gelir aralığı kaydeder
    public void RegisterPassiveIncome(float min, float max)
    {
        passiveIncomeRanges.Add(new PassiveIncomeRange(min, max));
        OnPassiveIncomeChanged?.Invoke(passiveIncomeRanges.Count);
    }

    public int GetPassiveIncomeSourceCount()
    {
        return passiveIncomeRanges.Count;
    }

    //pasif gelir aralığını tutan basit struct
    private struct PassiveIncomeRange
    {
        public float min;
        public float max;
        public PassiveIncomeRange(float min, float max)
        {
            this.min = min;
            this.max = max;
        }
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
