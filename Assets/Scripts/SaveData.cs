using System;
using System.Collections.Generic;
using UnityEngine;

// Everything written to the save file. Add new fields at the bottom and raise SaveSystem.CurrentVersion
// when the format changes, so old saves can still be recognised.
[Serializable]
public class SaveData
{
    public int version;
    public string savedAt;              // date and time, for a future "Continue" screen

    // Where
    public string sceneName;
    public Vector3 playerPosition;

    // Errands
    public List<string> completedErrands = new List<string>();
    public List<string> failedErrands = new List<string>();
    public List<SavedErrand> activeErrands = new List<SavedErrand>();

    // Inventory and flags
    public List<SavedPair> items = new List<SavedPair>();
    public List<SavedPair> flags = new List<SavedPair>();

    // Story time (version 2)
    public int dayIndex;
    public int periodIndex;
    public float periodTimeLeft;
    public bool storyFinished;
    public bool betweenRounds;   // saved during the break after a round
    public List<PeriodResult> periodResults = new List<PeriodResult>();
}

// How one story round went
[Serializable]
public class PeriodResult
{
    public int day;
    public string period;
    public int completed;
    public int total;
    public float timeLeft;
    public PeriodRating rating;
}

[Serializable]
public class SavedErrand
{
    public string errandId;
    public int stepIndex;
    public float timeLeft;
}

// JsonUtility can't save dictionaries, so key/value pairs are stored as a list
[Serializable]
public class SavedPair
{
    public string key;
    public int value;

    public SavedPair(string key, int value)
    {
        this.key = key;
        this.value = value;
    }
}