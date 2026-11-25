using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;

// Otsukai > Tools > Convert To Root + Visual: turns the selected props into the project's object structure
// (see docs/otsukai-guide.md, section 3):
//   root   = scale 1, at the bottom-centre of the sprite, Sorting Group, colliders and scripts
//   Visual = child with the SpriteRenderer and the old scale, so it looks exactly the same
// Collider sizes and offsets are recalculated so they stay where they were. Ctrl+Z undoes it.
// Afterwards it checks that the sprite and every collider still cover the same area and warns if not.
public static class RootVisualConverter
{
    const string VisualName = "Visual";
    const float Tolerance = 0.01f;

    [MenuItem("Otsukai/Tools/Convert To Root + Visual")]
    static void ConvertSelected()
    {
        int done = 0;
        foreach (GameObject go in Selection.gameObjects)
        {
            if (Convert(go)) done++;
        }
        Debug.Log("Root + Visual: converted " + done + " of " + Selection.gameObjects.Length + " selected object(s).");
    }

    [MenuItem("Otsukai/Tools/Convert To Root + Visual", true)]
    static bool CanConvert() => Selection.gameObjects.Length > 0;

    static bool Convert(GameObject go)
    {
        Transform root = go.transform;
        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        if (sr == null)
        {
            Debug.LogWarning("Root + Visual: '" + go.name + "' has no SpriteRenderer on itself, skipped.", go);
            return false;
        }
        if (root.Find(VisualName) != null)
        {
            Debug.LogWarning("Root + Visual: '" + go.name + "' already has a Visual child, skipped.", go);
            return false;
        }
        if (root.rotation != Quaternion.identity)
        {
            Debug.LogWarning("Root + Visual: '" + go.name + "' is rotated, convert it by hand.", go);
            return false;
        }
        if (PrefabUtility.IsPartOfPrefabInstance(go))
        {
            Debug.LogWarning("Root + Visual: '" + go.name + "' is a prefab instance. Open the prefab and convert it there.", go);
            return false;
        }

        Undo.RegisterFullObjectHierarchyUndo(go, "Convert to root + Visual");

        Bounds spriteBefore = sr.bounds;
        Collider2D[] colliders = go.GetComponents<Collider2D>();
        Bounds[] colliderBefore = new Bounds[colliders.Length];
        for (int i = 0; i < colliders.Length; i++) colliderBefore[i] = colliders[i].bounds;

        Vector3 oldPos = root.position;
        Vector3 oldScale = root.localScale;
        Vector3 newPos = new Vector3(spriteBefore.center.x, spriteBefore.min.y, oldPos.z);

        // Keep existing children where they are while the root moves and loses its scale
        Transform[] children = new Transform[root.childCount];
        for (int i = 0; i < children.Length; i++) children[i] = root.GetChild(i);
        foreach (Transform c in children) Undo.SetTransformParent(c, null, "Convert to root + Visual");

        // Collider shapes in world space, before the root changes
        ColliderShape[] shapes = new ColliderShape[colliders.Length];
        for (int i = 0; i < colliders.Length; i++) shapes[i] = ColliderShape.Read(colliders[i], root);

        // Visual child gets a copy of the SpriteRenderer and the old scale
        GameObject visual = new GameObject(VisualName);
        Undo.RegisterCreatedObjectUndo(visual, "Convert to root + Visual");
        Undo.SetTransformParent(visual.transform, root, "Convert to root + Visual");
        ComponentUtility.CopyComponent(sr);
        ComponentUtility.PasteComponentAsNew(visual);

        // Sorting Group on the root keeps the old sorting layer and order
        SortingGroup group = go.GetComponent<SortingGroup>();
        if (group == null) group = Undo.AddComponent<SortingGroup>(go);
        group.sortingLayerID = sr.sortingLayerID;
        group.sortingOrder = sr.sortingOrder;

        Undo.DestroyObjectImmediate(sr);

        root.position = newPos;
        root.localScale = Vector3.one;
        visual.transform.position = oldPos;
        visual.transform.localScale = oldScale;

        for (int i = 0; i < colliders.Length; i++) shapes[i].Write(colliders[i], root);
        foreach (Transform c in children) Undo.SetTransformParent(c, root, "Convert to root + Visual");

        // Self-check: everything must cover the same area as before
        Physics2D.SyncTransforms();
        bool ok = Same(visual.GetComponent<SpriteRenderer>().bounds, spriteBefore);
        for (int i = 0; i < colliders.Length; i++) ok &= Same(colliders[i].bounds, colliderBefore[i]);
        if (!ok)
            Debug.LogWarning("Root + Visual: '" + go.name + "' converted, but the sprite or a collider moved. Check it, or Ctrl+Z.", go);

        EditorUtility.SetDirty(go);
        return true;
    }

    static bool Same(Bounds a, Bounds b)
    {
        return (a.center - b.center).sqrMagnitude < Tolerance * Tolerance &&
               (a.size - b.size).sqrMagnitude < Tolerance * Tolerance;
    }

    // A collider's shape in world space, so it can be put back after the root changes
    struct ColliderShape
    {
        Vector2 centre;      // Box / Circle / Capsule: world centre
        Vector2 size;        // Box / Capsule: world size
        float radius;        // Circle: world radius
        Vector2[][] paths;   // Polygon / Edge: world points

        public static ColliderShape Read(Collider2D c, Transform t)
        {
            ColliderShape s = new ColliderShape();
            Vector2 scale = Abs(t.lossyScale);
            s.centre = t.TransformPoint(c.offset);

            if (c is BoxCollider2D box) s.size = Vector2.Scale(box.size, scale);
            else if (c is CapsuleCollider2D cap) s.size = Vector2.Scale(cap.size, scale);
            else if (c is CircleCollider2D circle) s.radius = circle.radius * Mathf.Max(scale.x, scale.y);
            else if (c is PolygonCollider2D poly)
            {
                s.paths = new Vector2[poly.pathCount][];
                for (int p = 0; p < poly.pathCount; p++) s.paths[p] = ToWorld(poly.GetPath(p), c.offset, t);
            }
            else if (c is EdgeCollider2D edge) s.paths = new[] { ToWorld(edge.points, c.offset, t) };
            return s;
        }

        public void Write(Collider2D c, Transform t)
        {
            Undo.RecordObject(c, "Convert to root + Visual");

            if (c is PolygonCollider2D poly)
            {
                poly.offset = Vector2.zero;
                for (int p = 0; p < paths.Length; p++) poly.SetPath(p, ToLocal(paths[p], t));
                return;
            }
            if (c is EdgeCollider2D edge)
            {
                edge.offset = Vector2.zero;
                edge.points = ToLocal(paths[0], t);
                return;
            }

            c.offset = t.InverseTransformPoint(centre);
            if (c is BoxCollider2D box) box.size = size;
            else if (c is CapsuleCollider2D cap) cap.size = size;
            else if (c is CircleCollider2D circle) circle.radius = radius;
        }

        static Vector2[] ToWorld(Vector2[] points, Vector2 offset, Transform t)
        {
            Vector2[] w = new Vector2[points.Length];
            for (int i = 0; i < points.Length; i++) w[i] = t.TransformPoint(points[i] + offset);
            return w;
        }

        static Vector2[] ToLocal(Vector2[] points, Transform t)
        {
            Vector2[] l = new Vector2[points.Length];
            for (int i = 0; i < points.Length; i++) l[i] = t.InverseTransformPoint(points[i]);
            return l;
        }

        static Vector2 Abs(Vector3 v) => new Vector2(Mathf.Abs(v.x), Mathf.Abs(v.y));
    }
}
