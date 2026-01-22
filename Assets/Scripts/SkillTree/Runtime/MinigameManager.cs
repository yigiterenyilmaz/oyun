using System;
using System.Collections.Generic;
using UnityEngine;

public class MinigameManager : MonoBehaviour
{
    public static MinigameManager Instance { get; private set; }

    private HashSet<MiniGameData> unlockedMinigames = new HashSet<MiniGameData>();

    public static event Action<MiniGameData> OnMinigameUnlocked;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void UnlockMinigame(MiniGameData minigame)
    {
        if (unlockedMinigames.Contains(minigame))
            return;

        unlockedMinigames.Add(minigame);
        OnMinigameUnlocked?.Invoke(minigame);
    }

    public bool IsMinigameUnlocked(MiniGameData minigame)
    {
        return unlockedMinigames.Contains(minigame);
    }

    public List<MiniGameData> GetUnlockedMinigames()
    {
        return new List<MiniGameData>(unlockedMinigames);
    }
}
