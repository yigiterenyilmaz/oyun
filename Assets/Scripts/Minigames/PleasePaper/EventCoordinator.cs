using UnityEngine;

/// <summary>
/// Farklı event sistemlerinin (RandomEvent, PleasePaper, Smuggle vb.) aynı anda
/// event göstermesini engelleyen cooldown sistemi.
/// Tam kilitleme yapmaz — sadece iki event arasında minimum süre bırakır.
/// </summary>
public static class EventCoordinator
{
    private static float lastEventTime = -999f;
    private static float cooldownDuration = 2f; //iki event arası minimum süre (saniye)

    /// <summary>
    /// Event gösterilebilir mi kontrol eder.
    /// Son event'ten bu yana yeterli süre geçtiyse true döner.
    /// </summary>
    public static bool CanShowEvent()
    {
        return Time.time - lastEventTime >= cooldownDuration;
    }

    /// <summary>
    /// Event gösterildi olarak işaretle. Cooldown sayacını sıfırlar.
    /// Her event popup'ı gösterildiğinde çağrılmalı.
    /// </summary>
    public static void MarkEventShown()
    {
        lastEventTime = Time.time;
    }

    /// <summary>
    /// Cooldown süresini değiştirir (varsayılan 2 saniye).
    /// </summary>
    public static void SetCooldownDuration(float duration)
    {
        cooldownDuration = Mathf.Max(0f, duration);
    }
}
