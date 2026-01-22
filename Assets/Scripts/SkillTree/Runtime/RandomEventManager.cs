using System;
using System.Collections.Generic;
using UnityEngine;

public class RandomEventManager : MonoBehaviour
{
    public static RandomEventManager Instance { get; private set; }

    private HashSet<Event> unlockedEvents = new HashSet<Event>();

    public static event Action<Event> OnEventUnlocked;
    public static event Action<Event> OnEventTriggered;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void UnlockEvent(Event evt)
    {
        if (unlockedEvents.Contains(evt))
            return;

        unlockedEvents.Add(evt);
        OnEventUnlocked?.Invoke(evt);
    }

    public bool IsEventUnlocked(Event evt)
    {
        return unlockedEvents.Contains(evt);
    }

    public List<Event> GetUnlockedEvents()
    {
        return new List<Event>(unlockedEvents);
    }

    public Event GetRandomEvent()
    {
        if (unlockedEvents.Count == 0)
            return null;

        List<Event> eventList = new List<Event>(unlockedEvents);
        int randomIndex = UnityEngine.Random.Range(0, eventList.Count);
        return eventList[randomIndex];
    }

    public void TriggerRandomEvent()
    {
        Event evt = GetRandomEvent();
        if (evt == null)
            return;

        OnEventTriggered?.Invoke(evt);

        if (evt.effects != null)
        {
            foreach (SkillEffect effect in evt.effects)
            {
                effect.Apply();
            }
        }
    }
}
