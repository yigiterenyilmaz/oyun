using UnityEngine;

[CreateAssetMenu(menuName = "SkillTree/InvestmentProduct")]
public class InvestmentProduct : ScriptableObject
{
    public string id;
    public string displayName;
    [TextArea(2, 4)] public string description;
    public Sprite icon;

    public int cost; //satın alma fiyatı
    [Range(0f, 1f)] public float profitChance = 0.8f; //kâr olasılığı (0=hep zarar, 1=hep kâr)
    public float maxProfitPercent = 40f; //maksimum kâr potansiyeli (%)
    public float maxLossPercent = 25f; //maksimum zarar potansiyeli (%)
    public float volatility = 3f; //tick başına fiyat salınım büyüklüğü (%)
    public float postPotentialDrift = -5f; //potansiyelden sonra sapma yönü (%, + yukarı, - aşağı)
    public float postPotentialTimeout = 60f; //potansiyelde satılmazsa drift başlama süresi (sn)

    [Header("Piyasa Salınımı (satın alınmadan önceki idle hareket)")]
    public float idleOscillationMin = -5f; //piyasa salınım alt sınır (%)
    public float idleOscillationMax = 5f;  //piyasa salınım üst sınır (%)
}
