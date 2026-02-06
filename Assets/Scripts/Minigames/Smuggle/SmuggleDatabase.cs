using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Minigames/Smuggle/Database")]
public class SmuggleDatabase : ScriptableObject
{
    public List<SmuggleRoutePack> routePacks; //tüm rota paketleri havuzu
    public List<SmuggleCourier> couriers; //tüm kurye havuzu
    public List<SmuggleEvent> events; //operasyon sırasında tetiklenebilecek event havuzu
}
