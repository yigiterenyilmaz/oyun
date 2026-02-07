using System;
using System.Collections.Generic;
using UnityEngine;

public class SmuggleManager : MonoBehaviour
{
    public static SmuggleManager Instance { get; private set; }

    [Header("Referanslar")]
    public MiniGameData minigameData; //MinigameManager'dan açık mı kontrolü için
    public SmuggleDatabase database; //rota paketleri, kurye havuzu ve eventler

    [Header("Operasyon Ayarları")]
    public float eventCheckInterval = 5f; //kaç saniyede bir event tetiklenme kontrolü
    public float eventDecisionTime = 10f; //event'te karar süresi (saniye)

    //mevcut operasyon durumu
    private SmuggleState currentState = SmuggleState.Idle;
    private SmuggleRoutePack currentRoutePack;
    private SmuggleRoute selectedRoute;
    private SmuggleCourier selectedCourier;

    //mevcut operasyonda sunulan kuryeler
    private List<SmuggleCourier> currentCourierOptions = new List<SmuggleCourier>();

    //operasyon zamanlayıcı
    private float operationDuration; //toplam operasyon süresi (saniye)
    private float operationTimer; //geçen süre
    private float eventCheckTimer; //event kontrol sayacı

    //event sistemi
    private SmuggleEvent currentEvent; //şu an aktif event
    private float eventDecisionTimer; //event karar sayacı
    private List<SmuggleEvent> triggeredEvents = new List<SmuggleEvent>(); //bu operasyonda tetiklenen eventler (tekrar tetiklenmemesi için)
    private List<SmuggleEvent> activeEventPool; //şu an aktif event havuzu (seçimlere göre değişir)

    //operasyon boyunca biriken modifier'lar
    private float accumulatedSuccessModifier;
    private float accumulatedSuspicionModifier;
    private int accumulatedCostModifier;

