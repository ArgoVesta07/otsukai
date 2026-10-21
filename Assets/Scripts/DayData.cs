using System;
using UnityEngine;

public enum DayPeriod
{
    Morning,
    Afternoon,
    Night
}

// One day of the story, made as an asset: right-click in the Project window >
// Create > Otsukai > Day. Each period is one round with its own timer and errands.
[CreateAssetMenu(menuName = "Otsukai/Day", fileName = "Day")]
public class DayData : ScriptableObject
{
    public PeriodData[] periods;
}

[Serializable]
public class PeriodData
{
    public string note;   // free note for yourself, not used by the game

    public DayPeriod period = DayPeriod.Morning;

    [Tooltip("Seconds for this round. 0 = no timer (ends when all errands are done).")]
    public float timeLimit = 180f;

    [Tooltip("Errands that belong to this round. The round ends when all of them are done or failed.")]
    public PeriodErrand[] errands;

    [Tooltip("Incidents that happen during this round.")]
    public ScheduledIncident[] incidents;
}

[Serializable]
public class ScheduledIncident
{
    public IncidentData incident;

    [Tooltip("Seconds after the round starts (round time: it pauses during dialogue and loading).")]
    public float startAt = 20f;
}

[Serializable]
public class PeriodErrand
{
    public ErrandData errand;

    [Tooltip("Ticked = starts by itself when the round begins. Off = an NPC has to give it.")]
    public bool autoStart;
}
