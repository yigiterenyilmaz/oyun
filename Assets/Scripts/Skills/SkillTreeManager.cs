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

    //passive income — direkt gelir (skill açılınca akan, zamanla azalan gelir)
    [Header("Direkt Gelir Azalma Ayarları (% kalan)")]
    [Range(0, 100)] public float decayAt1Min = 95f;  //1 dakika sonra başlangıcın %kaçı kalmış
    [Range(0, 100)] public float decayAt2Min = 85f;  //2 dakika sonra
    [Range(0, 100)] public float decayAt3Min = 60f;  //3 dakika sonra
    [Range(0, 100)] public float decayAt4Min = 25f;  //4 dakika sonra
    [Range(0, 100)] public float decayAt5Min = 5f;   //5 dakika sonra (sonrası oyun hesaplar)
    private List<DirectIncomeSource> directIncomeSources = new List<DirectIncomeSource>();

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
        //pasif geliri 5 saniyede bir uygula (ürün geliri + direkt gelir)
        bool hasIncome = ownedProducts.Count > 0 || directIncomeSources.Count > 0;
        if (hasIncome && GameStatManager.Instance != null)
        {
            passiveIncomeTimer += Time.deltaTime;
            if (passiveIncomeTimer >= PASSIVE_INCOME_INTERVAL)
            {
                passiveIncomeTimer = 0f;

                float totalIncome = 0f;

                //ürün tabanlı gelir
                foreach (var kvp in ownedProducts)
                {
                    PassiveIncomeProduct product = kvp.Key;
                    int count = kvp.Value;
                    float incomePerUnit = UnityEngine.Random.Range(product.minIncomePerTick, product.maxIncomePerTick);
                    totalIncome += incomePerUnit * count;
                }

                totalIncome *= PASSIVE_INCOME_INTERVAL;

                //direkt gelir (her kaynak kendi decay eğrisiyle)
                float directTotal = CalculateDirectIncome();
                totalIncome += directTotal * PASSIVE_INCOME_INTERVAL;

                GameStatManager.Instance.AddWealth(totalIncome);
                OnPassiveIncomeTick?.Invoke(totalIncome);
            }
        }
    }

    /// <summary>
    /// Tüm direkt gelir kaynaklarının decay uygulanmış anlık saniye başına toplamını hesaplar.
    /// Süresi dolan kaynakları temizler.
    /// </summary>
    private float CalculateDirectIncome()
    {
        float total = 0f;

        for (int i = directIncomeSources.Count - 1; i >= 0; i--)
        {
            DirectIncomeSource source = directIncomeSources[i];
            float elapsed = Time.time - source.startTime;
            float multiplier = GetDecayMultiplier(elapsed);

            //süresi doldu — listeden çıkar
            if (multiplier <= 0f)
            {
                directIncomeSources.RemoveAt(i);
                continue;
            }

            total += source.incomePerSecond * multiplier;
        }

        return total;
    }

    /// <summary>
    /// Inspector'daki yüzde değerlerinden decay çarpanını hesaplar.
    /// 0-5dk arası: keypoint'ler arası linear interpolation.
    /// 5dk sonrası: son segmentin eğimiyle sıfıra kadar devam eder.
    /// </summary>
    private float GetDecayMultiplier(float elapsedSeconds)
    {
        float minutes = elapsedSeconds / 60f;

        if (minutes <= 0f) return 1f;

        //keypoint'ler: dakika 0 = %100, dakika 1-5 = Inspector değerleri
        float k0 = 100f;
        float k1 = decayAt1Min;
        float k2 = decayAt2Min;
        float k3 = decayAt3Min;
        float k4 = decayAt4Min;
        float k5 = decayAt5Min;

        float value;

        if (minutes < 1f)
            value = Mathf.Lerp(k0, k1, minutes);
        else if (minutes < 2f)
            value = Mathf.Lerp(k1, k2, minutes - 1f);
        else if (minutes < 3f)
            value = Mathf.Lerp(k2, k3, minutes - 2f);
        else if (minutes < 4f)
            value = Mathf.Lerp(k3, k4, minutes - 3f);
        else if (minutes < 5f)
            value = Mathf.Lerp(k4, k5, minutes - 4f);
        else
        {
            //5dk sonrası: 4-5dk arasındaki eğimle devam et
            float slopePerMinute = k5 - k4;
            value = k5 + slopePerMinute * (minutes - 5f);
        }

        return Mathf.Max(0f, value / 100f);
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

    // ==================== DİREKT GELİR SİSTEMİ ====================

    /// <summary>
    /// DirectPassiveIncomeEffect tarafından çağrılır. Yeni bir gelir kaynağı ekler.
    /// Gelir zamanla azalır (decay eğrisi).
    /// </summary>
    public void AddDirectPassiveIncome(float incomePerSecond)
    {
        directIncomeSources.Add(new DirectIncomeSource(incomePerSecond, Time.time));
    }

    /// <summary>
    /// Şu anki toplam direkt geliri döner (decay uygulanmış, saniye başına).
    /// </summary>
    public float GetDirectPassiveIncome()
    {
        float total = 0f;
        for (int i = 0; i < directIncomeSources.Count; i++)
        {
            float elapsed = Time.time - directIncomeSources[i].startTime;
            float multiplier = GetDecayMultiplier(elapsed);
            if (multiplier <= 0f) continue;
            total += directIncomeSources[i].incomePerSecond * multiplier;
        }
        return total;
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

        //bu bilim adamının kendi zamanlayıcısından ideal noktayı hesapla (zeka hızı etkiler)
        float elapsed = Time.time - scientist.startTime;
        float sweetSpot = trainingBaseSweetSpot + trainingSweetSpotGrowthRate * scientist.data.intelligenceLevel * elapsed;

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
        if (!scientist.isCompleted && scientist.level >= scientist.data.completeCost)
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
        float sweetSpot = trainingBaseSweetSpot + trainingSweetSpotGrowthRate * scientist.data.intelligenceLevel * elapsed;
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
        ScientistTraining scientist = scientists[scientistIndex];
        float elapsed = Time.time - scientist.startTime;
        return trainingBaseSweetSpot + trainingSweetSpotGrowthRate * scientist.data.intelligenceLevel * elapsed;
    }

    /// <summary>
    /// Bilim adamını listeden kalıcı olarak çıkarır (kaçak operasyona gönderildi).
    /// </summary>
    public bool RemoveScientist(int index)
    {
        if (index < 0 || index >= scientists.Count) return false;

        scientists.RemoveAt(index);
        return true;
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

/// <summary>
/// Skill'den gelen direkt gelir kaynağı. Zamanla decay uygulanır.
/// </summary>
public class DirectIncomeSource
{
    public float incomePerSecond;
    public float startTime;

    public DirectIncomeSource(float incomePerSecond, float startTime)
    {
        this.incomePerSecond = incomePerSecond;
        this.startTime = startTime;
    }
}
