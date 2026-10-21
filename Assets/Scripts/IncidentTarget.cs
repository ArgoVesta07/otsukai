using UnityEngine;

// Something in the scene that changes while an incident is active.
// Example: road block -> "Show While Active" = a barrier object with a collider.
// Example: shop busy  -> "Hide While Active" = the shelf's trigger, "Show While Active" = a "Closed" sign.
// Put this on an object that stays active (not on one of the objects it switches).
public class IncidentTarget : MonoBehaviour
{
    [Tooltip("Must match the Incident Id of the IncidentData asset.")]
    public string incidentId = "RoadClosed";

    public GameObject[] showWhileActive;
    public GameObject[] hideWhileActive;

    void OnEnable()
    {
        IncidentManager.Changed += Apply;
        Apply();   // also correct after entering a scene while the incident is already running
    }

    void OnDisable()
    {
        IncidentManager.Changed -= Apply;
    }

    void Apply()
    {
        bool on = IncidentManager.IsActive(incidentId);

        if (showWhileActive != null)
        {
            foreach (GameObject go in showWhileActive)
                if (go != null) go.SetActive(on);
        }

        if (hideWhileActive != null)
        {
            foreach (GameObject go in hideWhileActive)
                if (go != null) go.SetActive(!on);
        }
    }
}
