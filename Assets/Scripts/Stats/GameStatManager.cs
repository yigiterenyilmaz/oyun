using System;
using UnityEngine;

public class GameStatManager : MonoBehaviour
{
    public static GameStatManager Instance { get; private set; }

    public float startingWealth = 1000f;
    public float startingSuspicion = 0f;
    public float startingTrust = 50f;

    public float minSuspicion = 0f;
    public float maxSuspicion = 100f;
    public float minTrust = 0f;
    public float maxTrust = 100f;

    public float wealth;
    public float suspicion;
    public float trust;

    public static event Action<StatType, float, float> OnStatChanged;
    //statlar değiştiğinde Action gönderir. UI bunu dinleyip barları güncelleyecek.

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
        trust = startingTrust;
    }

    public float GetStat(StatType statType)
    {
        return statType switch
        {
            StatType.Wealth => wealth,
            StatType.Suspicion => suspicion,
            StatType.Trust => trust,
            _ => 0f
        }; //verilen stat type a göre statın değerini döner
    }

    public void ModifyStat(StatType statType, float amount) //stata değer eklemek için
    {
        float oldValue = GetStat(statType);
        float newValue;

        switch (statType)
        {
            case StatType.Wealth:
                wealth += amount;
                newValue = wealth;
                break;
            case StatType.Suspicion:
                suspicion = Mathf.Clamp(suspicion + amount, minSuspicion, maxSuspicion);
                newValue = suspicion;
                break;
            case StatType.Trust:
                trust = Mathf.Clamp(trust + amount, minTrust, maxTrust);
                newValue = trust;
                break;
            default:
                return;
        }

        if (oldValue != newValue)
        {
            OnStatChanged?.Invoke(statType, oldValue, newValue);
        } /* eğer stat değiştiyse bir Event patlatır(bu event'oyundaki eventten farklı bir şey)
            UI burayı dinleyecek ve barlar ona göre tepki verecek.
        */
    }

    public void SetStat(StatType statType, float value) // statı set etmek için.
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
            case StatType.Trust:
                trust = Mathf.Clamp(value, minTrust, maxTrust);
                break;
            default:
                return;
        }

        float newValue = GetStat(statType);
        if (oldValue != newValue)
        {
            OnStatChanged?.Invoke(statType, oldValue, newValue); //modifyStat metoduyla aynı mantık
        }
    }

    public bool HasEnoughWealth(float amount)
    {
        return wealth >= amount; 
    }

    public bool TrySpendWealth(float amount)
    {
        if (!HasEnoughWealth(amount))
            return false;

        ModifyStat(StatType.Wealth, -amount);
        return true;
    }
}
