using System;
using System.Collections.Generic;
using UnityEngine;

public class WarForOilManager : MonoBehaviour
{
    public static WarForOilManager Instance { get; private set; }

    [Header("Referanslar")]
    public MiniGameData minigameData;
    public WarForOilDatabase database;

    //mevcut durum
    private WarForOilState currentState = WarForOilState.Idle;
    private WarForOilCountry selectedCountry;

    //baskı fazı
    private float pressureCooldownTimer;

    //savaş fazı
    private float supportStat;
    private float warTimer;
    private float eventCheckTimer;
    private float eventDecisionTimer;
    private WarForOilEvent currentEvent;
    private List<WarForOilEvent> triggeredEvents = new List<WarForOilEvent>();

    //operasyon boyunca biriken modifier'lar
    private float accumulatedSuspicionModifier;
    private int accumulatedCostModifier;

    //sonuç ekranı beklerken saklanan sonuç
    private WarForOilResult pendingResult;

    //savaş kaybedilirse minigame kalıcı olarak devre dışı kalır
    private bool permanentlyDisabled;

    //kazanılan (işgal edilen) ülkeler — tekrar seçilemez
    private HashSet<WarForOilCountry> conqueredCountries = new HashSet<WarForOilCountry>();

    //ülke rotasyonu — UI'da görünen 3 ülke
    private List<WarForOilCountry> activeCountries = new List<WarForOilCountry>();
    private Dictionary<WarForOilCountry, float> countryArrivalTime = new Dictionary<WarForOilCountry, float>();
    private float rotationTimer;
    private bool rotationInitialized;

    //events — UI bu event'leri dinleyecek
    public static event Action<WarForOilCountry> OnCountrySelected;
    public static event Action<bool, float> OnPressureResult; //başarı, cooldown süresi (başarısızsa)
    public static event Action<float> OnPressureCooldownUpdate; //kalan cooldown süresi
    public static event Action<WarForOilCountry, float> OnWarStarted; //ülke, savaş süresi
    public static event Action<float> OnWarProgress; //ilerleme (0-1)
    public static event Action<WarForOilEvent> OnWarEventTriggered; //event tetiklendi
    public static event Action<float> OnEventDecisionTimerUpdate; //event karar sayacı
    public static event Action<WarForOilEventChoice> OnWarEventResolved; //seçim yapıldı
    public static event Action<WarForOilResult> OnWarResultReady; //sonuç hesaplandı, sonuç ekranını göster
    public static event Action<WarForOilResult> OnWarFinished; //sonuç ekranı kapatıldı, her şey bitti
    public static event Action<List<WarForOilCountry>> OnActiveCountriesChanged; //UI'daki ülke listesi değişti

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
        //ülke rotasyonunu her zaman güncelle (state'ten bağımsız)
        UpdateCountryRotation();

