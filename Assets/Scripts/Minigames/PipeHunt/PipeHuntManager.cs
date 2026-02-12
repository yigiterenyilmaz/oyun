using System;
using System.Collections.Generic;
using UnityEngine;

public class PipeHuntManager : MonoBehaviour
{
    public static PipeHuntManager Instance { get; private set; }

    [Header("Referanslar")]
    public MiniGameData minigameData;
    public PipeHuntDatabase database;

    //state
    private PipeHuntState currentState = PipeHuntState.Idle;
    private List<PipeInstance> pipes = new List<PipeInstance>();
    private int pickaxeRemainingDurability;
    private float gameTimer;
    private float accumulatedIncome;

    //events — UI dinleyecek
    public static event Action<List<PipeInstance>, float> OnGameStarted; //borular, süre
    public static event Action<float> OnTimerUpdate; //kalan süre
    public static event Action<PipeInstance, int> OnPipeHit; //vurulan boru, kalan boru dayanıklılığı
    public static event Action<PipeInstance> OnPipeBurst; //patlayan boru
    public static event Action<int> OnEmptyHit; //boş zemine vuruldu, kalan kazma dayanıklılığı
    public static event Action<int> OnPickaxeDamaged; //kazma hasar aldı, kalan dayanıklılık
    public static event Action OnPickaxeBroken; //kazma kırıldı
    public static event Action<float> OnIncomeUpdate; //toplam biriken gelir
    public static event Action<PipeHuntResult> OnGameFinished; //minigame bitti

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
        if (currentState != PipeHuntState.Active) return;

        //süre sayacı (oyun duraklatılmış, unscaled kullan)
        gameTimer -= Time.unscaledDeltaTime;
        OnTimerUpdate?.Invoke(gameTimer);

        //patlayan borulardan gelir biriktir
        AccumulateIncome();

