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
    public float riskCheckInterval = 1f;       //game over kontrol aralığı (saniye)
    public float riskMultiplier = 0.003f;      //risk çarpanı (risk * (1-stealth) * multiplier = saniyedeki game over şansı)

    //mevcut durum
    private ScientistSmuggleState currentState = ScientistSmuggleState.Idle;
    private ScientistSmuggleEvent currentOffer;

    //gönderilen bilim adamı
    private int assignedScientistIndex = -1;
    private float assignedStealthLevel; //atanan bilim adamının gizlilik seviyesi (süreç boyunca saklanır)

    //risk sistemi
    private float effectiveRiskLevel; //ülke riskLevel + event modifier'ları
    private float accumulatedRiskModifier; //event'lerden biriken risk değişimi

    //zamanlayıcılar
    private float offerTimer;
    private float nextOfferTime;
    private float offerDecisionTimer;
    private float processTimer;
    private float eventCheckTimer;
    private float eventDecisionTimer;
    private float riskCheckTimer;

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
    private HashSet<ScientistSmuggleEvent> completedOffers = new HashSet<ScientistSmuggleEvent>();
    private ScientistSmuggleEvent lastOffer;
    private Dictionary<ScientistSmuggleEvent, int> rejectionCounts = new Dictionary<ScientistSmuggleEvent, int>();
    private const int maxRejectionsBeforeDrop = 2;

    //events — UI bu event'leri dinleyecek
    public static event Action<ScientistSmuggleEvent> OnOfferReceived;              //teklif geldi
    public static event Action<float> OnOfferDecisionTimerUpdate;                   //teklif karar sayacı
    public static event Action<ScientistSmuggleEvent, float> OnProcessStarted;      //süreç başladı (offer, süre)
    public static event Action<float> OnProcessProgress;                            //ilerleme (0-1)
    public static event Action<ScientistSmuggleEvent> OnSmuggleEventTriggered;      //event tetiklendi
    public static event Action<float> OnEventDecisionTimerUpdate;                   //event karar sayacı
    public static event Action<ScientistSmuggleEventChoice> OnSmuggleEventResolved; //seçim yapıldı
    public static event Action<string> OnGameOver;                                  //rastgele game over (sebep)
    public static event Action<ScientistSmuggleResult> OnProcessCompleted;          //süreç bitti (sonuç)

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
    /// ActiveProcess: süreç timer'ı ilerler, risk kontrolü ve event kontrolü yapılır.
    /// </summary>
    private void UpdateActiveProcess()
    {
        processTimer += Time.deltaTime;

        //UI'a ilerleme bildir
        float progress = Mathf.Clamp01(processTimer / processDuration);
        OnProcessProgress?.Invoke(progress);

        //rastgele game over kontrolü
        riskCheckTimer += Time.deltaTime;
        if (riskCheckTimer >= riskCheckInterval)
        {
            riskCheckTimer = 0f;
            if (RollForGameOver())
            {
                FailProcess("Operasyon deşifre oldu.");
                return;
            }
        }

        //event kontrol
        eventCheckTimer += Time.deltaTime;
        if (eventCheckTimer >= processEventInterval)
        {
            eventCheckTimer = 0f;
            TryTriggerProcessEvent();

            if (currentState != ScientistSmuggleState.ActiveProcess) return;
        }

        //süreç bitti — başarı
        if (processTimer >= processDuration)
        {
            CompleteProcess();
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

    // ==================== RİSK SİSTEMİ ====================

    /// <summary>
    /// Game over zarı atar. Şans = riskMultiplier * effectiveRisk * (1 - stealth).
    /// effectiveRisk = ülke riskLevel + event modifier'ları (0-1 arası clamp).
    /// </summary>
    private bool RollForGameOver()
    {
        float adjustedRisk = Mathf.Clamp01(effectiveRiskLevel + accumulatedRiskModifier);
        float gameOverChance = riskMultiplier * adjustedRisk * (1f - assignedStealthLevel);

        if (gameOverChance <= 0f) return false;

        return UnityEngine.Random.value < gameOverChance;
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

        //bilim adamının gizlilik seviyesini sakla
        assignedScientistIndex = scientistIndex;
        assignedStealthLevel = scientist.data.stealthLevel;

        //bilim adamını listeden kalıcı olarak çıkar
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
    /// Süreçi başlatır. Risk seviyesi offer'dan alınır.
    /// </summary>
    private void StartActiveProcess()
    {
        currentState = ScientistSmuggleState.ActiveProcess;

        effectiveRiskLevel = currentOffer.riskLevel;
        accumulatedRiskModifier = 0f;
        accumulatedSuspicionModifier = 0f;
        accumulatedCostModifier = 0;
        processTimer = 0f;
        eventCheckTimer = 0f;
        riskCheckTimer = 0f;
        triggeredEvents.Clear();
        currentEvent = null;

        //offer'ın kendi event havuzu varsa onu kullan, yoksa database'den al
        activeEventPool = (currentOffer.processEvents != null && currentOffer.processEvents.Count > 0)
            ? currentOffer.processEvents
            : database.processEvents;

        OnProcessStarted?.Invoke(currentOffer, processDuration);
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
    /// Oyuncu event seçimi yaptı. Risk modifier biriktirilir.
    /// </summary>
    public void ResolveEvent(int choiceIndex)
    {
        if (currentState != ScientistSmuggleState.EventPhase || currentEvent == null) return;
        if (choiceIndex < 0 || choiceIndex >= currentEvent.choices.Count) return;

        ScientistSmuggleEventChoice choice = currentEvent.choices[choiceIndex];

        //modifier'ları biriktir
        accumulatedRiskModifier += choice.riskModifier;
        accumulatedSuspicionModifier += choice.suspicionModifier;
        accumulatedCostModifier += choice.costModifier;

        OnSmuggleEventResolved?.Invoke(choice);

        currentEvent = null;

        //oyunu devam ettir
        if (GameManager.Instance != null)
            GameManager.Instance.ResumeGame();

        currentState = ScientistSmuggleState.ActiveProcess;
    }

    // ==================== SONUÇ SİSTEMİ ====================

    /// <summary>
    /// Süre doldu — operasyon başarılı. baseReward kazanılır.
    /// </summary>
    private void CompleteProcess()
    {
        currentState = ScientistSmuggleState.ResultScreen;

        pendingResult = new ScientistSmuggleResult();
        pendingResult.success = true;
        pendingResult.offer = currentOffer;
        pendingResult.wealthChange = currentOffer.baseReward - accumulatedCostModifier;
        pendingResult.suspicionChange = accumulatedSuspicionModifier;

        //oyunu duraklat — sonuç ekranında zaman durmalı
        if (GameManager.Instance != null)
            GameManager.Instance.PauseGame();

        OnProcessCompleted?.Invoke(pendingResult);
    }

    /// <summary>
    /// Operasyon deşifre oldu — game over.
    /// </summary>
    private void FailProcess(string reason)
    {
        currentState = ScientistSmuggleState.ResultScreen;

        pendingResult = new ScientistSmuggleResult();
        pendingResult.success = false;
        pendingResult.offer = currentOffer;
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
    }

    /// <summary>
    /// Tüm state değişkenlerini sıfırlar.
    /// </summary>
    private void ResetState()
    {
        currentEvent = null;
        currentOffer = null;
        assignedScientistIndex = -1;
        assignedStealthLevel = 0f;
        accumulatedRiskModifier = 0f;
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

    public float GetProcessProgress()
    {
        if (currentState != ScientistSmuggleState.ActiveProcess && currentState != ScientistSmuggleState.EventPhase)
            return 0f;
        return Mathf.Clamp01(processTimer / processDuration);
    }

    /// <summary>
    /// Şu anki efektif risk seviyesini döner (ülke riski + event modifier'ları, 0-1 arası).
    /// </summary>
    public float GetEffectiveRisk()
    {
        return Mathf.Clamp01(effectiveRiskLevel + accumulatedRiskModifier);
    }
}

/// <summary>
/// ScientistSmuggle minigame durumları
/// </summary>
public enum ScientistSmuggleState
{
    Idle,           //teklif bekleniyor
    OfferPending,   //teklif geldi, karar bekleniyor
    ActiveProcess,  //süreç devam ediyor
    EventPhase,     //event geldi, karar bekleniyor
    ResultScreen    //sonuç ekranı gösteriliyor
}

/// <summary>
/// Süreç sonucu
/// </summary>
[System.Serializable]
public class ScientistSmuggleResult
{
    public bool success;
    public ScientistSmuggleEvent offer;
    public float wealthChange;
    public float suspicionChange;
}
