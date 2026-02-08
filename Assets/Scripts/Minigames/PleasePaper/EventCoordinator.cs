/// <summary>
/// Farklı event sistemlerinin (RandomEvent, PleasePaper, Smuggle vb.) aynı anda
/// event göstermesini engelleyen paylaşımlı slot yöneticisi.
/// </summary>
public static class EventCoordinator
{
    private static string currentOwner = null;

    /// <summary>
    /// Event slot'unu almaya çalışır. Slot boşsa alır ve true döner.
    /// Zaten aynı owner tarafından alınmışsa da true döner.
    /// </summary>
    public static bool TryAcquireEventSlot(string owner)
    {
        if (currentOwner == null)
        {
            currentOwner = owner;
            return true;
        }

        //aynı owner zaten slot'u tutuyorsa tekrar izin ver
        return currentOwner == owner;
    }

    /// <summary>
    /// Event slot'unu serbest bırakır. Sadece mevcut owner bırakabilir.
    /// </summary>
    public static void ReleaseEventSlot(string owner)
    {
        if (currentOwner == owner)
        {
            currentOwner = null;
        }
    }

    /// <summary>
    /// Event slot'u şu an dolu mu kontrol eder.
    /// </summary>
    public static bool IsEventSlotOccupied()
    {
        return currentOwner != null;
    }
}
