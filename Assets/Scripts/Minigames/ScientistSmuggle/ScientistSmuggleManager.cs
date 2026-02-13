using System;
using System.Collections.Generic;
using UnityEngine;

public class ScientistSmuggleManager : MonoBehaviour
{
    public static ScientistSmuggleManager Instance { get; private set; }

    [Header("Referanslar")]
    public MiniGameData minigameData; //MinigameManager'dan açık mı kontrolü için
    public ScientistSmuggleDatabase database;

    [Header("Teklif Ayarları")]
    public float minOfferInterval = 90f;  //minimum teklif aralığı (saniye)
    public float maxOfferInterval = 150f; //maximum teklif aralığı (saniye)
    public float offerDecisionTime = 15f; //teklif karar süresi (saniye)

    [Header("Süreç Ayarları")]
    public float processDuration = 300f;       //süreç süresi (5 dakika)
    public float processEventInterval = 15f;   //süreç sırasında event kontrol aralığı
    public float initialControlStat = 40f;     //controlStat başlangıç değeri

    //mevcut durum
    private ScientistSmuggleState currentState = ScientistSmuggleState.Idle;
    private ScientistSmuggleEvent currentOffer; //şu anki teklif eventi

    //gönderilen bilim adamı
    private int assignedScientistIndex = -1; //bu operasyona atanan bilim adamının index'i

    //controlStat: 0-100 arası, süreç boyunca eventlerle değişir
    private float controlStat;

    //zamanlayıcılar
    private float offerTimer;
    private float nextOfferTime;
    private float offerDecisionTimer;
    private float processTimer;
    private float eventCheckTimer;
    private float eventDecisionTimer;

    //event sistemi
    private ScientistSmuggleEvent currentEvent;
    private List<ScientistSmuggleEvent> activeEventPool;
    private List<ScientistSmuggleEvent> triggeredEvents = new List<ScientistSmuggleEvent>();

    //hangi state'ten EventPhase'e geçildiğini takip eder
    private ScientistSmuggleState stateBeforeEvent;

    //operasyon boyunca biriken modifier'lar
    private float accumulatedSuspicionModifier;
    private int accumulatedCostModifier;

    //sonuç ekranı beklerken saklanan sonuç
    private ScientistSmuggleResult pendingResult;

    //offer tekrar sistemi
    private HashSet<ScientistSmuggleEvent> completedOffers = new HashSet<ScientistSmuggleEvent>(); //tamamlanan offer'lar (kalıcı olarak havuzdan çıkar)
    private ScientistSmuggleEvent lastOffer; //son gösterilen offer (art arda tekrar engeli)
    private Dictionary<ScientistSmuggleEvent, int> rejectionCounts = new Dictionary<ScientistSmuggleEvent, int>();
    private const int maxRejectionsBeforeDrop = 2;

