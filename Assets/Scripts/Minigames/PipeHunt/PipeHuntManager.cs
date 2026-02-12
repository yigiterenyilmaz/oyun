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
    private HuntTool currentTool;
    private int toolRemainingDurability;
    private float gameDuration;
    private float gameTimer;
    private float accumulatedIncome;

    //events — UI dinleyecek
    public static event Action<List<PipeInstance>, float, HuntTool> OnGameStarted; //borular, süre, seçilen alet
    public static event Action<float> OnTimerUpdate; //kalan süre
    public static event Action<PipeInstance, int> OnPipeHit; //vurulan boru, kalan boru dayanıklılığı
    public static event Action<PipeInstance> OnPipeBurst; //patlayan boru
    public static event Action<int> OnEmptyHit; //boş zemine vuruldu, kalan alet dayanıklılığı
    public static event Action<int> OnToolDamaged; //alet hasar aldı, kalan dayanıklılık
    public static event Action OnToolBroken; //alet kırıldı
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
    /// Minigame'i seçilen aletle başlatır. Alet maliyeti ödenir, süre stealth'e göre hesaplanır.
    /// </summary>
    public void StartGame(HuntTool tool)
    {
        if (currentState != PipeHuntState.Idle) return;
        if (tool == null) return;

        //minigame açık mı ve cooldown'da mı kontrol et
        if (MinigameManager.Instance != null)
        {
            if (!MinigameManager.Instance.IsMinigameUnlocked(minigameData)) return;
            if (MinigameManager.Instance.IsOnCooldown(minigameData)) return;
        }

        //alet maliyetini öde
        if (GameStatManager.Instance != null)
        {
            if (!GameStatManager.Instance.HasEnoughWealth(tool.cost)) return;
            GameStatManager.Instance.TrySpendWealth(tool.cost);
        }

        //seçilen aleti kaydet
        currentTool = tool;

        //boruları yerleştir
        GeneratePipes();

        //alet dayanıklılığı ve süre hesabı
        toolRemainingDurability = tool.durability;
        gameDuration = Mathf.Lerp(database.minGameDuration, database.maxGameDuration, tool.stealth);
        gameTimer = gameDuration;
        accumulatedIncome = 0f;

        currentState = PipeHuntState.Active;

        //ana oyunu duraklat — minigame kendi zamanını unscaled ile yönetir
        if (GameManager.Instance != null)
            GameManager.Instance.PauseGame();

        OnGameStarted?.Invoke(pipes, gameDuration, currentTool);
    }

    /// <summary>
    /// Oyuncu bir boruya vurdu. UI boru id'sini gönderir.
    /// </summary>
    public void HitPipe(int pipeId)
    {
        if (currentState != PipeHuntState.Active) return;

        PipeInstance pipe = GetPipeById(pipeId);
        if (pipe == null || pipe.isBurst) return;

        //boruya hasar ver (aletin vuruş hasarı kadar)
        pipe.remainingDurability -= currentTool.damagePerHit;

        //alete hasar ver (her vuruş 1 dayanıklılık düşürür)
        toolRemainingDurability--;
        OnToolDamaged?.Invoke(toolRemainingDurability);

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

        //alet kırıldı mı
        if (toolRemainingDurability <= 0)
        {
            toolRemainingDurability = 0;
            OnToolBroken?.Invoke();
            FinishGame(PipeHuntEndReason.ToolBroken);
        }
    }

    /// <summary>
    /// Oyuncu boş zemine vurdu. Alet aşınır ama boru hasar almaz.
    /// </summary>
    public void HitEmpty()
    {
        if (currentState != PipeHuntState.Active) return;

        //alete hasar ver (her vuruş 1 dayanıklılık düşürür)
        toolRemainingDurability--;
        OnToolDamaged?.Invoke(toolRemainingDurability);
        OnEmptyHit?.Invoke(toolRemainingDurability);

        //alet kırıldı mı
        if (toolRemainingDurability <= 0)
        {
            toolRemainingDurability = 0;
            OnToolBroken?.Invoke();
            FinishGame(PipeHuntEndReason.ToolBroken);
        }
    }

    // ==================== UI İÇİN GETTER'LAR ====================

    /// <summary>
    /// Oyuncunun seçebileceği aletlerin listesini döner. UI alet seçim ekranında kullanır.
    /// </summary>
    public List<HuntTool> GetAvailableTools()
    {
        return database.tools;
    }

    /// <summary>
    /// Belirli bir alet için hesaplanan oyun süresini döner. UI'da alet bilgisi gösterirken kullanılır.
    /// </summary>
    public float GetToolDuration(HuntTool tool)
    {
        if (tool == null) return 0f;
        return Mathf.Lerp(database.minGameDuration, database.maxGameDuration, tool.stealth);
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

        //patlayan boru sayısı
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
        result.toolUsed = currentTool;
        result.toolCostPaid = currentTool != null ? currentTool.cost : 0;

        OnGameFinished?.Invoke(result);

        //ana oyunu devam ettir
        if (GameManager.Instance != null)
            GameManager.Instance.ResumeGame();

        //state'i sıfırla
        currentState = PipeHuntState.Idle;
        currentTool = null;
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

    public int GetToolRemainingDurability()
    {
        return toolRemainingDurability;
    }

    public float GetAccumulatedIncome()
    {
        return accumulatedIncome;
    }

    public float GetRemainingTime()
    {
        return gameTimer;
    }

    public HuntTool GetCurrentTool()
    {
        return currentTool;
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
    TimeUp,       //süre doldu
    ToolBroken    //alet kırıldı
}

[System.Serializable]
public class PipeHuntResult
{
    public float totalIncome;       //toplam kazanılan gelir
    public int burstPipeCount;      //patlayan boru sayısı
    public int totalPipeCount;      //toplam boru sayısı
    public PipeHuntEndReason endReason; //bitiş sebebi
    public float remainingTime;     //kalan süre (alet kırıldıysa > 0)
    public HuntTool toolUsed;       //kullanılan alet
    public int toolCostPaid;        //ödenen alet maliyeti
}
