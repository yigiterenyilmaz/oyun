using System;
using System.Collections.Generic;
using UnityEngine;

public class RandomEventManager : MonoBehaviour
{
    public static RandomEventManager Instance { get; private set; }

    public int currentGamePhase = 0;

    private HashSet<Event> unlockedEvents = new HashSet<Event>();
    private HashSet<Event> triggeredEvents = new HashSet<Event>();

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

    public List<Event> GetEligibleEvents()
    {
        List<Event> eligibleEvents = new List<Event>();

        foreach (Event evt in unlockedEvents)
        {
            if (IsEventEligible(evt))
            {
                eligibleEvents.Add(evt);
            }
        }

        return eligibleEvents;
    }

    public bool IsEventEligible(Event evt)
    {
        if (!evt.isRepeatable && triggeredEvents.Contains(evt))
            return false;

        if (currentGamePhase < evt.minGamePhase || currentGamePhase > evt.maxGamePhase)
            return false;

        if (evt.requiredSkills != null && evt.requiredSkills.Count > 0)
        {
            foreach (Skill skill in evt.requiredSkills)
            {
                if (!SkillTreeManager.Instance.IsUnlocked(skill.id))
                    return false;
            }
        }

        if (evt.statConditions != null && evt.statConditions.Count > 0)
        {
            foreach (StatCondition condition in evt.statConditions)
            {
                if (!condition.IsMet())
                    return false;
            }
        }

        return true;
    }

    public Event GetRandomEvent()
    {
        List<Event> eligibleEvents = GetEligibleEvents();

        if (eligibleEvents.Count == 0)
            return null;

        float totalWeight = 0f;
        foreach (Event evt in eligibleEvents)
        {
            totalWeight += evt.weight;
        }

        float randomValue = UnityEngine.Random.Range(0f, totalWeight);
        float currentWeight = 0f;

        foreach (Event evt in eligibleEvents)
        {
            currentWeight += evt.weight;
            if (randomValue <= currentWeight)
            {
                return evt;
            }
        }

        return eligibleEvents[eligibleEvents.Count - 1];
    }

    public void TriggerRandomEvent()
    {
        Event evt = GetRandomEvent();
        if (evt == null)
            return;

        triggeredEvents.Add(evt);
        OnEventTriggered?.Invoke(evt);
    }

    public void SelectChoice(EventChoice choice)
    {
        if (choice.effects == null)
            return;

        foreach (SkillEffect effect in choice.effects)
        {
            effect.Apply();
        }
    }

    public void SetGamePhase(int phase)
    {
        currentGamePhase = phase;
    }
}
