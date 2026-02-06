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

    //events
    public static event Action OnCountryDataReady;

    //biome → region eşleştirmesi
    //MapGenerator: 1=Forest, 2=Desert, 3=Mountains, 4=Plains
    //Tasarım:      Tarım,    BosArazi, Sanayi,     Sehir
    private static readonly Dictionary<int, RegionType> biomeToRegion = new Dictionary<int, RegionType>
    {
        { 1, RegionType.Tarim },
        { 2, RegionType.BosArazi },
        { 3, RegionType.Sanayi },
        { 4, RegionType.Sehir }
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
        OnCountryDataReady?.Invoke();
    }

    //MapGenerator'dan biome oranlarını çekip region oranlarına dönüştürür
    private void PullRegionRatios()
    {
        regionRatios[RegionType.Tarim] = mapGenerator.ForestRatio;
        regionRatios[RegionType.BosArazi] = mapGenerator.DesertRatio;
        regionRatios[RegionType.Sanayi] = mapGenerator.MountainRatio;
        regionRatios[RegionType.Sehir] = mapGenerator.PlainsRatio;
    }

    //belirli bölge tipinin oranını döner
    public float GetRegionRatio(RegionType regionType)
    {
        return regionRatios.TryGetValue(regionType, out float ratio) ? ratio : 0f;
    }

    //en baskın bölge tipini döner
    public RegionType GetDominantRegion()
    {
        RegionType dominant = RegionType.BosArazi;
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

    //tüm oranları döner
    public Dictionary<RegionType, float> GetAllRegionRatios()
    {
        return new Dictionary<RegionType, float>(regionRatios);
    }
}
