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

    //training — bilim adamı eğitim sistemi
    private bool trainingUnlocked = false;
    private List<ScientistTraining> scientists = new List<ScientistTraining>();
    private float trainingCoefficient = 0f;
    private float trainingBaseSweetSpot = 0f;
    private float trainingSweetSpotGrowthRate = 0f;

    //events — passive income
    public static event Action<List<PassiveIncomeProduct>> OnProductsUnlocked; //yeni ürünler satın alınabilir
    public static event Action<PassiveIncomeProduct, int> OnProductBought; //ürün, yeni toplam adet
    public static event Action<PassiveIncomeProduct, int> OnProductSold; //ürün, yeni toplam adet
    public static event Action<float> OnPassiveIncomeTick; //bu tick'te kazanılan toplam gelir

    //events — training
    public static event Action OnTrainingUnlocked; //eğitim sistemi açıldı
    public static event Action<int> OnScientistStarted; //yeni bilim adamı index'i
    public static event Action<int, float> OnScientistLevelChanged; //bilim adamı index'i, yeni seviye
    public static event Action<int> OnScientistCompleted; //eğitimi tamamlanan bilim adamı index'i

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

        //diğer ön koşullar kontrolü (flags enum)
        if (skill.otherPrerequisites != OtherPrerequisite.None)
        {
            if (!CheckOtherPrerequisites(skill.otherPrerequisites))
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
    /// UnlockTrainingEffect tarafından çağrılır. Bilim adamı eğitim sistemini açar.
    /// </summary>
    public void UnlockTraining(float coefficient, float baseSweetSpot, float sweetSpotGrowthRate)
    {
        if (trainingUnlocked) return;

        trainingCoefficient = coefficient;
        trainingBaseSweetSpot = baseSweetSpot;
        trainingSweetSpotGrowthRate = sweetSpotGrowthRate;
        trainingUnlocked = true;

        OnTrainingUnlocked?.Invoke();
    }

    /// <summary>
    /// UI bu metodu çağırır. Yeni bir bilim adamı eğitimine başlar.
    /// Dönen değer bilim adamının index'idir.
    /// </summary>
    public int StartScientistTraining(ScientistData data)
    {
        if (!trainingUnlocked) return -1;
        if (data == null) return -1;

        ScientistTraining scientist = new ScientistTraining(data, Time.time);
        scientists.Add(scientist);

        int index = scientists.Count - 1;
        OnScientistStarted?.Invoke(index);
        return index;
    }

    /// <summary>
    /// UI bu metodu çağırır. Belirtilen bilim adamına wealth yatırır.
    /// Verimlilik çan eğrisiyle hesaplanır: ideal noktaya yakın yatırımlar en verimli.
    /// İdeal nokta, o bilim adamının eğitim başlangıcından itibaren zamanla büyür.
    /// </summary>
    public bool InvestInScientist(int scientistIndex, float amount)
    {
        if (!trainingUnlocked) return false;
        if (scientistIndex < 0 || scientistIndex >= scientists.Count) return false;
        if (amount <= 0f) return false;
        if (GameStatManager.Instance == null) return false;
        if (!GameStatManager.Instance.HasEnoughWealth(amount)) return false;

        ScientistTraining scientist = scientists[scientistIndex];
        if (scientist.isCompleted) return false;

        //bu bilim adamının kendi zamanlayıcısından ideal noktayı hesapla
        float elapsed = Time.time - scientist.startTime;
        float sweetSpot = trainingBaseSweetSpot + trainingSweetSpotGrowthRate * elapsed;

        //çan eğrisi: x * e^(1-x) — x=1'de zirve, altında ve üstünde düşer
        float newTotal = scientist.totalInvested + amount;
        float x = newTotal / sweetSpot;
        float efficiency = x * Mathf.Exp(1f - x);

        //puan hesapla ve uygula
        float points = amount * trainingCoefficient * efficiency;

        GameStatManager.Instance.TrySpendWealth(amount);
        scientist.totalInvested = newTotal;
        scientist.level += points;

        //tamamlanma kontrolü
        if (!scientist.isCompleted && scientist.level >= scientist.data.completionLevel)
        {
            scientist.isCompleted = true;
            OnScientistCompleted?.Invoke(scientistIndex);
        }

        OnScientistLevelChanged?.Invoke(scientistIndex, scientist.level);
        return true;
    }

    /// <summary>
    /// Belirli bir bilim adamına belirli miktar yatırılsa kaç puan kazanılacağını hesaplar (UI önizleme).
    /// </summary>
    public float PreviewScientistInvestment(int scientistIndex, float amount)
    {
        if (!trainingUnlocked || amount <= 0f) return 0f;
        if (scientistIndex < 0 || scientistIndex >= scientists.Count) return 0f;

        ScientistTraining scientist = scientists[scientistIndex];
        if (scientist.isCompleted) return 0f;

        float elapsed = Time.time - scientist.startTime;
        float sweetSpot = trainingBaseSweetSpot + trainingSweetSpotGrowthRate * elapsed;
        float x = (scientist.totalInvested + amount) / sweetSpot;
        float efficiency = x * Mathf.Exp(1f - x);

        return amount * trainingCoefficient * efficiency;
    }

    /// <summary>
    /// OtherPrerequisite flags kontrolü. Her flag için ilgili koşulu doğrular.
    /// </summary>
    private bool CheckOtherPrerequisites(OtherPrerequisite flags)
    {
        if ((flags & OtherPrerequisite.ThreeScientistsTrained) != 0)
        {
            if (GetCompletedScientistCount() < 3)
                return false;
        }

        return true;
    }

    // ==================== EĞİTİM GETTER'LARI ====================

    public bool IsTrainingUnlocked()
    {
        return trainingUnlocked;
    }

    public int GetScientistCount()
    {
        return scientists.Count;
    }

    public int GetCompletedScientistCount()
    {
        int count = 0;
        foreach (ScientistTraining s in scientists)
        {
            if (s.isCompleted) count++;
        }
        return count;
    }

    public ScientistTraining GetScientist(int index)
    {
        if (index < 0 || index >= scientists.Count) return null;
        return scientists[index];
    }

    public List<ScientistTraining> GetAllScientists()
    {
        return new List<ScientistTraining>(scientists);
    }

    public float GetScientistSweetSpot(int scientistIndex)
    {
        if (scientistIndex < 0 || scientistIndex >= scientists.Count) return 0f;
        float elapsed = Time.time - scientists[scientistIndex].startTime;
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
