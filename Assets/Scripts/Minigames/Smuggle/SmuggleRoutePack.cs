using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Minigames/Smuggle/Route Pack")]
public class SmuggleRoutePack : ScriptableObject
{
    public string id;
    public string displayName;
    public List<SmuggleRoute> routes; //bu paketteki 4 rota
}