    //events — UI bu event'leri dinleyecek
    public static event Action<ScientistSmuggleEvent> OnOfferReceived;                  //teklif geldi
    public static event Action<float> OnOfferDecisionTimerUpdate;                       //teklif karar sayacı
    public static event Action<ScientistSmuggleEvent, float> OnProcessStarted;          //süreç başladı (offer, süre)
    public static event Action<float> OnProcessProgress;                                //ilerleme (0-1)
    public static event Action<float> OnControlStatChanged;                             //controlStat değişti
    public static event Action<ScientistSmuggleEvent> OnSmuggleEventTriggered;          //event tetiklendi
    public static event Action<float> OnEventDecisionTimerUpdate;                       //event karar sayacı
    public static event Action<ScientistSmuggleEventChoice> OnSmuggleEventResolved;     //seçim yapıldı
    public static event Action<float> OnBargainingStarted;                              //pazarlık başladı (bargainingPower)
    public static event Action<string> OnGameOver;                                      //süreç başarısız bitti (sebep)
    public static event Action<ScientistSmuggleResult> OnProcessCompleted;              //süreç bitti (sonuç)

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
            case ScientistSmuggleState.Idle:
                UpdateIdle();
                break;
            case ScientistSmuggleState.OfferPending:
                UpdateOfferPending();
                break;
            case ScientistSmuggleState.ActiveProcess:
                UpdateActiveProcess();
                break;
            case ScientistSmuggleState.EventPhase:
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
            GenerateOffer();
        }
    }

    /// <summary>
    /// OfferPending: oyuncu teklif hakkında karar veriyor, geri sayım çalışıyor.
    /// </summary>
    private void UpdateOfferPending()
    {
        offerDecisionTimer -= Time.unscaledDeltaTime;
        OnOfferDecisionTimerUpdate?.Invoke(offerDecisionTimer);

        //süre doldu — teklifi otomatik reddet
        if (offerDecisionTimer <= 0f)
        {
            RejectOffer();
        }
    }

    /// <summary>
    /// ActiveProcess: süreç süresi, timer ilerler, event'ler kontrol edilir.
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
            if (currentState != ScientistSmuggleState.ActiveProcess) return;
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
        eventDecisionTimer -= Time.unscaledDeltaTime;
        OnEventDecisionTimerUpdate?.Invoke(eventDecisionTimer);

        //süre doldu — default seçeneği otomatik seç
        if (eventDecisionTimer <= 0f)
        {
            int defaultIdx = (currentEvent.defaultChoiceIndex >= 0 &&
                              currentEvent.defaultChoiceIndex < currentEvent.choices.Count)
                ? currentEvent.defaultChoiceIndex
                : 0;
            ResolveEvent(defaultIdx);
        }
    }

    // ==================== TEKLİF SİSTEMİ ====================

    /// <summary>
    /// Havuzdan teklif eventi seçer, sunar.
    /// </summary>
    private void GenerateOffer()
    {
        if (database.offerEvents == null || database.offerEvents.Count == 0) return;

        //EventCoordinator cooldown kontrolü
        if (!EventCoordinator.CanShowEvent()) return;

        //uygun offer'ları filtrele
        List<ScientistSmuggleEvent> available = new List<ScientistSmuggleEvent>();
        for (int i = 0; i < database.offerEvents.Count; i++)
        {
            ScientistSmuggleEvent offer = database.offerEvents[i];
            if (completedOffers.Contains(offer)) continue;
            if (rejectionCounts.ContainsKey(offer) && rejectionCounts[offer] >= maxRejectionsBeforeDrop) continue;
            available.Add(offer);
        }

        //art arda tekrar engeli
        if (available.Count > 1)
            available.Remove(lastOffer);

        if (available.Count == 0) return;

        //rastgele teklif seç
        int idx = UnityEngine.Random.Range(0, available.Count);
        currentOffer = available[idx];
        lastOffer = currentOffer;

        EventCoordinator.MarkEventShown();

        currentState = ScientistSmuggleState.OfferPending;
        offerDecisionTimer = offerDecisionTime;

        //oyunu duraklat — offer karar ekranında zaman durmalı
        if (GameManager.Instance != null)
            GameManager.Instance.PauseGame();

        OnOfferReceived?.Invoke(currentOffer);
    }

    /// <summary>
    /// Oyuncu teklifi kabul etti ve bir bilim adamı atadı.
    /// Bilim adamı eğitimi tamamlanmış olmalı. Atanan bilim adamı kalıcı olarak listeden çıkar.
    /// </summary>
    public void AcceptOffer(int scientistIndex)
    {
        if (currentState != ScientistSmuggleState.OfferPending || currentOffer == null) return;
        if (SkillTreeManager.Instance == null) return;

        //bilim adamı geçerli ve eğitimi tamamlanmış mı kontrol et
        ScientistTraining scientist = SkillTreeManager.Instance.GetScientist(scientistIndex);
        if (scientist == null || !scientist.isCompleted) return;

        //bilim adamını ata ve listeden kalıcı olarak çıkar
        assignedScientistIndex = scientistIndex;
        SkillTreeManager.Instance.RemoveScientist(scientistIndex);

        //oyunu devam ettir
        if (GameManager.Instance != null)
            GameManager.Instance.ResumeGame();

        //tamamlanan offer olarak kaydet
        completedOffers.Add(currentOffer);

        StartActiveProcess();
    }

    /// <summary>
    /// Oyuncu teklifi reddetti. Idle'a dön, yeni timer başlat.
    /// </summary>
    public void RejectOffer()
    {
        if (currentState != ScientistSmuggleState.OfferPending) return;

        //oyunu devam ettir
        if (GameManager.Instance != null)
            GameManager.Instance.ResumeGame();

        //ret sayısını artır
        if (currentOffer != null)
        {
            if (!rejectionCounts.ContainsKey(currentOffer))
                rejectionCounts[currentOffer] = 0;
            rejectionCounts[currentOffer]++;
        }

        currentOffer = null;
        currentState = ScientistSmuggleState.Idle;
        nextOfferTime = UnityEngine.Random.Range(minOfferInterval, maxOfferInterval);
        offerTimer = 0f;
    }

    // ==================== SÜREÇ SİSTEMİ ====================

    /// <summary>
    /// Süreçi başlatır. controlStat başlangıç değerinden başlar.
    /// </summary>
    private void StartActiveProcess()
    {
        currentState = ScientistSmuggleState.ActiveProcess;

        controlStat = initialControlStat;
        processTimer = 0f;
        eventCheckTimer = 0f;
        accumulatedSuspicionModifier = 0f;
        accumulatedCostModifier = 0;
        triggeredEvents.Clear();
        currentEvent = null;

        //offer'ın kendi event havuzu varsa onu kullan, yoksa database'den al
        activeEventPool = (currentOffer.processEvents != null && currentOffer.processEvents.Count > 0)
            ? currentOffer.processEvents
            : database.processEvents;

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
        List<ScientistSmuggleEvent> available = new List<ScientistSmuggleEvent>();
        for (int i = 0; i < activeEventPool.Count; i++)
        {
            if (!triggeredEvents.Contains(activeEventPool[i]))
                available.Add(activeEventPool[i]);
        }

        if (available.Count == 0) return;

        //EventCoordinator cooldown kontrolü
        if (!EventCoordinator.CanShowEvent()) return;

        //rastgele bir event seç
        int idx = UnityEngine.Random.Range(0, available.Count);
        currentEvent = available[idx];
        triggeredEvents.Add(currentEvent);

        EventCoordinator.MarkEventShown();

        //event fazına geç
        stateBeforeEvent = ScientistSmuggleState.ActiveProcess;
        currentState = ScientistSmuggleState.EventPhase;
        eventDecisionTimer = currentEvent.decisionTime;

        //oyunu duraklat — event karar ekranında zaman durmalı
        if (GameManager.Instance != null)
            GameManager.Instance.PauseGame();

        OnSmuggleEventTriggered?.Invoke(currentEvent);
    }

    /// <summary>
    /// Oyuncu event seçimi yaptı. controlStat güncellenir.
    /// </summary>
    public void ResolveEvent(int choiceIndex)
    {
        if (currentState != ScientistSmuggleState.EventPhase || currentEvent == null) return;
        if (choiceIndex < 0 || choiceIndex >= currentEvent.choices.Count) return;

        ScientistSmuggleEventChoice choice = currentEvent.choices[choiceIndex];

        //modifier'ları biriktir
        accumulatedSuspicionModifier += choice.suspicionModifier;
        accumulatedCostModifier += choice.costModifier;

        //controlStat güncelle
        controlStat = Mathf.Clamp(controlStat + choice.controlStatModifier, 0f, 100f);
        OnControlStatChanged?.Invoke(controlStat);

        OnSmuggleEventResolved?.Invoke(choice);

        currentEvent = null;

        //oyunu devam ettir
        if (GameManager.Instance != null)
            GameManager.Instance.ResumeGame();

        //controlStat 0 kontrolü
        if (controlStat <= 0f)
        {
            FailProcess();
            return;
        }

        currentState = ScientistSmuggleState.ActiveProcess;
    }

    // ==================== SONUÇ SİSTEMİ ====================

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

        StartBargaining();
    }

    /// <summary>
    /// Pazarlık fazını başlatır. bargainingPower controlStat'a göre hesaplanır.
    /// </summary>
    private void StartBargaining()
    {
        currentState = ScientistSmuggleState.BargainingPhase;

        float bargainingPower = (controlStat - 50f) / 50f; //0.0 - 1.0 arası

        pendingResult = new ScientistSmuggleResult();
        pendingResult.success = true;
        pendingResult.offer = currentOffer;
        pendingResult.bargainingPower = bargainingPower;
        pendingResult.finalControlStat = controlStat;
        pendingResult.wealthChange = currentOffer.baseReward * (0.5f + bargainingPower * 0.5f)
            - accumulatedCostModifier;
        pendingResult.suspicionChange = accumulatedSuspicionModifier;

        //oyunu duraklat — pazarlık ekranında zaman durmalı
        if (GameManager.Instance != null)
            GameManager.Instance.PauseGame();

        OnBargainingStarted?.Invoke(bargainingPower);
    }

    /// <summary>
    /// Süreç başarısız oldu. controlStat 0'a düştü veya süreç sonunda < 50.
    /// </summary>
    private void FailProcess()
    {
        currentState = ScientistSmuggleState.BargainingPhase; //sonuç ekranı bekliyor

        string reason = controlStat <= 0f
            ? "Control stat dropped to zero."
            : "Control stat below threshold at process end.";

        pendingResult = new ScientistSmuggleResult();
        pendingResult.success = false;
        pendingResult.offer = currentOffer;
        pendingResult.finalControlStat = controlStat;
        pendingResult.wealthChange = -accumulatedCostModifier;
        pendingResult.suspicionChange = accumulatedSuspicionModifier;

        //oyunu duraklat — game over ekranında zaman durmalı
        if (GameManager.Instance != null)
            GameManager.Instance.PauseGame();

        OnGameOver?.Invoke(reason);
    }

    /// <summary>
    /// Sonuç ekranını kapatır. UI bu metodu çağırır.
    /// Stat'lar uygulanır, oyun devam eder, cooldown başlar.
    /// </summary>
    public void DismissResultScreen()
    {
        if (pendingResult == null) return;

        ScientistSmuggleResult result = pendingResult;
        pendingResult = null;

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

        //oyunu devam ettir
        if (GameManager.Instance != null)
            GameManager.Instance.ResumeGame();

        ResetState();

        OnProcessCompleted?.Invoke(result);
    }

    /// <summary>
    /// Tüm state değişkenlerini sıfırlar.
    /// </summary>
    private void ResetState()
    {
        currentEvent = null;
        currentOffer = null;
        assignedScientistIndex = -1;
        currentState = ScientistSmuggleState.Idle;
        nextOfferTime = UnityEngine.Random.Range(minOfferInterval, maxOfferInterval);
        offerTimer = 0f;
    }

    // ==================== GETTER'LAR ====================

    public bool IsActive()
    {
        return currentState != ScientistSmuggleState.Idle;
    }

    public ScientistSmuggleState GetCurrentState()
    {
        return currentState;
    }

    public float GetControlStat()
    {
        return controlStat;
    }

    public float GetProcessProgress()
    {
        if (currentState != ScientistSmuggleState.ActiveProcess && currentState != ScientistSmuggleState.EventPhase)
            return 0f;
        return Mathf.Clamp01(processTimer / processDuration);
    }
}

/// <summary>
/// ScientistSmuggle minigame durumları
/// </summary>
public enum ScientistSmuggleState
{
    Idle,            //teklif bekleniyor
    OfferPending,    //teklif geldi, karar bekleniyor
    ActiveProcess,   //süreç devam ediyor
    EventPhase,      //event geldi, karar bekleniyor
    BargainingPhase  //pazarlık aşaması
}

/// <summary>
/// Süreç sonucu
/// </summary>
[System.Serializable]
public class ScientistSmuggleResult
{
    public bool success;
    public ScientistSmuggleEvent offer;    //teklif eventi
    public float wealthChange;
    public float suspicionChange;
    public float bargainingPower;          //başarılıysa pazarlık gücü (0-1)
    public float finalControlStat;         //süreç sonundaki controlStat
}
