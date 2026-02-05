using System;
using UnityEngine;

public class GameStatManager : MonoBehaviour
{
    public static GameStatManager Instance { get; private set; }

    [Header("Starting Values")]
    public float startingWealth = 1000f;
    public float startingSuspicion = 0f;
    public float startingReputation = 50f;
    public float startingPoliticalInfluence = 0f;

    [Header("Suspicion Settings")]
    public float minSuspicion = 0f;
    public float maxSuspicion = 100f;

    [Header("Reputation Settings")]
    public float minReputation = 0f;
    public float maxReputation = 100f;

    [Header("Political Influence Settings")]
    public float minPoliticalInfluence = -100f;
    public float maxPoliticalInfluence = 100f;

    [Header("Suspicion Modifier Settings (itibar etkisi)")]
    public float baseSuspicionMultiplier = 1.5f; //itibar 0'da çarpan
    public float minSuspicionMultiplier = 0.5f;  //itibar 100'de çarpan

    [Header("Skill Efficiency Settings (siyasi nüfuz etkisi)")]
    public float minSkillEfficiency = 0.5f;  //nüfuz -100'de
    public float maxSkillEfficiency = 1.5f;  //nüfuz +100'de

    //runtime values
    private float wealth;
    private float suspicion;
    private float reputation;
    private float politicalInfluence;

    //events
    public static event Action<StatType, float, float> OnStatChanged; //stat, oldValue, newValue
    public static event Action OnGameOver; //şüphe 100'e ulaştığında

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        wealth = startingWealth;
        suspicion = startingSuspicion;
        reputation = startingReputation;
        politicalInfluence = startingPoliticalInfluence;
    }

    #region Getters

    public float GetStat(StatType statType)
    {
        return statType switch
        {
            StatType.Wealth => wealth,
            StatType.Suspicion => suspicion,
            StatType.Reputation => reputation,
            StatType.PoliticalInfluence => politicalInfluence,
            _ => 0f
        };
    }

    public float Wealth => wealth;
    public float Suspicion => suspicion;
    public float Reputation => reputation;
    public float PoliticalInfluence => politicalInfluence;

    #endregion

    #region Modifiers (hesaplayıcılar)

    //itibar bazlı şüphe çarpanı
    //yüksek itibar = düşük çarpan (şüphe daha az artar)
    //düşük itibar = yüksek çarpan (şüphe daha çok artar)
    public float GetSuspicionMultiplier()
    {
        //itibar 0 → baseSuspicionMultiplier (1.5)
        //itibar 100 → minSuspicionMultiplier (0.5)
        float t = reputation / maxReputation;
        return Mathf.Lerp(baseSuspicionMultiplier, minSuspicionMultiplier, t);
    }

    //siyasi nüfuz bazlı skill verim çarpanı
    //yüksek nüfuz = yüksek verim
    //düşük nüfuz = düşük verim
    public float GetSkillEfficiencyMultiplier()
    {
        //nüfuz -100 → minSkillEfficiency (0.5)
        //nüfuz 0 → 1.0
        //nüfuz +100 → maxSkillEfficiency (1.5)
        float t = (politicalInfluence - minPoliticalInfluence) / (maxPoliticalInfluence - minPoliticalInfluence);
        return Mathf.Lerp(minSkillEfficiency, maxSkillEfficiency, t);
    }

    #endregion

    #region Stat Modification

    public void ModifyStat(StatType statType, float amount)
    {
        switch (statType)
        {
            case StatType.Wealth:
                AddWealth(amount);
                break;
            case StatType.Suspicion:
                AddSuspicion(amount);
                break;
            case StatType.Reputation:
                AddReputation(amount);
                break;
            case StatType.PoliticalInfluence:
                AddPoliticalInfluence(amount);
                break;
        }
    }

    public void AddWealth(float amount)
    {
        float oldValue = wealth;
        wealth += amount;

        if (oldValue != wealth)
        {
            OnStatChanged?.Invoke(StatType.Wealth, oldValue, wealth);
        }
    }

    //şüphe ekleme - itibar çarpanı uygulanır (sadece artışlarda)
    public void AddSuspicion(float amount)
    {
        float oldValue = suspicion;

        //sadece pozitif değerlerde (şüphe artışında) itibar çarpanı uygula
        if (amount > 0)
        {
            amount *= GetSuspicionMultiplier();
        }

        suspicion = Mathf.Clamp(suspicion + amount, minSuspicion, maxSuspicion);

        if (oldValue != suspicion)
        {
            OnStatChanged?.Invoke(StatType.Suspicion, oldValue, suspicion);
        }

        //game over kontrolü
        if (suspicion >= maxSuspicion)
        {
            OnGameOver?.Invoke();
        }
    }

    //şüphe ekleme - çarpan UYGULANMADAN (özel durumlar için)
    public void AddSuspicionRaw(float amount)
    {
        float oldValue = suspicion;
        suspicion = Mathf.Clamp(suspicion + amount, minSuspicion, maxSuspicion);

        if (oldValue != suspicion)
        {
            OnStatChanged?.Invoke(StatType.Suspicion, oldValue, suspicion);
        }

        if (suspicion >= maxSuspicion)
        {
            OnGameOver?.Invoke();
        }
    }

    public void AddReputation(float amount)
    {
        float oldValue = reputation;
        reputation = Mathf.Clamp(reputation + amount, minReputation, maxReputation);

        if (oldValue != reputation)
        {
            OnStatChanged?.Invoke(StatType.Reputation, oldValue, reputation);
        }
    }

    public void AddPoliticalInfluence(float amount)
    {
        float oldValue = politicalInfluence;
        politicalInfluence = Mathf.Clamp(politicalInfluence + amount, minPoliticalInfluence, maxPoliticalInfluence);

        if (oldValue != politicalInfluence)
        {
            OnStatChanged?.Invoke(StatType.PoliticalInfluence, oldValue, politicalInfluence);
        }
    }

    #endregion

    #region Set Methods

    public void SetStat(StatType statType, float value)
    {
        float oldValue = GetStat(statType);

        switch (statType)
        {
            case StatType.Wealth:
                wealth = value;
                break;
            case StatType.Suspicion:
                suspicion = Mathf.Clamp(value, minSuspicion, maxSuspicion);
                break;
            case StatType.Reputation:
                reputation = Mathf.Clamp(value, minReputation, maxReputation);
                break;
            case StatType.PoliticalInfluence:
                politicalInfluence = Mathf.Clamp(value, minPoliticalInfluence, maxPoliticalInfluence);
                break;
            default:
                return;
        }

        float newValue = GetStat(statType);
        if (oldValue != newValue)
        {
            OnStatChanged?.Invoke(statType, oldValue, newValue);
        }

        //set ile de game over kontrolü
        if (statType == StatType.Suspicion && suspicion >= maxSuspicion)
        {
            OnGameOver?.Invoke();
        }
    }

    #endregion

    #region Utility Methods

    public bool HasEnoughWealth(float amount)
    {
        return wealth >= amount;
    }

    public bool TrySpendWealth(float amount)
    {
        if (!HasEnoughWealth(amount))
            return false;

        AddWealth(-amount);
        return true;
    }

    //stat yüzdesini döner (UI için)
    public float GetStatPercent(StatType statType)
    {
        return statType switch
        {
            StatType.Suspicion => suspicion / maxSuspicion,
            StatType.Reputation => reputation / maxReputation,
            StatType.PoliticalInfluence => (politicalInfluence - minPoliticalInfluence) / (maxPoliticalInfluence - minPoliticalInfluence),
            _ => 0f
        };
    }

    #endregion
}
