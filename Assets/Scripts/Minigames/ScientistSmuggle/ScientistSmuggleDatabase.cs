using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Minigames/ScientistSmuggle/Database")]
public class ScientistSmuggleDatabase : ScriptableObject
{
    public List<ScientistSmuggleEvent> offerEvents;        //teklif eventleri (eventType = Offer)
    public List<ScientistSmuggleEvent> processEvents;      //süreç eventleri (eventType = Process)
    public List<ScientistSmuggleEvent> postProcessEvents;  //operasyon sonrası musallat eventleri (eventType = PostProcess)
}
