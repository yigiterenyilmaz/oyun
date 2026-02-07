using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Minigames/Smuggle/Event")]
public class SmuggleEvent : ScriptableObject
{
    public string id;
    public string displayName;
    [TextArea(2, 8)] public string description; //event açıklaması (oyuncuya gösterilecek)
    public List<SmuggleEventChoice> choices; //oyuncunun seçebileceği seçenekler
}

[System.Serializable]
public class SmuggleEventChoice
{
    public string displayName;
    [TextArea(2, 4)] public string description;
    public float successModifier; //başarı şansına etki (+ veya -)
    public float suspicionModifier; //şüphe değişimi
    public int costModifier; //ekstra maliyet (rüşvet, kayıp vs.)
    public List<SmuggleEvent> nextEventPool; //bu seçim yapılırsa sonraki eventler bu havuzdan gelir (boşsa zincir biter)
}
