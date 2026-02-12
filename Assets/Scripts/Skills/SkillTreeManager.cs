using System;
using System.Collections.Generic;
using UnityEngine;

public class SkillTreeManager : MonoBehaviour
{
    public static SkillTreeManager Instance { get; private set; }

    public SkillDatabase database;
    private HashSet<string> unlockedSkillIds = new HashSet<string>();
    private HashSet<string> blockedSkillIds = new HashSet<string>(); //sonsuza kadar kilitli skiller

    //passive income — ürün tabanlı sistem
    private HashSet<PassiveIncomeProduct> unlockedProducts = new HashSet<PassiveIncomeProduct>();
    private Dictionary<PassiveIncomeProduct, int> ownedProducts = new Dictionary<PassiveIncomeProduct, int>();
    private float passiveIncomeTimer = 0f;
    private const float PASSIVE_INCOME_INTERVAL = 5f; //kaç saniyede bir gelir eklenir

    //events — skill
    public static event Action<List<PassiveIncomeProduct>> OnProductsUnlocked; //yeni ürünler satın alınabilir
    public static event Action<PassiveIncomeProduct, int> OnProductBought; //ürün, yeni toplam adet
    public static event Action<PassiveIncomeProduct, int> OnProductSold; //ürün, yeni toplam adet
    public static event Action<float> OnPassiveIncomeTick; //bu tick'te kazanılan toplam gelir

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
        if (ownedProducts.Count > 0 && GameStatManager.Instance != null)
        {
            passiveIncomeTimer += Time.deltaTime;
            if (passiveIncomeTimer >= PASSIVE_INCOME_INTERVAL)
            {
                passiveIncomeTimer = 0f;

                float totalIncome = 0f;
                foreach (var kvp in ownedProducts)
                {
                    PassiveIncomeProduct product = kvp.Key;
                    int count = kvp.Value;
                    //ürün tipi başına tek random, adet ile çarpılır
                    float incomePerUnit = UnityEngine.Random.Range(product.minIncomePerTick, product.maxIncomePerTick);
                    totalIncome += incomePerUnit * count;
                }

                totalIncome *= PASSIVE_INCOME_INTERVAL;
                GameStatManager.Instance.AddWealth(totalIncome);
                OnPassiveIncomeTick?.Invoke(totalIncome);
            }
        }
    }

    // ==================== SKİLL SİSTEMİ ====================

    public bool IsUnlocked(string skillId)
    {
        return unlockedSkillIds.Contains(skillId);
    }

    public bool IsBlocked(string skillId)
    {
        return blockedSkillIds.Contains(skillId);
    }

    public bool CanUnlock(Skill skill)
    {
        if (skill == null)
            return false;

        if (IsUnlocked(skill.id))
            return false;

        if (IsBlocked(skill.id))
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

    // ==================== ÜRÜN SİSTEMİ ====================

    /// <summary>
    /// PassiveIncomeEffect tarafından çağrılır. Ürünleri satın alınabilir yapar.
    /// </summary>
    public void UnlockProducts(List<PassiveIncomeProduct> products)
    {
        List<PassiveIncomeProduct> newlyUnlocked = new List<PassiveIncomeProduct>();

        foreach (PassiveIncomeProduct product in products)
        {
            if (product != null && unlockedProducts.Add(product))
            {
                newlyUnlocked.Add(product);
            }
        }

        if (newlyUnlocked.Count > 0)
            OnProductsUnlocked?.Invoke(newlyUnlocked);
    }

    /// <summary>
    /// UI bu metodu çağırır. Ürün satın alır, parayı öder.
    /// </summary>
    public bool BuyProduct(PassiveIncomeProduct product)
    {
        if (product == null) return false;
        if (!unlockedProducts.Contains(product)) return false;
        if (GameStatManager.Instance == null) return false;
        if (!GameStatManager.Instance.HasEnoughWealth(product.cost)) return false;

        GameStatManager.Instance.TrySpendWealth(product.cost);

        if (ownedProducts.ContainsKey(product))
            ownedProducts[product]++;
        else
            ownedProducts[product] = 1;

        OnProductBought?.Invoke(product, ownedProducts[product]);
        return true;
    }

    /// <summary>
    /// UI bu metodu çağırır. Ürün satar, maliyetin altına satar.
    /// </summary>
    public bool SellProduct(PassiveIncomeProduct product)
    {
        if (product == null) return false;
        if (!ownedProducts.ContainsKey(product) || ownedProducts[product] <= 0) return false;

        //satış fiyatı = cost * sellRatio
        float sellPrice = product.cost * product.sellRatio;

        ownedProducts[product]--;

        if (ownedProducts[product] <= 0)
            ownedProducts.Remove(product);

        if (GameStatManager.Instance != null)
            GameStatManager.Instance.AddWealth(sellPrice);

        int remaining = ownedProducts.ContainsKey(product) ? ownedProducts[product] : 0;
        OnProductSold?.Invoke(product, remaining);
        return true;
    }

    // ==================== ÜRÜN GETTER'LARI ====================

    public bool IsProductUnlocked(PassiveIncomeProduct product)
    {
        return unlockedProducts.Contains(product);
    }

    public int GetOwnedCount(PassiveIncomeProduct product)
    {
        return ownedProducts.ContainsKey(product) ? ownedProducts[product] : 0;
    }

    public HashSet<PassiveIncomeProduct> GetUnlockedProducts()
    {
        return unlockedProducts;
    }

    public Dictionary<PassiveIncomeProduct, int> GetOwnedProducts()
    {
        return ownedProducts;
    }

    // ==================== EVENT BINDING ====================

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
