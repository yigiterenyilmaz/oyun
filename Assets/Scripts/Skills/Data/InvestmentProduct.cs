using UnityEngine;

[CreateAssetMenu(menuName = "SkillTree/Products/InvestmentProduct")]
public class InvestmentProduct : ScriptableObject
{
    public string id;
    public string displayName;
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

    [Header("Piyasa Erişilebilirliği")]
    public bool hasLimitedAvailability = false; //piyasada her zaman bulunmaz
    [Range(0f, 1f)] public float availabilityChance = 0.3f; //piyasada bulunma oranı (zamanın yüzdesi)
    public float availabilityCycleDuration = 120f; //faz döngü süresi (sn)
    public float minAvailableDuration = 15f; //minimum alınabilir kalma süresi (sn)
}