    //events - UI bu event'leri dinleyecek
    public static event Action<SmuggleRoutePack, List<SmuggleCourier>> OnSelectionPhaseStarted; //rota paketi ve kurye seçenekleri hazır
    public static event Action<SmuggleRoute, SmuggleCourier, float> OnOperationStarted; //operasyon başladı (rota, kurye, toplam süre)
    public static event Action<float> OnOperationProgress; //operasyon ilerleme (0-1 arası)
    public static event Action<SmuggleEvent> OnSmuggleEventTriggered; //operasyon sırasında event tetiklendi
    public static event Action<float> OnEventDecisionTimerUpdate; //event karar sayacı güncellendi
    public static event Action<SmuggleEventChoice> OnSmuggleEventResolved; //oyuncu event'te seçim yaptı
    public static event Action<SmuggleResult> OnOperationCompleted; //operasyon bitti, sonuç geldi
    public static event Action<string> OnSmuggleFailed; //minigame başlatılamadı (açık değil, cooldown vs.)
    public static event Action<float> OnOperationCancelled; //operasyon iptal edildi (iade edilen miktar)

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
        if (currentState == SmuggleState.InProgress)
        {
            //operasyon zamanlayıcısını ilerlet
            operationTimer += Time.deltaTime;

            //UI'a ilerleme bildir
            float progress = Mathf.Clamp01(operationTimer / operationDuration);
            OnOperationProgress?.Invoke(progress);

            //event tetiklenme kontrolü
            eventCheckTimer += Time.deltaTime;
            if (eventCheckTimer >= eventCheckInterval)
            {
                eventCheckTimer = 0f;
                TryTriggerEvent();

                //event tetiklendiyse bu frame'de sonuç hesaplama
                if (currentState != SmuggleState.InProgress) return;
            }

            //operasyon bitti mi
            if (operationTimer >= operationDuration)
            {
                CalculateResult();
            }
        }
        else if (currentState == SmuggleState.EventPhase)
        {
            //event karar sayacını geri say
            eventDecisionTimer -= Time.deltaTime;
            OnEventDecisionTimerUpdate?.Invoke(eventDecisionTimer);

            //süre doldu — ilk seçeneği otomatik seç
            if (eventDecisionTimer <= 0f)
            {
                ResolveEvent(0);
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
            OnSmuggleFailed?.Invoke("Minigame is not unlocked yet.");
            return false;
        }

        //cooldown kontrolü
        if (MinigameManager.Instance.IsOnCooldown(minigameData))
        {
            float remaining = MinigameManager.Instance.GetRemainingCooldown(minigameData);
            OnSmuggleFailed?.Invoke("Cooldown active. Remaining: " + Mathf.CeilToInt(remaining) + "s");
            return false;
        }

        //zaten aktif mi
        if (currentState != SmuggleState.Idle)
        {
            OnSmuggleFailed?.Invoke("An operation is already in progress.");
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

        //bütçe kontrolü: rota + kurye maliyetini karşılayacak para var mı
        int totalCost = selectedRoute.cost + courier.cost;
        if (GameStatManager.Instance == null || !GameStatManager.Instance.HasEnoughWealth(totalCost))
        {
            OnSmuggleFailed?.Invoke("Not enough funds. Required: " + totalCost);
            return;
        }

        //maliyeti peşin düş
        GameStatManager.Instance.AddWealth(-totalCost);

        selectedCourier = courier;
        StartOperation();
    }

    /// <summary>
    /// Operasyonu başlatır. Süreyi hesaplar, zamanlayıcıları sıfırlar.
    /// </summary>
    private void StartOperation()
    {
        currentState = SmuggleState.InProgress;

        //operasyon süresini hesapla: mesafe / (kurye hızı çarpanı)
        //speed 0 → distance saniye, speed 50 → distance/6, speed 100 → distance/11
        float speedFactor = selectedCourier.speed * 0.1f + 1f;
        operationDuration = selectedRoute.distance / speedFactor;

        //zamanlayıcıları sıfırla
        operationTimer = 0f;
        eventCheckTimer = 0f;

        //event modifier'larını sıfırla
        accumulatedSuccessModifier = 0f;
        accumulatedSuspicionModifier = 0f;
        accumulatedCostModifier = 0;
        triggeredEvents.Clear();
        currentEvent = null;
        activeEventPool = database.events; //başlangıç havuzu

        OnOperationStarted?.Invoke(selectedRoute, selectedCourier, operationDuration);
    }

    /// <summary>
    /// Aktif havuzdan event tetiklemeyi dener. Her event kendi triggerType'ına göre değerlendirilir.
    /// </summary>
    private void TryTriggerEvent()
    {
        //aktif havuz boşsa veya null ise event tetiklenmez (zincir bitmiş)
        if (activeEventPool == null || activeEventPool.Count == 0) return;

        //aktif havuzdan daha önce tetiklenmemiş eventleri filtrele
        List<SmuggleEvent> available = new List<SmuggleEvent>();
        for (int i = 0; i < activeEventPool.Count; i++)
        {
            if (!triggeredEvents.Contains(activeEventPool[i]))
                available.Add(activeEventPool[i]);
        }

        if (available.Count == 0) return;

        //rastgele bir aday event seç
        int idx = UnityEngine.Random.Range(0, available.Count);
        SmuggleEvent candidate = available[idx];

        //adayın triggerType'ına göre tetiklenme şansını hesapla
        float triggerChance = GetTriggerChance(candidate.triggerType);

        //roll at — şans tutmazsa bu aralıkta event tetiklenmez
        float roll = UnityEngine.Random.Range(0f, 100f);
        if (roll > triggerChance) return;

        //event tetiklendi
        currentEvent = candidate;
        triggeredEvents.Add(currentEvent);

        //operasyonu duraklat, event fazına geç
        currentState = SmuggleState.EventPhase;
        eventDecisionTimer = eventDecisionTime;

        OnSmuggleEventTriggered?.Invoke(currentEvent);
    }

    /// <summary>
    /// Event türüne göre tetiklenme şansını döner (0-100).
    /// </summary>
    private float GetTriggerChance(SmuggleEventTrigger triggerType)
    {
        switch (triggerType)
        {
            case SmuggleEventTrigger.Risk:
                //rota riski, kuryenin becerisi yüksekse düşer (reliability 100 → yarıya iner, 0 → tam risk)
                return selectedRoute.riskLevel * (1f - selectedCourier.reliability / 200f);
            case SmuggleEventTrigger.Betrayal:
                return selectedCourier.betrayalChance * 100f; //ihanet olasılığı (0-1 → 0-100)
            case SmuggleEventTrigger.Incompetence:
                return 100f - selectedCourier.reliability; //beceriksizlik (reliability 80 → %20 şans)
            default:
                return 0f;
        }
    }

    /// <summary>
    /// Oyuncu event seçimi yaptı. UI bu metodu çağırır.
    /// </summary>
    public void ResolveEvent(int choiceIndex)
    {
        if (currentState != SmuggleState.EventPhase || currentEvent == null) return;

        if (choiceIndex < 0 || choiceIndex >= currentEvent.choices.Count) return;

        SmuggleEventChoice choice = currentEvent.choices[choiceIndex];

        //modifier'ları biriktir
        accumulatedSuccessModifier += choice.successModifier;
        accumulatedSuspicionModifier += choice.suspicionModifier;
        accumulatedCostModifier += choice.costModifier;

        //seçime göre aktif event havuzunu güncelle
        //nextEventPool boş veya null ise zincir biter, artık event tetiklenmez
        activeEventPool = (choice.nextEventPool != null && choice.nextEventPool.Count > 0)
            ? choice.nextEventPool
            : null;

        OnSmuggleEventResolved?.Invoke(choice);

        //operasyona geri dön
        currentEvent = null;
        currentState = SmuggleState.InProgress;
    }

    /// <summary>
    /// Operasyon sonucunu hesaplar ve uygular. Event modifier'larını dahil eder.
    /// </summary>
    private void CalculateResult()
    {
        //başarı olasılığı: tamamen eventlere bağlı — event yoksa veya iyi yönetildiyse başarılı
        float successChance = 100f + accumulatedSuccessModifier;
        successChance = Mathf.Clamp(successChance, 5f, 95f); //her zaman %5-95 arası

        float roll = UnityEngine.Random.Range(0f, 100f);
        bool success = roll <= successChance;

        SmuggleResult result = new SmuggleResult();
        result.success = success;
        result.route = selectedRoute;
        result.courier = selectedCourier;

        if (success)
        {
            //maliyet peşin ödendi, burada sadece kazanç ve event kayıpları hesaplanır
            result.wealthChange = currentRoutePack.baseReward - accumulatedCostModifier;
            result.suspicionChange = selectedRoute.riskLevel * 0.1f + accumulatedSuspicionModifier;
        }
        else
        {
            //maliyet zaten ödendi, event kayıpları ek zarar olarak uygulanır
            result.wealthChange = -accumulatedCostModifier;
            result.suspicionChange = selectedRoute.riskLevel * 0.3f + accumulatedSuspicionModifier;
        }

        //stat'lara uygula
        if (GameStatManager.Instance != null)
        {
            GameStatManager.Instance.AddWealth(result.wealthChange);
            GameStatManager.Instance.AddSuspicion(result.suspicionChange);
        }

        //cooldown başlat
        if (MinigameManager.Instance != null)
            MinigameManager.Instance.StartCooldown(minigameData);

        //durumu sıfırla
        currentState = SmuggleState.Idle;

        OnOperationCompleted?.Invoke(result);
    }

    /// <summary>
    /// Seçim aşamasını iptal eder. Henüz para ödenmediği için iade yoktur.
    /// </summary>
    public void CancelSelection()
    {
        if (currentState != SmuggleState.SelectingRoute && currentState != SmuggleState.SelectingCourier) return;

        currentState = SmuggleState.Idle;
    }

    /// <summary>
    /// Operasyonu iptal eder. Kalan yola göre kısmi iade yapılır.
    /// İade formülü: ödenen maliyet * (kalan yüzde / 2)
    /// </summary>
    public void CancelOperation()
    {
        if (currentState != SmuggleState.InProgress && currentState != SmuggleState.EventPhase) return;

        //kalan yol yüzdesi (0-1)
        float progress = Mathf.Clamp01(operationTimer / operationDuration);
        float remaining = 1f - progress;

        //iade: ödenen maliyet * (kalan% / 2)
        int totalCost = selectedRoute.cost + selectedCourier.cost;
        float refundAmount = totalCost * (remaining / 2f);

        //iadeyi uygula
        if (GameStatManager.Instance != null && refundAmount > 0f)
        {
            GameStatManager.Instance.AddWealth(refundAmount);
        }

        //durumu sıfırla
        currentEvent = null;
        currentState = SmuggleState.Idle;

        OnOperationCancelled?.Invoke(refundAmount);
    }

    /// <summary>
    /// Minigame'in şu an oynanabilir olup olmadığını döner.
    /// </summary>
    public bool CanPlay()
    {
        if (MinigameManager.Instance == null || !MinigameManager.Instance.IsMinigameUnlocked(minigameData))
            return false;
        if (MinigameManager.Instance.IsOnCooldown(minigameData))
            return false;
        if (currentState != SmuggleState.Idle)
            return false;
        return true;
    }

    /// <summary>
    /// Operasyon ilerleme oranını döner (0-1). Operasyon yoksa 0.
    /// </summary>
    public float GetOperationProgress()
    {
        if (currentState != SmuggleState.InProgress && currentState != SmuggleState.EventPhase)
            return 0f;
        return Mathf.Clamp01(operationTimer / operationDuration);
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
    InProgress,      //operasyon devam ediyor (kurye yolda)
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
