using UnityEngine;

// Placeholder for a building of the town layout (made by Otsukai > Town > Import Layout CSV).
// Draws its footprint in the Scene view only, so the art can be placed on top. Root = bottom centre.
public class TownBuilding : MonoBehaviour
{
    public string number;
    public Vector2 size = new Vector2(5f, 5f);   // tiles
    public bool enterable;

    void OnDrawGizmos()
    {
        Vector3 centre = transform.position + new Vector3(0f, size.y / 2f, 0f);
        Gizmos.color = enterable ? new Color(0.9f, 0.5f, 0.3f, 0.8f) : new Color(0.5f, 0.6f, 0.9f, 0.8f);
        Gizmos.DrawWireCube(centre, new Vector3(size.x, size.y, 0f));
#if UNITY_EDITOR
        UnityEditor.Handles.Label(centre, name);   // the object name already starts with the number
#endif
    }
}
