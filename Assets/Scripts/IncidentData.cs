using UnityEngine;
using UnityEngine.Serialization;

// One kind of unexpected event (road closed, shop busy, ...), made as an asset:
// right-click in the Project window > Create > Otsukai > Incident.
// What it does in the world is set up with IncidentTarget objects in the scene that use the same Incident Id.
[CreateAssetMenu(menuName = "Otsukai/Incident", fileName = "NewIncident")]
public class IncidentData : ScriptableObject
{
    [Tooltip("Unique id. IncidentTarget objects with this id react when it starts and ends.")]
    public string incidentId = "NewIncident";

    [Tooltip("Seconds it lasts. 0 = until the round ends.")]
    public float duration = 20f;

    [Header("Messages (pop-up, empty = none)")]
    [GameText] public string startMessage = "Something happened!";
    [GameText] public string endMessage = "";

    [Header("Challenge mode (random days)")]
    [Tooltip("Can the challenge mode pick this incident at random?")]
    [FormerlySerializedAs("allowInClassic")] public bool allowInChallenge = true;

    [Tooltip("Higher = picked more often.")]
    public float weight = 1f;

    [Tooltip("Earliest day it may appear in challenge mode.")]
    public int minDay = 1;
}
