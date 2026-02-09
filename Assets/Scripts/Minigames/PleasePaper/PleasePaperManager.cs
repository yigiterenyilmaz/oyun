using System;
using System.Collections.Generic;
using UnityEngine;

public class PleasePaperManager : MonoBehaviour
{
    public static PleasePaperManager Instance { get; private set; }

    [Header("Referanslar")]
    public MiniGameData minigameData; //MinigameManager'dan açık mı kontrolü için
    public PleasePaperDatabase database; //teklif, süreç ve sonuç eventleri

    [Header("Teklif Ayarları")]
    public float minOfferInterval = 90f; //minimum teklif aralığı (saniye)
    public float maxOfferInterval = 150f; //maximum teklif aralığı (saniye)
    public float offerDecisionTime = 15f; //teklif karar süresi (saniye)

    [Header("Süreç Ayarları")]
    public float processDuration = 300f; //gerçek kriz süreci süresi (5 dakika)
    public float processEventInterval = 15f; //süreç sırasında event kontrol aralığı
    //event karar süresi her event'in kendi decisionTime alanından okunur
    public float initialControlStat = 40f; //controlStat başlangıç değeri

    //mevcut durum
    private PleasePaperState currentState = PleasePaperState.Idle;
    private PleasePaperEvent currentOffer; //şu anki teklif eventi (eventType = Offer)

    //controlStat: 0-100 arası, süreç boyunca eventlerle değişir
    private float controlStat;

    //zamanlayıcılar
    private float offerTimer; //teklif bekleme sayacı
    private float nextOfferTime; //bir sonraki teklif zamanı
    private float offerDecisionTimer; //teklif karar geri sayımı
    private float processTimer; //süreç geçen süresi
    private float eventCheckTimer; //event kontrol sayacı
    private float eventDecisionTimer; //event karar geri sayımı

    //event sistemi
    private PleasePaperEvent currentEvent;
    private List<PleasePaperEvent> activeEventPool;
    private List<PleasePaperEvent> triggeredEvents = new List<PleasePaperEvent>();

    //sahte kriz event zinciri takibi
    private int fakeCrisisEventIndex; //sahte kriz zincirinde kaçıncı event

    //hangi state'ten EventPhase'e geçildiğini takip eder (geri dönüş için)
    private PleasePaperState stateBeforeEvent;

    //operasyon boyunca biriken modifier'lar
    private float accumulatedSuspicionModifier;
    private int accumulatedCostModifier;

