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

    //training — eğitim sistemi
    private bool trainingUnlocked = false;
    private float trainingLevel = 0f;
    private float totalInvested = 0f;
    private float trainingCoefficient = 0f;
    private float trainingBaseSweetSpot = 0f;
    private float trainingSweetSpotGrowthRate = 0f;
    private float trainingUnlockTime = 0f; //eğitimin açıldığı an (Time.time)

    //events — passive income
    public static event Action<List<PassiveIncomeProduct>> OnProductsUnlocked; //yeni ürünler satın alınabilir
    public static event Action<PassiveIncomeProduct, int> OnProductBought; //ürün, yeni toplam adet
    public static event Action<PassiveIncomeProduct, int> OnProductSold; //ürün, yeni toplam adet
    public static event Action<float> OnPassiveIncomeTick; //bu tick'te kazanılan toplam gelir

    //events — training
    public static event Action OnTrainingUnlocked; //eğitim alanı açıldı
    public static event Action<float> OnTrainingLevelChanged; //yeni seviye

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

        //eğitim seviyesi gereksinimi kontrolü
        if (skill.requiredTrainingLevel > 0f)
        {
            if (trainingLevel < skill.requiredTrainingLevel)
                return false;
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

    // ==================== EĞİTİM SİSTEMİ ====================

    /// <summary>
    /// UnlockTrainingEffect tarafından çağrılır. Eğitim alanını açar ve eğri parametrelerini ayarlar.
    /// </summary>
    public void UnlockTraining(float coefficient, float baseSweetSpot, float sweetSpotGrowthRate)
    {
        if (trainingUnlocked) return;

        trainingCoefficient = coefficient;
        trainingBaseSweetSpot = baseSweetSpot;
        trainingSweetSpotGrowthRate = sweetSpotGrowthRate;
        trainingLevel = 0f;
        totalInvested = 0f;
        trainingUnlockTime = Time.time;
        trainingUnlocked = true;

        OnTrainingUnlocked?.Invoke();
    }

    /// <summary>
    /// UI bu metodu çağırır. Belirtilen miktar kadar wealth yatırır.
    /// Verimlilik çan eğrisiyle hesaplanır: ideal noktaya yakın yatırımlar en verimli.
    /// İdeal nokta zamanla büyür — oyuncuyu zaman içinde yatırım yapmaya teşvik eder.
    /// </summary>
    public bool InvestInTraining(float amount)
    {
        if (!trainingUnlocked) return false;
        if (amount <= 0f) return false;
        if (GameStatManager.Instance == null) return false;
        if (!GameStatManager.Instance.HasEnoughWealth(amount)) return false;

        //ideal noktayı hesapla (zamanla büyür)
        float elapsed = Time.time - trainingUnlockTime;
        float sweetSpot = trainingBaseSweetSpot + trainingSweetSpotGrowthRate * elapsed;

        //çan eğrisi: x * e^(1-x) — x=1'de zirve, altında ve üstünde düşer
        float newTotal = totalInvested + amount;
        float x = newTotal / sweetSpot;
        float efficiency = x * Mathf.Exp(1f - x);

        //puan hesapla ve uygula
        float points = amount * trainingCoefficient * efficiency;

        GameStatManager.Instance.TrySpendWealth(amount);
        totalInvested = newTotal;
        trainingLevel += points;

        OnTrainingLevelChanged?.Invoke(trainingLevel);
        return true;
    }

    /// <summary>
    /// Belirli bir miktar yatırılsa ne kadar puan kazanılacağını hesaplar (UI önizleme için).
    /// </summary>
    public float PreviewTrainingInvestment(float amount)
    {
        if (!trainingUnlocked || amount <= 0f) return 0f;

        float elapsed = Time.time - trainingUnlockTime;
        float sweetSpot = trainingBaseSweetSpot + trainingSweetSpotGrowthRate * elapsed;
        float x = (totalInvested + amount) / sweetSpot;
        float efficiency = x * Mathf.Exp(1f - x);

        return amount * trainingCoefficient * efficiency;
    }

    // ==================== EĞİTİM GETTER'LARI ====================

    public bool IsTrainingUnlocked()
    {
        return trainingUnlocked;
    }

    public float GetTrainingLevel()
    {
        return trainingLevel;
    }

    public float GetTotalInvested()
    {
        return totalInvested;
    }

    public float GetCurrentSweetSpot()
    {
        if (!trainingUnlocked) return 0f;
        float elapsed = Time.time - trainingUnlockTime;
        return trainingBaseSweetSpot + trainingSweetSpotGrowthRate * elapsed;
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
