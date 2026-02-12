using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Minigames/PipeHunt/Database")]
public class PipeHuntDatabase : ScriptableObject
{
    [Header("Borular")]
    public List<PipeType> pipeTypes; //oyunda çıkabilecek boru tipleri
    public int pipeCount = 8; //her oyunda zemine dağıtılan boru sayısı

    [Header("Aletler")]
    public List<HuntTool> tools; //oyuncunun seçebileceği alet tipleri

    [Header("Süre")]
    public float minGameDuration = 15f; //stealth=0 → en kısa süre (saniye)
    public float maxGameDuration = 45f; //stealth=1 → en uzun süre (saniye)

    [Header("Boru Yerleşim")]
    public float minPipeDistance = 0.1f; //borular arası minimum mesafe (normalize 0-1)
}