    //events — UI bu event'leri dinleyecek
    public static event Action<PleasePaperEvent> OnOfferReceived; //teklif geldi (Offer tipi event)
    public static event Action<float> OnOfferDecisionTimerUpdate; //teklif karar sayacı
    public static event Action<PleasePaperEvent, float> OnProcessStarted; //süreç başladı (offer, süre)
    public static event Action<float> OnProcessProgress; //ilerleme (0-1)
    public static event Action<float> OnControlStatChanged; //controlStat değişti
    public static event Action<PleasePaperEvent> OnPleasePaperEventTriggered; //event tetiklendi
    public static event Action<float> OnEventDecisionTimerUpdate; //event karar sayacı
    public static event Action<PleasePaperEventChoice> OnPleasePaperEventResolved; //seçim yapıldı
    public static event Action<float> OnBargainingStarted; //pazarlık başladı (bargainingPower)
    public static event Action<string> OnGameOver; //süreç başarısız bitti (sebep)
    public static event Action<PleasePaperResult> OnProcessCompleted; //süreç bitti
#pragma warning disable 0067
    public static event Action<string> OnPleasePaperFailed; //minigame başlatılamadı (ileride kullanılacak)
#pragma warning restore 0067

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        nextOfferTime = UnityEngine.Random.Range(minOfferInterval, maxOfferInterval);
    }

    private void Update()
    {
        switch (currentState)
        {
            case PleasePaperState.Idle:
                UpdateIdle();
                break;
            case PleasePaperState.OfferPending:
                UpdateOfferPending();
                break;
            case PleasePaperState.FakeCrisisProcess:
                UpdateFakeCrisisProcess();
                break;
            case PleasePaperState.ActiveProcess:
                UpdateActiveProcess();
                break;
            case PleasePaperState.EventPhase:
                UpdateEventPhase();
                break;
        }
    }

    /// <summary>
    /// Idle: teklif zamanlayıcısı çalışır, süre dolunca teklif gelir.
    /// </summary>
    private void UpdateIdle()
    {
        //minigame açık değilse timer çalışmasın
        if (MinigameManager.Instance == null || !MinigameManager.Instance.IsMinigameUnlocked(minigameData))
            return;

        //cooldown'daysa timer çalışmasın
        if (MinigameManager.Instance.IsOnCooldown(minigameData))
            return;

        offerTimer += Time.deltaTime;
        if (offerTimer >= nextOfferTime)
        {
            offerTimer = 0f;
            GenerateOffer();
        }
    }

    /// <summary>
    /// OfferPending: oyuncu teklif hakkında karar veriyor, geri sayım çalışıyor.
    /// </summary>
    private void UpdateOfferPending()
    {
        offerDecisionTimer -= Time.deltaTime;
        OnOfferDecisionTimerUpdate?.Invoke(offerDecisionTimer);

        //süre doldu — teklifi otomatik reddet
        if (offerDecisionTimer <= 0f)
        {
            RejectOffer();
        }
    }

    /// <summary>
    /// FakeCrisisProcess: sahte kriz event zinciri sırayla gösterilir.
    /// EventPhase'den dönünce sıradaki event tetiklenir.
    /// </summary>
    private void UpdateFakeCrisisProcess()
    {
        //sahte kriz zincirinde sıradaki event'i tetikle
        TriggerNextFakeCrisisEvent();
    }

    /// <summary>
    /// ActiveProcess: gerçek kriz süreci, timer ilerler, event'ler kontrol edilir.
    /// </summary>
    private void UpdateActiveProcess()
    {
        processTimer += Time.deltaTime;

        //UI'a ilerleme bildir
        float progress = Mathf.Clamp01(processTimer / processDuration);
        OnProcessProgress?.Invoke(progress);

        //event kontrol
        eventCheckTimer += Time.deltaTime;
        if (eventCheckTimer >= processEventInterval)
        {
            eventCheckTimer = 0f;
            TryTriggerProcessEvent();

            //event tetiklendiyse bu frame'de süreç sonucu hesaplama
            if (currentState != PleasePaperState.ActiveProcess) return;
        }

        //süreç bitti mi
        if (processTimer >= processDuration)
        {
            CalculateResult();
        }
    }

    /// <summary>
    /// EventPhase: event karar sayacı geri sayıyor.
    /// </summary>
    private void UpdateEventPhase()
    {
        eventDecisionTimer -= Time.deltaTime;
        OnEventDecisionTimerUpdate?.Invoke(eventDecisionTimer);

        //süre doldu — ilk seçeneği otomatik seç
        if (eventDecisionTimer <= 0f)
        {
            ResolveEvent(0);
        }
    }

    /// <summary>
    /// Havuzdan rastgele teklif eventi seçer, sunar.
    /// </summary>
    private void GenerateOffer()
    {
        if (database.offerEvents == null || database.offerEvents.Count == 0) return;

        //EventCoordinator slot kontrolü — başka bir event aktifse teklif ertelenir
        if (EventCoordinator.IsEventSlotOccupied())
        {
            //bu cycle'ı atla, bir sonraki interval'de tekrar dene
            nextOfferTime = UnityEngine.Random.Range(minOfferInterval, maxOfferInterval);
            return;
        }

        //rastgele teklif eventi seç
        int idx = UnityEngine.Random.Range(0, database.offerEvents.Count);
        currentOffer = database.offerEvents[idx];

        //slot'u al
        EventCoordinator.TryAcquireEventSlot("PleasePaper");

        currentState = PleasePaperState.OfferPending;
        offerDecisionTimer = offerDecisionTime;

        OnOfferReceived?.Invoke(currentOffer);
    }

    /// <summary>
    /// Oyuncu teklifi kabul etti. Sahte kriz mi gerçek kriz mi kontrol edilir.
    /// </summary>
    public void AcceptOffer()
    {
        if (currentState != PleasePaperState.OfferPending || currentOffer == null) return;

        if (currentOffer.isFakeCrisis)
        {
            StartFakeCrisisProcess();
        }
        else
        {
            StartActiveProcess();
        }
    }

    /// <summary>
    /// Oyuncu teklifi reddetti. Idle'a dön, yeni timer başlat.
    /// </summary>
    public void RejectOffer()
    {
        if (currentState != PleasePaperState.OfferPending) return;

        //slot'u bırak
        EventCoordinator.ReleaseEventSlot("PleasePaper");

        currentOffer = null;
        currentState = PleasePaperState.Idle;
        nextOfferTime = UnityEngine.Random.Range(minOfferInterval, maxOfferInterval);
        offerTimer = 0f;
    }

    /// <summary>
    /// Sahte kriz zincirini başlatır. Offer event'inin fakeCrisisEvents listesi sırayla gösterilir.
    /// </summary>
    private void StartFakeCrisisProcess()
    {
        currentState = PleasePaperState.FakeCrisisProcess;
        fakeCrisisEventIndex = 0;
        accumulatedSuspicionModifier = 0f;
        accumulatedCostModifier = 0;
        triggeredEvents.Clear();

        //ilk event'i hemen tetikle
        TriggerNextFakeCrisisEvent();
    }

    /// <summary>
    /// Sahte kriz zincirinde sıradaki event'i tetikler.
    /// Zincir bittiyse sahte kriz sonucunu hesaplar.
    /// </summary>
    private void TriggerNextFakeCrisisEvent()
    {
        if (currentOffer.fakeCrisisEvents == null ||
            fakeCrisisEventIndex >= currentOffer.fakeCrisisEvents.Count)
        {
            //zincir bitti — sahte kriz sonucunu hesapla
            CompleteFakeCrisis();
            return;
        }

        currentEvent = currentOffer.fakeCrisisEvents[fakeCrisisEventIndex];
        fakeCrisisEventIndex++;

        stateBeforeEvent = PleasePaperState.FakeCrisisProcess;
        currentState = PleasePaperState.EventPhase;
        eventDecisionTimer = currentEvent.decisionTime;

        OnPleasePaperEventTriggered?.Invoke(currentEvent);
    }

    /// <summary>
    /// Sahte kriz tamamlandı. Oyuncu zararlı çıkar.
    /// </summary>
    private void CompleteFakeCrisis()
    {
        PleasePaperResult result = new PleasePaperResult();
        result.success = false;
        result.isFakeCrisis = true;
        result.offer = currentOffer;
        result.wealthChange = -accumulatedCostModifier;
        result.suspicionChange = accumulatedSuspicionModifier;

        //stat'lara uygula
        if (GameStatManager.Instance != null)
        {
            if (result.wealthChange != 0)
                GameStatManager.Instance.AddWealth(result.wealthChange);
            if (result.suspicionChange != 0)
                GameStatManager.Instance.AddSuspicion(result.suspicionChange);
        }

        //cooldown başlat
        if (MinigameManager.Instance != null)
            MinigameManager.Instance.StartCooldown(minigameData);

        //slot'u bırak
        EventCoordinator.ReleaseEventSlot("PleasePaper");

        //durumu sıfırla
        ResetState();

        OnProcessCompleted?.Invoke(result);
    }

    /// <summary>
    /// Gerçek kriz sürecini başlatır. controlStat 40'tan başlar, 5 dakika sürer.
    /// </summary>
    private void StartActiveProcess()
    {
        currentState = PleasePaperState.ActiveProcess;

        controlStat = initialControlStat;
        processTimer = 0f;
        eventCheckTimer = 0f;
        accumulatedSuspicionModifier = 0f;
        accumulatedCostModifier = 0;
        triggeredEvents.Clear();
        currentEvent = null;
        activeEventPool = database.processEvents; //başlangıç havuzu

        OnProcessStarted?.Invoke(currentOffer, processDuration);
        OnControlStatChanged?.Invoke(controlStat);
    }

    /// <summary>
    /// Süreç sırasında event tetiklemeyi dener.
    /// </summary>
    private void TryTriggerProcessEvent()
    {
        if (activeEventPool == null || activeEventPool.Count == 0) return;

        //daha önce tetiklenmemiş eventleri filtrele
        List<PleasePaperEvent> available = new List<PleasePaperEvent>();
        for (int i = 0; i < activeEventPool.Count; i++)
        {
            if (!triggeredEvents.Contains(activeEventPool[i]))
                available.Add(activeEventPool[i]);
        }

        if (available.Count == 0) return;

        //rastgele bir event seç
        int idx = UnityEngine.Random.Range(0, available.Count);
        currentEvent = available[idx];
        triggeredEvents.Add(currentEvent);

        //event fazına geç
        stateBeforeEvent = PleasePaperState.ActiveProcess;
        currentState = PleasePaperState.EventPhase;
        eventDecisionTimer = currentEvent.decisionTime;

        OnPleasePaperEventTriggered?.Invoke(currentEvent);
    }

    /// <summary>
    /// Oyuncu event seçimi yaptı. controlStat güncellenir, zincir kontrol edilir.
    /// </summary>
    public void ResolveEvent(int choiceIndex)
    {
        if (currentState != PleasePaperState.EventPhase || currentEvent == null) return;
        if (choiceIndex < 0 || choiceIndex >= currentEvent.choices.Count) return;

        PleasePaperEventChoice choice = currentEvent.choices[choiceIndex];

        //modifier'ları biriktir
        accumulatedSuspicionModifier += choice.suspicionModifier;
        accumulatedCostModifier += choice.costModifier;

        //controlStat güncelle (sadece gerçek kriz sürecinde anlamlı)
        controlStat = Mathf.Clamp(controlStat + choice.controlStatModifier, 0f, 100f);
        OnControlStatChanged?.Invoke(controlStat);

        //event havuzunu güncelle
        activeEventPool = (choice.nextEventPool != null && choice.nextEventPool.Count > 0)
            ? choice.nextEventPool
            : null;

        OnPleasePaperEventResolved?.Invoke(choice);

        currentEvent = null;

        //hangi state'ten geldiğimize göre geri dön
        if (stateBeforeEvent == PleasePaperState.FakeCrisisProcess)
        {
            //sahte kriz — zincire geri dön
            currentState = PleasePaperState.FakeCrisisProcess;
        }
        else
        {
            //gerçek kriz — controlStat 0 kontrolü
            if (controlStat <= 0f)
            {
                FailProcess();
                return;
            }

            currentState = PleasePaperState.ActiveProcess;
        }
    }

    /// <summary>
    /// Süreç sonu: controlStat < 50 → fail, >= 50 → pazarlık.
    /// </summary>
    private void CalculateResult()
    {
        if (controlStat < 50f)
        {
            FailProcess();
            return;
        }

        //başarı — pazarlık fazına geç
        StartBargaining();
    }

    /// <summary>
    /// Pazarlık fazını başlatır. bargainingPower controlStat'a göre hesaplanır.
    /// Endgame event'i (index 0) tetiklenir.
    /// </summary>
    private void StartBargaining()
    {
        currentState = PleasePaperState.BargainingPhase;

        float bargainingPower = (controlStat - 50f) / 50f; //0.0 - 1.0 arası

        OnBargainingStarted?.Invoke(bargainingPower);

        CompleteBargaining(bargainingPower);
    }

    /// <summary>
    /// Pazarlık tamamlandı. Nihai kazanç hesaplanır, stat'lara uygulanır.
    /// </summary>
    private void CompleteBargaining(float bargainingPower)
    {
        PleasePaperResult result = new PleasePaperResult();
        result.success = true;
        result.isFakeCrisis = false;
        result.offer = currentOffer;
        result.bargainingPower = bargainingPower;
        result.finalControlStat = controlStat;

        //kazanç: taban ödül * (0.5 + pazarlık gücü * 0.5)
        //controlStat 50 → %50 kazanç, controlStat 100 → %100 kazanç
        result.wealthChange = currentOffer.baseReward * (0.5f + bargainingPower * 0.5f)
            - accumulatedCostModifier;
        result.suspicionChange = accumulatedSuspicionModifier;

        //stat'lara uygula
        if (GameStatManager.Instance != null)
        {
            if (result.wealthChange != 0)
                GameStatManager.Instance.AddWealth(result.wealthChange);
            if (result.suspicionChange != 0)
                GameStatManager.Instance.AddSuspicion(result.suspicionChange);
        }

        //cooldown başlat
        if (MinigameManager.Instance != null)
            MinigameManager.Instance.StartCooldown(minigameData);

        //slot'u bırak
        EventCoordinator.ReleaseEventSlot("PleasePaper");

        //durumu sıfırla
        ResetState();

        OnProcessCompleted?.Invoke(result);
    }

    /// <summary>
    /// Süreç başarısız oldu. controlStat 0'a düştü veya süreç sonunda < 50.
    /// </summary>
    private void FailProcess()
    {
        string reason = controlStat <= 0f
            ? "Control stat dropped to zero."
            : "Control stat below threshold at process end.";
        OnGameOver?.Invoke(reason);

        PleasePaperResult result = new PleasePaperResult();
        result.success = false;
        result.isFakeCrisis = false;
        result.offer = currentOffer;
        result.finalControlStat = controlStat;

        //event kayıpları ek zarar
        result.wealthChange = -accumulatedCostModifier;
        result.suspicionChange = accumulatedSuspicionModifier;

        //stat'lara uygula
        if (GameStatManager.Instance != null)
        {
            if (result.wealthChange != 0)
                GameStatManager.Instance.AddWealth(result.wealthChange);
            if (result.suspicionChange != 0)
                GameStatManager.Instance.AddSuspicion(result.suspicionChange);
        }

        //cooldown başlat
        if (MinigameManager.Instance != null)
            MinigameManager.Instance.StartCooldown(minigameData);

        //slot'u bırak
        EventCoordinator.ReleaseEventSlot("PleasePaper");

        //durumu sıfırla
        ResetState();

        OnProcessCompleted?.Invoke(result);
    }

    /// <summary>
    /// Tüm state değişkenlerini sıfırlar. Süreç/sahte kriz bittikten sonra çağrılır.
    /// </summary>
    private void ResetState()
    {
        currentEvent = null;
        currentOffer = null;
        currentState = PleasePaperState.Idle;
        nextOfferTime = UnityEngine.Random.Range(minOfferInterval, maxOfferInterval);
        offerTimer = 0f;
    }

    /// <summary>
    /// Minigame'in şu an aktif olup olmadığını döner.
    /// </summary>
    public bool IsActive()
    {
        return currentState != PleasePaperState.Idle;
    }

    public PleasePaperState GetCurrentState()
    {
        return currentState;
    }

    public float GetControlStat()
    {
        return controlStat;
    }

    /// <summary>
    /// Süreç ilerleme oranını döner (0-1). Süreç yoksa 0.
    /// </summary>
    public float GetProcessProgress()
    {
        if (currentState != PleasePaperState.ActiveProcess && currentState != PleasePaperState.EventPhase)
            return 0f;
        return Mathf.Clamp01(processTimer / processDuration);
    }
}

/// <summary>
/// Please Paper minigame durumları
/// </summary>
public enum PleasePaperState
{
    Idle,               //teklif bekleniyor
    OfferPending,       //teklif geldi, karar bekleniyor
    FakeCrisisProcess,  //sahte kriz event zinciri
    ActiveProcess,      //gerçek kriz süreci devam ediyor
    EventPhase,         //event geldi, karar bekleniyor
    BargainingPhase     //pazarlık aşaması
}

/// <summary>
/// Süreç sonucu
/// </summary>
[System.Serializable]
public class PleasePaperResult
{
    public bool success;
    public bool isFakeCrisis;
    public PleasePaperEvent offer; //teklif eventi
    public float wealthChange;
    public float suspicionChange;
    public float bargainingPower; //başarılıysa pazarlık gücü (0-1)
    public float finalControlStat; //süreç sonundaki controlStat
}
