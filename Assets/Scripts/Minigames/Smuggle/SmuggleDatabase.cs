using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Smuggle/Database")]
public class SmuggleDatabase : ScriptableObject
{
    public List<SmuggleRoutePack> routePacks; //tüm rota paketleri havuzu
    public List<SmuggleCourier> couriers; //tüm kurye havuzu
}
