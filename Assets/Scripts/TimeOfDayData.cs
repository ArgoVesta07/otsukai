using System;
using UnityEngine;

// The parts of a day (Morning, Afternoon, Evening, Night) and the hour each one starts.
// Made as an asset: right-click in the Project window > Create > Otsukai > Time Of Day.
// Used by the clock in Classic and Challenge mode.
[CreateAssetMenu(menuName = "Otsukai/Time Of Day", fileName = "TimeOfDay")]
public class TimeOfDayData : ScriptableObject
{
    [Serializable]
    public class Phase
    {
        [GameText] public string name = "Morning";

        [Tooltip("Hour this phase starts (0-24). 6.5 = 06:30. Keep the list in order from early to late.")]
        public float startHour = 6f;

        [Tooltip("Night: running errands fail when it starts, and NPCs don't give new errands. (Classic mode)")]
        public bool isNight;

        public Phase() { }

        public Phase(string name, float startHour, bool isNight = false)
        {
            this.name = name;
            this.startHour = startHour;
            this.isNight = isNight;
        }
    }

    public Phase[] phases =
    {
        new Phase("Morning", 6f),
        new Phase("Afternoon", 11f),
        new Phase("Evening", 15f),
        new Phase("Night", 18f, true),
    };

    // Which phase a clock time is in (minutes since midnight). Before the first phase = the first phase.
    public int IndexAt(float minutes)
    {
        if (phases == null || phases.Length == 0) return -1;

        float hour = minutes / 60f;
        int index = 0;
        for (int i = 0; i < phases.Length; i++)
        {
            if (phases[i] != null && hour >= phases[i].startHour) index = i;
        }
        return index;
    }

    public Phase Get(int index)
    {
        if (phases == null || index < 0 || index >= phases.Length) return null;
        return phases[index];
    }

    // Translated name of the phase at this clock time ("Afternoon" / "昼")
    public string NameAt(float minutes)
    {
        Phase phase = Get(IndexAt(minutes));
        return phase != null ? Loc.T(phase.name) : "";
    }
}
