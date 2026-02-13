using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Minigames/ScientistSmuggle/Event")]
public class ScientistSmuggleEvent : ScriptableObject
{
    public string id;
    public string displayName;
    [TextArea(2, 8)] public string description; //event açıklaması (oyuncuya gösterilecek)
    public ScientistSmuggleEventType eventType; //bu event hangi gruba ait

    //--- Offer alanları (sadece eventType == Offer iken anlamlı) ---
    public int baseReward; //başarılı olursa taban kazanç

    //--- Süreç event havuzu (sadece Offer için, boşsa database'den alınır) ---
    public List<ScientistSmuggleEvent> processEvents; //bu offer'a özel süreç eventleri

    //--- Karar süresi ---
    public float decisionTime = 10f; //oyuncunun karar vermesi için verilen süre (saniye)

    //--- Seçenekler (Process eventleri için) ---
    public List<ScientistSmuggleEventChoice> choices; //oyuncunun seçebileceği seçenekler
    public int defaultChoiceIndex = -1; //süre dolunca otomatik seçilecek seçenek (-1 = ilk seçenek)
}

public enum ScientistSmuggleEventType
{
    Offer,   //teklif eventi — ülke bilgisi, baseReward taşır
    Process  //süreç eventi — choices ile controlStat etkiler
}

[System.Serializable]
public class ScientistSmuggleEventChoice
{
    public string displayName;
    [TextArea(2, 4)] public string description;
    public float controlStatModifier; //controlStat değişimi (+ veya -)
    public float suspicionModifier;   //şüphe değişimi
    public int costModifier;          //ek maliyet/zarar
}
