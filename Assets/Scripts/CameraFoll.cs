using UnityEngine;

// Follows the target (the player). Optional map bounds keep the camera from showing past the map edge;
// when the map is smaller than the view (e.g. a small room), the camera stays centered on it.
// Set the bounds per scene in the Inspector; the yellow box in the Scene view shows them.
public class CameraFollow : MonoBehaviour
{
    public Transform target;

    [Header("Map bounds")]
    [SerializeField] bool useBounds = true;
    [SerializeField] Vector2 boundsCenter = Vector2.zero;
    [SerializeField] Vector2 boundsSize = new Vector2(64f, 40f);

    Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 pos = new Vector3(target.position.x, target.position.y, -10f);
        if (useBounds && cam != null) pos = Clamp(pos);
        transform.position = pos;
    }

    Vector3 Clamp(Vector3 pos)
    {
        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;

        pos.x = ClampAxis(pos.x, boundsCenter.x, boundsSize.x * 0.5f, halfWidth);
        pos.y = ClampAxis(pos.y, boundsCenter.y, boundsSize.y * 0.5f, halfHeight);
        return pos;
    }

    static float ClampAxis(float value, float center, float halfMap, float halfView)
    {
        if (halfMap <= halfView) return center;   // map smaller than the view: keep it centered
        return Mathf.Clamp(value, center - halfMap + halfView, center + halfMap - halfView);
    }

    void OnDrawGizmosSelected()
    {
        if (!useBounds) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(boundsCenter, boundsSize);
    }
}