using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Minigames/PipeHunt/Database")]
public class PipeHuntDatabase : ScriptableObject
{
    [Header("Borular")]
    public List<PipeType> pipeTypes; //oyunda çıkabilecek boru tipleri
    public int pipeCount = 8; //her oyunda zemine dağıtılan boru sayısı

    [Header("Kazma")]
    public int pickaxeDurability = 100; //kazmanın toplam dayanıklılığı
    public int damagePerHit = 10; //her vuruşta kazmanın boruya verdiği hasar

    [Header("Süre")]
    public float gameDuration = 30f; //minigame süresi (saniye)

    [Header("Boru Yerleşim")]
    public float minPipeDistance = 0.1f; //borular arası minimum mesafe (normalize 0-1)
}
