using UnityEngine;

// The story mode: the days in order. Right-click in the Project window > Create > Otsukai > Story.
[CreateAssetMenu(menuName = "Otsukai/Story", fileName = "Story")]
public class StoryData : ScriptableObject
{
    [Tooltip("Element 0 = Day 1, Element 1 = Day 2, ...")]
    public DayData[] days;
}
