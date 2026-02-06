using UnityEngine;

[CreateAssetMenu(menuName = "Smuggle/Courier")]
public class SmuggleCourier : ScriptableObject
{
    public string id;
    public string displayName;
    [TextArea(2, 8)] public string description; //kuryenin açıklaması

    [Range(0f, 100f)] public float reliability; //genel başarı oranını etkiler
    [Range(0f, 100f)] public float speed; //operasyon süresini kısaltır
    public int cost; //kurye kiralama maliyeti
    [Range(0f, 1f)] public float betrayalChance; //ihanet olasılığı
    [Range(0f, 100f)] public float discretion; //ihbar/fark edilme olasılığını düşürür
}
