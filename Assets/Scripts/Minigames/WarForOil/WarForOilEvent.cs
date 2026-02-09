using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Minigames/WarForOil/Event")]
public class WarForOilEvent : ScriptableObject
{
    public string id;
    public string displayName;
    [TextArea(2, 8)] public string description;

    public float decisionTime = 10f; //karar süresi (saniye)
    public List<WarForOilEventChoice> choices;
    public int defaultChoiceIndex = -1; //süre dolunca otomatik seçilecek seçenek (-1 = ilk seçenek)
}

[System.Serializable]
public class WarForOilEventChoice
{
    public string displayName;
    [TextArea(2, 4)] public string description;
    public float supportModifier; //destek stat'ını etkiler (pozitif = ülkeyi destekle)
    public float suspicionModifier; //şüphe etkisi
    public int costModifier; //maliyet etkisi
}
