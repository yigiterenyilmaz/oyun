using System;
using System.Collections.Generic;
using UnityEngine;

public class SmuggleManager : MonoBehaviour
{
    public static SmuggleManager Instance { get; private set; }

    [Header("Referanslar")]
    public MiniGameData minigameData; //MinigameManager'dan açık mı kontrolü için
    public SmuggleDatabase database; //rota paketleri ve kurye havuzu

    [Header("Cooldown")]
    public float cooldownDuration = 120f; //operasyonlar arası bekleme süresi (saniye)

    [Header("Event Ayarları")]
    public float eventDecisionTime = 10f; //event'te karar süresi (saniye)

    //mevcut operasyon durumu
    private SmuggleState currentState = SmuggleState.Idle;
    private SmuggleRoutePack currentRoutePack;
    private SmuggleRoute selectedRoute;
    private SmuggleCourier selectedCourier;
    private float cooldownTimer = 0f;
    private bool isCooldownActive = false;

    //mevcut operasyonda sunulan kuryeler
    private List<SmuggleCourier> currentCourierOptions = new List<SmuggleCourier>();

    //events - UI bu event'leri dinleyecek
    public static event Action<SmuggleRoutePack, List<SmuggleCourier>> OnSelectionPhaseStarted; //rota paketi ve kurye seçenekleri hazır
    public static event Action<SmuggleRoute, SmuggleCourier> OnOperationStarted; //operasyon başladı
    public static event Action<SmuggleResult> OnOperationCompleted; //operasyon bitti, sonuç geldi
    public static event Action<string> OnSmuggleFailed; //minigame başlatılamadı (açık değil, cooldown vs.)

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
        //cooldown sayacı
        if (isCooldownActive)
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer <= 0f)
            {
                isCooldownActive = false;
                cooldownTimer = 0f;
            }
        }
    }

    /// <summary>
    /// Minigame'i başlatmayı dener. Açık mı ve cooldown'da mı kontrol eder.
    /// </summary>
    public bool TryStartMinigame()
    {
        //minigame açık mı
        if (MinigameManager.Instance == null || !MinigameManager.Instance.IsMinigameUnlocked(minigameData))
        {
            OnSmuggleFailed?.Invoke("Minigame henüz açılmadı.");
            return false;
        }

        //cooldown kontrolü
        if (isCooldownActive)
        {
            OnSmuggleFailed?.Invoke("Cooldown süresi dolmadı. Kalan: " + Mathf.CeilToInt(cooldownTimer) + "s");
            return false;
        }

        //zaten aktif mi
        if (currentState != SmuggleState.Idle)
        {
            OnSmuggleFailed?.Invoke("Zaten aktif bir operasyon var.");
            return false;
        }

        //seçim aşamasını başlat
        StartSelectionPhase();
        return true;
    }

    /// <summary>
    /// Havuzdan rastgele rota paketi ve 3 kurye seçer, UI'a sunar.
    /// </summary>
    private void StartSelectionPhase()
    {
        currentState = SmuggleState.SelectingRoute;

        //rastgele rota paketi seç
        int packIndex = UnityEngine.Random.Range(0, database.routePacks.Count);
        currentRoutePack = database.routePacks[packIndex];

        //rastgele 3 kurye seç (tekrarsız)
        currentCourierOptions.Clear();
        List<SmuggleCourier> availableCouriers = new List<SmuggleCourier>(database.couriers);
        int courierCount = Mathf.Min(3, availableCouriers.Count);
        for (int i = 0; i < courierCount; i++)
        {
            int idx = UnityEngine.Random.Range(0, availableCouriers.Count);
            currentCourierOptions.Add(availableCouriers[idx]);
            availableCouriers.RemoveAt(idx);
        }

        OnSelectionPhaseStarted?.Invoke(currentRoutePack, currentCourierOptions);
    }

    /// <summary>
    /// Oyuncu rota seçti. UI bu metodu çağırır.
    /// </summary>
    public void SelectRoute(SmuggleRoute route)
    {
        if (currentState != SmuggleState.SelectingRoute) return;

        selectedRoute = route;
        currentState = SmuggleState.SelectingCourier;
    }

    /// <summary>
    /// Oyuncu kurye seçti. Operasyon başlar. UI bu metodu çağırır.
    /// </summary>
    public void SelectCourier(SmuggleCourier courier)
    {
        if (currentState != SmuggleState.SelectingCourier) return;

        selectedCourier = courier;
        StartOperation();
    }

    /// <summary>
    /// Operasyonu başlatır. İleride event zinciri burada tetiklenecek.
    /// </summary>
    private void StartOperation()
    {
        currentState = SmuggleState.InProgress;
        OnOperationStarted?.Invoke(selectedRoute, selectedCourier);

        //TODO: event zinciri burada tetiklenecek
        //şimdilik direkt sonuç hesapla
        CalculateResult();
    }

    /// <summary>
    /// Operasyon sonucunu hesaplar ve uygular.
    /// </summary>
    private void CalculateResult()
    {
        //başarı olasılığı: kurye güvenilirliği ve rota riski temel alınır
        float successChance = selectedCourier.reliability - (selectedRoute.riskLevel * 0.5f);
        successChance = Mathf.Clamp(successChance, 5f, 95f); //her zaman %5-95 arası

        float roll = UnityEngine.Random.Range(0f, 100f);
        bool success = roll <= successChance;

        SmuggleResult result = new SmuggleResult();
        result.success = success;
        result.route = selectedRoute;
        result.courier = selectedCourier;

        if (success)
        {
            result.wealthChange = selectedRoute.baseReward - selectedRoute.cost - selectedCourier.cost;
            result.suspicionChange = selectedRoute.riskLevel * 0.1f; //başarılı olsa bile az şüphe
        }
        else
        {
            result.wealthChange = -(selectedRoute.cost + selectedCourier.cost); //masrafları kaybedersin
            result.suspicionChange = selectedRoute.riskLevel * 0.3f; //başarısızlıkta daha fazla şüphe
        }

        //stat'lara uygula
        if (GameStatManager.Instance != null)
        {
            GameStatManager.Instance.AddWealth(result.wealthChange);
            GameStatManager.Instance.AddSuspicion(result.suspicionChange);
        }

        //cooldown başlat
        isCooldownActive = true;
        cooldownTimer = cooldownDuration;

        //durumu sıfırla
        currentState = SmuggleState.Idle;

        OnOperationCompleted?.Invoke(result);
    }

    /// <summary>
    /// Cooldown'dan kalan süreyi döner.
    /// </summary>
    public float GetRemainingCooldown()
    {
        return isCooldownActive ? cooldownTimer : 0f;
    }

    /// <summary>
    /// Minigame'in şu an oynanabilir olup olmadığını döner.
    /// </summary>
    public bool CanPlay()
    {
        if (MinigameManager.Instance == null || !MinigameManager.Instance.IsMinigameUnlocked(minigameData))
            return false;
        if (isCooldownActive)
            return false;
        if (currentState != SmuggleState.Idle)
            return false;
        return true;
    }

    public SmuggleState GetCurrentState()
    {
        return currentState;
    }
}

/// <summary>
/// Smuggle minigame durumları
/// </summary>
public enum SmuggleState
{
    Idle,            //beklemede
    SelectingRoute,  //rota seçimi
    SelectingCourier,//kurye seçimi
    InProgress,      //operasyon devam ediyor
    EventPhase       //event geldi, karar bekleniyor
}

/// <summary>
/// Operasyon sonucu
/// </summary>
[System.Serializable]
public class SmuggleResult
{
    public bool success;
    public SmuggleRoute route;
    public SmuggleCourier courier;
    public float wealthChange;
    public float suspicionChange;
}