        switch (currentState)
        {
            case WarForOilState.PressurePhase:
                UpdatePressurePhase();
                break;
            case WarForOilState.WarProcess:
                UpdateWarProcess();
                break;
            case WarForOilState.EventPhase:
                UpdateEventPhase();
                break;
        }
    }

    // ==================== UI'IN ÇAĞIRDIĞI METODLAR ====================

    /// <summary>
    /// UI'dan ülke seçimi yapılır. CountrySelection → PressurePhase geçişi.
    /// </summary>
    public void SelectCountry(WarForOilCountry country)
    {
        if (permanentlyDisabled) return;
        if (currentState != WarForOilState.Idle && currentState != WarForOilState.CountrySelection) return;
        if (country == null) return;

        //minigame açık mı ve cooldown'da mı kontrol et
        if (MinigameManager.Instance != null)
        {
            if (!MinigameManager.Instance.IsMinigameUnlocked(minigameData)) return;
            if (MinigameManager.Instance.IsOnCooldown(minigameData)) return;
        }

        //bu ülke zaten işgal edilmiş mi
        if (conqueredCountries.Contains(country)) return;

        //bu ülke aktif listede mi (UI'da görünüyor mu)
        if (!activeCountries.Contains(country)) return;

        selectedCountry = country;
        currentState = WarForOilState.PressurePhase;
        pressureCooldownTimer = 0f;

        OnCountrySelected?.Invoke(country);
    }

    /// <summary>
    /// Oyuncu "Baskı Yap" butonuna bastı. Siyasi nüfuza göre başarı kontrolü.
    /// Başarılı → savaş başlar. Başarısız → cooldown başlar.
    /// </summary>
    public void AttemptPressure()
    {
        if (currentState != WarForOilState.PressurePhase) return;
        if (pressureCooldownTimer > 0f) return; //cooldown devam ediyor

        float politicalInfluence = 0f;
        if (GameStatManager.Instance != null)
            politicalInfluence = GameStatManager.Instance.PoliticalInfluence;

        float successChance = Mathf.Clamp(
            politicalInfluence * database.politicalInfluenceMultiplier,
            0f, 0.95f
        );

        bool success = UnityEngine.Random.value < successChance;

        if (success)
        {
            OnPressureResult?.Invoke(true, 0f);
            StartWar();
        }
        else
        {
            pressureCooldownTimer = database.pressureCooldown;
            OnPressureResult?.Invoke(false, database.pressureCooldown);
        }
    }

    /// <summary>
    /// Oyuncu baskı fazından vazgeçip ülke seçimine geri dönmek istiyor.
    /// </summary>
    public void CancelPressure()
    {
        if (currentState != WarForOilState.PressurePhase) return;

        selectedCountry = null;
        currentState = WarForOilState.Idle;
        pressureCooldownTimer = 0f;
    }

    /// <summary>
    /// Oyuncu event seçimi yaptı.
    /// </summary>
    public void ResolveEvent(int choiceIndex)
    {
        if (currentState != WarForOilState.EventPhase || currentEvent == null) return;
        if (choiceIndex < 0 || choiceIndex >= currentEvent.choices.Count) return;

        WarForOilEventChoice choice = currentEvent.choices[choiceIndex];

        //modifier'ları biriktir
        accumulatedSuspicionModifier += choice.suspicionModifier;
        accumulatedCostModifier += choice.costModifier;

        //supportStat güncelle
        supportStat = Mathf.Clamp(supportStat + choice.supportModifier, 0f, 100f);

        OnWarEventResolved?.Invoke(choice);

        currentEvent = null;

        //oyunu devam ettir
        if (GameManager.Instance != null)
            GameManager.Instance.ResumeGame();

        //savaş sürecine geri dön
        currentState = WarForOilState.WarProcess;
    }

    /// <summary>
    /// Sonuç ekranını kapatır. UI bu metodu çağırır.
    /// Stat'lar uygulanır, oyun devam eder, cooldown başlar.
    /// </summary>
    public void DismissResultScreen()
    {
        if (pendingResult == null) return;

        WarForOilResult result = pendingResult;
        pendingResult = null;

        //stat'lara uygula
        if (GameStatManager.Instance != null)
        {
            if (result.wealthChange != 0)
                GameStatManager.Instance.AddWealth(result.wealthChange);
            if (result.suspicionChange != 0)
                GameStatManager.Instance.AddSuspicion(result.suspicionChange);
            if (result.politicalInfluenceChange != 0)
                GameStatManager.Instance.AddPoliticalInfluence(result.politicalInfluenceChange);
        }

        //savaş kaybedildiyse minigame kalıcı olarak devre dışı
        if (!result.warWon)
            permanentlyDisabled = true;

        //cooldown başlat (kazanıldıysa)
        if (!permanentlyDisabled && MinigameManager.Instance != null)
            MinigameManager.Instance.StartCooldown(minigameData);

        //oyunu devam ettir
        if (GameManager.Instance != null)
            GameManager.Instance.ResumeGame();

        //durumu sıfırla
        ResetState();

        OnWarFinished?.Invoke(result);
    }

    // ==================== ÜLKE ROTASYONU ====================

    /// <summary>
    /// Ülke rotasyonunu başlatır — database'den rastgele ülkeler seçer.
    /// </summary>
    private void InitializeCountryRotation()
    {
        activeCountries.Clear();
        countryArrivalTime.Clear();

        List<WarForOilCountry> pool = GetAvailableCountryPool();

        //havuzdan rastgele seç
        int count = Mathf.Min(database.visibleCountryCount, pool.Count);
        for (int i = 0; i < count; i++)
        {
            int idx = UnityEngine.Random.Range(0, pool.Count);
            WarForOilCountry country = pool[idx];
            activeCountries.Add(country);
            countryArrivalTime[country] = Time.time;
            pool.RemoveAt(idx);
        }

        rotationTimer = 0f;
        rotationInitialized = true;

        OnActiveCountriesChanged?.Invoke(activeCountries);
    }

    /// <summary>
    /// Rotasyon timer'ı günceller. Her interval'de bir ülke değiştirilir.
    /// </summary>
    private void UpdateCountryRotation()
    {
        //minigame açık değilse rotasyon çalışmasın
        if (permanentlyDisabled) return;
        if (MinigameManager.Instance == null || !MinigameManager.Instance.IsMinigameUnlocked(minigameData))
            return;

        //ilk çalıştırmada ülkeleri seç
        if (!rotationInitialized)
        {
            InitializeCountryRotation();
            return;
        }

        rotationTimer += Time.deltaTime;
        if (rotationTimer < database.rotationInterval) return;
        rotationTimer = 0f;

        //swap için uygun ülkeleri bul: en az 1 döngü orada olmuş + şu an savaşta/baskıda olmayan
        List<int> swappableIndices = new List<int>();
        for (int i = 0; i < activeCountries.Count; i++)
        {
            WarForOilCountry country = activeCountries[i];

            //bu ülke şu an seçilmiş ve işlem devam ediyorsa swap'a dahil etme
            if (country == selectedCountry && currentState != WarForOilState.Idle) continue;

            //en az bir rotasyon süresi orada olmuş mu
            if (Time.time - countryArrivalTime[country] < database.rotationInterval) continue;

            swappableIndices.Add(i);
        }

        if (swappableIndices.Count == 0) return;

        //yerine gelecek ülke havuzu — aktif listede olmayan ve conquered olmayan
        List<WarForOilCountry> replacementPool = GetAvailableCountryPool();
        for (int i = 0; i < activeCountries.Count; i++)
            replacementPool.Remove(activeCountries[i]);

        if (replacementPool.Count == 0) return;

        //rastgele birini swap et
        int swapIdx = swappableIndices[UnityEngine.Random.Range(0, swappableIndices.Count)];
        WarForOilCountry oldCountry = activeCountries[swapIdx];
        WarForOilCountry newCountry = replacementPool[UnityEngine.Random.Range(0, replacementPool.Count)];

        activeCountries[swapIdx] = newCountry;
        countryArrivalTime.Remove(oldCountry);
        countryArrivalTime[newCountry] = Time.time;

        OnActiveCountriesChanged?.Invoke(activeCountries);
    }

    /// <summary>
    /// Database'den uygun ülke havuzunu döner (conquered olanlar hariç).
    /// </summary>
    private List<WarForOilCountry> GetAvailableCountryPool()
    {
        List<WarForOilCountry> pool = new List<WarForOilCountry>();
        if (database.countries == null) return pool;

        for (int i = 0; i < database.countries.Count; i++)
        {
            if (!conqueredCountries.Contains(database.countries[i]))
                pool.Add(database.countries[i]);
        }
        return pool;
    }

    // ==================== STATE GÜNCELLEMELERI ====================

    /// <summary>
    /// PressurePhase: cooldown geri sayımı.
    /// </summary>
    private void UpdatePressurePhase()
    {
        if (pressureCooldownTimer > 0f)
        {
            pressureCooldownTimer -= Time.deltaTime;
            if (pressureCooldownTimer < 0f) pressureCooldownTimer = 0f;
            OnPressureCooldownUpdate?.Invoke(pressureCooldownTimer);
        }
    }

    /// <summary>
    /// WarProcess: savaş timer'ı ilerler, event'ler kontrol edilir.
    /// </summary>
    private void UpdateWarProcess()
    {
        warTimer += Time.deltaTime;

        //UI'a ilerleme bildir
        float progress = Mathf.Clamp01(warTimer / database.warDuration);
        OnWarProgress?.Invoke(progress);

        //event kontrol
        eventCheckTimer += Time.deltaTime;
        if (eventCheckTimer >= database.eventInterval)
        {
            eventCheckTimer = 0f;
            TryTriggerWarEvent();

            //event tetiklendiyse bu frame'de savaş sonucu hesaplama
            if (currentState != WarForOilState.WarProcess) return;
        }

        //savaş bitti mi
        if (warTimer >= database.warDuration)
        {
            CalculateWarResult();
        }
    }

    /// <summary>
    /// EventPhase: event karar sayacı (oyun duraklatılmış, unscaledDeltaTime).
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

    // ==================== İÇ MANTIK ====================

    /// <summary>
    /// Savaşı başlatır. PressurePhase → WarProcess geçişi.
    /// </summary>
    private void StartWar()
    {
        currentState = WarForOilState.WarProcess;

        supportStat = database.initialSupportStat;
        warTimer = 0f;
        eventCheckTimer = 0f;
        accumulatedSuspicionModifier = 0f;
        accumulatedCostModifier = 0;
        triggeredEvents.Clear();
        currentEvent = null;

        OnWarStarted?.Invoke(selectedCountry, database.warDuration);
    }

    /// <summary>
    /// Savaş sırasında event tetiklemeyi dener.
    /// </summary>
    private void TryTriggerWarEvent()
    {
        if (selectedCountry.events == null || selectedCountry.events.Count == 0) return;

        //daha önce tetiklenmemiş eventleri filtrele
        List<WarForOilEvent> available = new List<WarForOilEvent>();
        for (int i = 0; i < selectedCountry.events.Count; i++)
        {
            if (!triggeredEvents.Contains(selectedCountry.events[i]))
                available.Add(selectedCountry.events[i]);
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
        currentState = WarForOilState.EventPhase;
        eventDecisionTimer = currentEvent.decisionTime;

        //oyunu duraklat
        if (GameManager.Instance != null)
            GameManager.Instance.PauseGame();

        OnWarEventTriggered?.Invoke(currentEvent);
    }

    /// <summary>
    /// Savaş sonu: kazanma olasılığı hesapla, random check yap.
    /// </summary>
    private void CalculateWarResult()
    {
        //destek oranı (0-1)
        float supportRatio = supportStat / 100f;

        //kazanma şansı hesapla
        float winChance = database.baseWinChance
            - selectedCountry.invasionDifficulty
            + supportRatio * database.supportWinBonus;
        winChance = Mathf.Clamp(winChance, database.minWinChance, database.maxWinChance);

        bool warWon = UnityEngine.Random.value < winChance;

        //sonucu hazırla
        pendingResult = new WarForOilResult();
        pendingResult.country = selectedCountry;
        pendingResult.warWon = warWon;
        pendingResult.finalSupportStat = supportStat;
        pendingResult.winChance = winChance;

        if (warWon)
        {
            //kazanıldı — ödül destek oranına göre
            float reward = database.baseWarReward * selectedCountry.resourceRichness * supportRatio;
            pendingResult.wealthChange = reward - accumulatedCostModifier;
            pendingResult.suspicionChange = accumulatedSuspicionModifier;
            pendingResult.politicalInfluenceChange = 0f; //kazanınca nüfuz değişmez
        }
        else
        {
            //kaybedildi — ceza
            pendingResult.wealthChange = -(database.warLossPenalty + accumulatedCostModifier);
            pendingResult.suspicionChange = database.warLossSuspicionIncrease + accumulatedSuspicionModifier;
            pendingResult.politicalInfluenceChange = -database.warLossPoliticalPenalty;
        }

        currentState = WarForOilState.ResultPhase;

        //kazanıldıysa ülkeyi işgal edilmiş olarak işaretle
        if (warWon)
            conqueredCountries.Add(selectedCountry);

        //oyunu duraklat — sonuç ekranında zaman durmalı
        if (GameManager.Instance != null)
            GameManager.Instance.PauseGame();

        OnWarResultReady?.Invoke(pendingResult);
    }

    /// <summary>
    /// Tüm state değişkenlerini sıfırlar.
    /// </summary>
    private void ResetState()
    {
        currentEvent = null;
        selectedCountry = null;
        currentState = WarForOilState.Idle;
        pressureCooldownTimer = 0f;
    }

    // ==================== GETTER'LAR ====================

    public bool IsActive()
    {
        return currentState != WarForOilState.Idle;
    }

    public bool IsPermanentlyDisabled()
    {
        return permanentlyDisabled;
    }

    public bool IsCountryConquered(WarForOilCountry country)
    {
        return conqueredCountries.Contains(country);
    }

    public WarForOilState GetCurrentState()
    {
        return currentState;
    }

    public WarForOilCountry GetSelectedCountry()
    {
        return selectedCountry;
    }

    public float GetSupportStat()
    {
        return supportStat;
    }

    public List<WarForOilCountry> GetActiveCountries()
    {
        return activeCountries;
    }

    public float GetWarProgress()
    {
        if (currentState != WarForOilState.WarProcess && currentState != WarForOilState.EventPhase)
            return 0f;
        return Mathf.Clamp01(warTimer / database.warDuration);
    }
}

/// <summary>
/// WarForOil minigame durumları
/// </summary>
public enum WarForOilState
{
    Idle,               //minigame aktif değil
    CountrySelection,   //ülke seçimi yapılıyor
    PressurePhase,      //yönetime baskı yapılıyor
    WarProcess,         //savaş devam ediyor
    EventPhase,         //event geldi, karar bekleniyor
    ResultPhase         //sonuç ekranı gösteriliyor
}

/// <summary>
/// Savaş sonucu
/// </summary>
[System.Serializable]
public class WarForOilResult
{
    public WarForOilCountry country;
    public bool warWon;
    public float finalSupportStat;
    public float winChance; //hesaplanan kazanma şansı
    public float wealthChange;
    public float suspicionChange;
    public float politicalInfluenceChange;
}
