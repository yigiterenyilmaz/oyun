using System;
using System.Collections.Generic;
using UnityEngine;

public class CountryData : MonoBehaviour
{
    public static CountryData Instance { get; private set; }

    [Header("Map Generator Reference")]
    public MapGenerator mapGenerator;

    //bölge oranları (0-1 arası, toplam 1.0)
    private Dictionary<RegionType, float> regionRatios = new Dictionary<RegionType, float>();

    //ülke özellikleri (0-100, her oyun başında random üretilir)
    private float corruptionIndex;
    private float educationIndex;
    private float climateFertility;
    private float naturalResourceWealth;

    //events
    public static event Action OnCountryDataReady;

    //biome → region eşleştirmesi
    //MapGenerator: 1=Forest, 2=Desert, 3=Mountains, 4=Plains
    //Oyun:         Agricultural, Barren, Industrial, Urban
    private static readonly Dictionary<int, RegionType> biomeToRegion = new Dictionary<int, RegionType>
    {
        { 1, RegionType.Agricultural },
        { 2, RegionType.Barren },
        { 3, RegionType.Industrial },
        { 4, RegionType.Urban }
    };

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        if (mapGenerator != null)
            mapGenerator.OnMapGenerated += HandleMapGenerated;
    }

    private void OnDisable()
    {
        if (mapGenerator != null)
            mapGenerator.OnMapGenerated -= HandleMapGenerated;
    }

    private void HandleMapGenerated()
    {
        PullRegionRatios();
        GenerateCountryProperties();
        LogCountryData();
        OnCountryDataReady?.Invoke();
    }

    //ülke özelliklerini random üret (ortaya yakın değerler daha olası, uçlar nadir ama mümkün)
    private void GenerateCountryProperties()
    {
        corruptionIndex = GenerateWeightedRandom();
        educationIndex = GenerateWeightedRandom();
        climateFertility = GenerateWeightedRandom();
        naturalResourceWealth = GenerateWeightedRandom();
    }

    //%90 ihtimalle 14-86 arası (uniform), %10 ihtimalle uçlar (0-13 veya 87-100)
    private float GenerateWeightedRandom()
    {
        float roll = UnityEngine.Random.Range(0f, 1f);

        if (roll < 0.9f)
        {
            //normal bölge: 14-86 arası eşit olasılık
            return UnityEngine.Random.Range(14f, 87f);
        }
        else
        {
            //uç bölge: 0-13 veya 87-100
            if (UnityEngine.Random.Range(0f, 1f) < 0.5f)
                return UnityEngine.Random.Range(0f, 14f);
            else
                return UnityEngine.Random.Range(87f, 101f);
        }
    }

    //MapGenerator'dan biome oranlarını çekip RegionType'a dönüştürür
    private void PullRegionRatios()
    {
        regionRatios[RegionType.Agricultural] = mapGenerator.ForestRatio;
        regionRatios[RegionType.Barren] = mapGenerator.DesertRatio;
        regionRatios[RegionType.Industrial] = mapGenerator.MountainRatio;
        regionRatios[RegionType.Urban] = mapGenerator.PlainsRatio;
    }

    #region Region Getters

    public float GetRegionRatio(RegionType regionType)
    {
        return regionRatios.TryGetValue(regionType, out float ratio) ? ratio : 0f;
    }

    public RegionType GetDominantRegion()
    {
        RegionType dominant = RegionType.Barren;
        float maxRatio = 0f;

        foreach (var pair in regionRatios)
        {
            if (pair.Value > maxRatio)
            {
                maxRatio = pair.Value;
                dominant = pair.Key;
            }
        }

        return dominant;
    }

    public Dictionary<RegionType, float> GetAllRegionRatios()
    {
        return new Dictionary<RegionType, float>(regionRatios);
    }

    #endregion

    private void LogCountryData()
    {
        Debug.Log("=== ÜLKE VERİSİ ===\n" +
            $"[Bölge Oranları]\n" +
            $"  Sanayi:    %{GetRegionRatio(RegionType.Industrial) * 100f:F1}\n" +
            $"  Şehir:     %{GetRegionRatio(RegionType.Urban) * 100f:F1}\n" +
            $"  Tarım:     %{GetRegionRatio(RegionType.Agricultural) * 100f:F1}\n" +
            $"  Boş Arazi: %{GetRegionRatio(RegionType.Barren) * 100f:F1}\n" +
            $"[Ülke Özellikleri]\n" +
            $"  Yozlaşma:       {corruptionIndex:F0}\n" +
            $"  Eğitim:         {educationIndex:F0}\n" +
            $"  İklim:          {climateFertility:F0}\n" +
            $"  Doğal Kaynak:   {naturalResourceWealth:F0}");
    }

    #region Country Property Getters

    public float CorruptionIndex => corruptionIndex;
    public float EducationIndex => educationIndex;
    public float ClimateFertility => climateFertility;
    public float NaturalResourceWealth => naturalResourceWealth;

    #endregion
}