        //süre doldu
        if (gameTimer <= 0f)
        {
            gameTimer = 0f;
            FinishGame(PipeHuntEndReason.TimeUp);
        }
    }

    // ==================== UI'IN ÇAĞIRDIĞI METODLAR ====================

    /// <summary>
    /// Minigame'i başlatır. Boruları yerleştirir, timer'ı başlatır.
    /// </summary>
    public void StartGame()
    {
        if (currentState != PipeHuntState.Idle) return;

        //minigame açık mı ve cooldown'da mı kontrol et
        if (MinigameManager.Instance != null)
        {
            if (!MinigameManager.Instance.IsMinigameUnlocked(minigameData)) return;
            if (MinigameManager.Instance.IsOnCooldown(minigameData)) return;
        }

        //boruları yerleştir
        GeneratePipes();

        //kazma ve timer
        pickaxeRemainingDurability = database.pickaxeDurability;
        gameTimer = database.gameDuration;
        accumulatedIncome = 0f;

        currentState = PipeHuntState.Active;

        //ana oyunu duraklat — minigame kendi zamanını unscaled ile yönetir
        if (GameManager.Instance != null)
            GameManager.Instance.PauseGame();

        OnGameStarted?.Invoke(pipes, database.gameDuration);
    }

    /// <summary>
    /// Oyuncu bir boruya vurdu. UI boru id'sini gönderir.
    /// </summary>
    public void HitPipe(int pipeId)
    {
        if (currentState != PipeHuntState.Active) return;

        PipeInstance pipe = GetPipeById(pipeId);
        if (pipe == null || pipe.isBurst) return;

        //boruya hasar ver
        pipe.remainingDurability -= database.damagePerHit;

        //kazmaya hasar ver
        pickaxeRemainingDurability -= database.damagePerHit;
        OnPickaxeDamaged?.Invoke(pickaxeRemainingDurability);

        if (pipe.remainingDurability <= 0)
        {
            //boru patladı
            pipe.remainingDurability = 0;
            pipe.isBurst = true;
            pipe.burstTime = Time.unscaledTime;
            OnPipeBurst?.Invoke(pipe);
        }
        else
        {
            OnPipeHit?.Invoke(pipe, pipe.remainingDurability);
        }

        //kazma kırıldı mı
        if (pickaxeRemainingDurability <= 0)
        {
            pickaxeRemainingDurability = 0;
            OnPickaxeBroken?.Invoke();
            FinishGame(PipeHuntEndReason.PickaxeBroken);
        }
    }

    /// <summary>
    /// Oyuncu boş zemine vurdu. Kazma aşınır ama boru hasar almaz.
    /// </summary>
    public void HitEmpty()
    {
        if (currentState != PipeHuntState.Active) return;

        //kazmaya hasar ver
        pickaxeRemainingDurability -= database.damagePerHit;
        OnPickaxeDamaged?.Invoke(pickaxeRemainingDurability);
        OnEmptyHit?.Invoke(pickaxeRemainingDurability);

        //kazma kırıldı mı
        if (pickaxeRemainingDurability <= 0)
        {
            pickaxeRemainingDurability = 0;
            OnPickaxeBroken?.Invoke();
            FinishGame(PipeHuntEndReason.PickaxeBroken);
        }
    }

    // ==================== İÇ MANTIK ====================

    /// <summary>
    /// Boruları rastgele yerleştirir. Borular arası minimum mesafe kontrolü yapar.
    /// </summary>
    private void GeneratePipes()
    {
        pipes.Clear();

        if (database.pipeTypes == null || database.pipeTypes.Count == 0) return;

        int maxAttempts = 100; //sonsuz döngü koruması

        for (int i = 0; i < database.pipeCount; i++)
        {
            //rastgele boru tipi
            PipeType pipeType = database.pipeTypes[UnityEngine.Random.Range(0, database.pipeTypes.Count)];

            //rastgele pozisyon — minimum mesafe kontrolü
            Vector2 position = Vector2.zero;
            bool validPosition = false;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                position = new Vector2(
                    UnityEngine.Random.Range(0.05f, 0.95f),
                    UnityEngine.Random.Range(0.05f, 0.95f)
                );

                if (IsPositionValid(position))
                {
                    validPosition = true;
                    break;
                }
            }

            if (!validPosition) continue; //yer bulunamadıysa bu boruyu atla

            PipeInstance pipe = new PipeInstance(i, pipeType, position);
            pipes.Add(pipe);
        }
    }

    /// <summary>
    /// Pozisyonun mevcut borulara yeterince uzak olup olmadığını kontrol eder.
    /// </summary>
    private bool IsPositionValid(Vector2 position)
    {
        for (int i = 0; i < pipes.Count; i++)
        {
            if (Vector2.Distance(position, pipes[i].position) < database.minPipeDistance)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Patlayan borulardan gelir biriktirir.
    /// </summary>
    private void AccumulateIncome()
    {
        float incomeThisFrame = 0f;

        for (int i = 0; i < pipes.Count; i++)
        {
            if (pipes[i].isBurst)
            {
                incomeThisFrame += pipes[i].pipeType.incomePerSecond * Time.unscaledDeltaTime;
            }
        }

        if (incomeThisFrame > 0f)
        {
            accumulatedIncome += incomeThisFrame;
            OnIncomeUpdate?.Invoke(accumulatedIncome);
        }
    }

    /// <summary>
    /// Minigame'i bitirir. Toplam geliri wealth'e ekler, cooldown başlatır.
    /// </summary>
    private void FinishGame(PipeHuntEndReason reason)
    {
        currentState = PipeHuntState.Finished;

        //son geliri hesapla (bu frame'deki kalan)
        int burstCount = 0;
        for (int i = 0; i < pipes.Count; i++)
        {
            if (pipes[i].isBurst) burstCount++;
        }

        //wealth'e ekle
        if (GameStatManager.Instance != null && accumulatedIncome > 0f)
            GameStatManager.Instance.AddWealth(accumulatedIncome);

        //cooldown başlat
        if (MinigameManager.Instance != null)
            MinigameManager.Instance.StartCooldown(minigameData);

        //sonucu bildir
        PipeHuntResult result = new PipeHuntResult();
        result.totalIncome = accumulatedIncome;
        result.burstPipeCount = burstCount;
        result.totalPipeCount = pipes.Count;
        result.endReason = reason;
        result.remainingTime = gameTimer;

        OnGameFinished?.Invoke(result);

        //ana oyunu devam ettir
        if (GameManager.Instance != null)
            GameManager.Instance.ResumeGame();

        //state'i sıfırla
        currentState = PipeHuntState.Idle;
        pipes.Clear();
    }

    private PipeInstance GetPipeById(int id)
    {
        for (int i = 0; i < pipes.Count; i++)
        {
            if (pipes[i].id == id) return pipes[i];
        }
        return null;
    }

    // ==================== GETTER'LAR ====================

    public PipeHuntState GetCurrentState()
    {
        return currentState;
    }

    public List<PipeInstance> GetPipes()
    {
        return pipes;
    }

    public int GetPickaxeRemainingDurability()
    {
        return pickaxeRemainingDurability;
    }

    public float GetAccumulatedIncome()
    {
        return accumulatedIncome;
    }

    public float GetRemainingTime()
    {
        return gameTimer;
    }
}

public enum PipeHuntState
{
    Idle,       //minigame aktif değil
    Active,     //oyun devam ediyor
    Finished    //oyun bitti (geçiş anı)
}

public enum PipeHuntEndReason
{
    TimeUp,         //süre doldu
    PickaxeBroken   //kazma kırıldı
}

[System.Serializable]
public class PipeHuntResult
{
    public float totalIncome;       //toplam kazanılan gelir
    public int burstPipeCount;      //patlayan boru sayısı
    public int totalPipeCount;      //toplam boru sayısı
    public PipeHuntEndReason endReason; //bitiş sebebi
    public float remainingTime;     //kalan süre (kazma kırıldıysa > 0)
}
