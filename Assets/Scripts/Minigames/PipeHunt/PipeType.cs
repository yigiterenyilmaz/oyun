using UnityEngine;

[CreateAssetMenu(menuName = "Minigames/PipeHunt/PipeType")]
public class PipeType : ScriptableObject
{
    public string id;
    public string displayName;

    public int durability; //borunun dayanıklılığı (kazma hasarı bu değerden düşer)
    public float incomePerSecond; //boru patladıktan sonra saniyede kazandırdığı gelir
}
