using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Otsukai > Town > Import Layout CSV: reads Design/town-v2/Town_Map_v2.csv (made by town_map.py) and puts,
// under a "Town Layout" object in the open scene:
//   - one TownBuilding placeholder per building (footprint drawn in the Scene view),
//   - a NavTarget (HUD arrow) at each door that has a Target Id,
//   - an ErrandSpot with a trigger there when "Errand Spot" = Ya (not for buildings with a Door to an interior),
//     with the "Label" column as its Idle Label (the name shown when the player stands there; kept if already set).
// Running it again moves the existing objects instead of making new ones, so art placed under them stays.
public static class TownLayoutImporter
{
    const string DefaultCsv = "Design/town-v2/Town_Map_v2.csv";
    const string RootName = "Town Layout";

    [MenuItem("Otsukai/Town/Import Layout CSV")]
    static void Import()
    {
        string path = File.Exists(DefaultCsv) ? DefaultCsv
                    : EditorUtility.OpenFilePanel("Town layout CSV", "Design", "csv");
        if (string.IsNullOrEmpty(path)) return;

        string[] lines = File.ReadAllLines(path);
        if (lines.Length < 2)
        {
            Debug.LogWarning("Town layout: the CSV is empty.");
            return;
        }

        Dictionary<string, int> col = new Dictionary<string, int>();
        string[] header = lines[0].Split(',');
        for (int i = 0; i < header.Length; i++) col[header[i].Trim()] = i;

        string[] needed = { "No", "Name", "Size (tiles)", "Enterable", "Root X", "Root Y",
                            "Door X", "Door Y", "Target Id", "Errand Spot" };
        foreach (string c in needed)
        {
            if (!col.ContainsKey(c))
            {
                Debug.LogWarning("Town layout: column '" + c + "' is missing. Run town_map.py again.");
                return;
            }
        }

        GameObject root = GameObject.Find(RootName);
        if (root == null)
        {
            root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Import town layout");
        }

        int count = 0;
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            string[] v = lines[i].Split(',');
            if (v.Length < header.Length)
            {
                Debug.LogWarning("Town layout: line " + (i + 1) + " has too few columns, skipped.");
                continue;
            }

            string no = v[col["No"]].Trim();
            string objName = no + " " + v[col["Name"]].Trim();
            Vector3 rootPos = new Vector3(Num(v[col["Root X"]]), Num(v[col["Root Y"]]), 0f);
            Vector3 doorPos = new Vector3(Num(v[col["Door X"]]), Num(v[col["Door Y"]]), 0f);
            string targetId = v[col["Target Id"]].Trim();
            bool spot = v[col["Errand Spot"]].Trim() == "Yes";
            string label = col.TryGetValue("Label", out int li) && li < v.Length ? v[li].Trim() : "";

            GameObject building = Numbered(root.transform, no, objName);
            Undo.RecordObject(building.transform, "Import town layout");
            building.transform.position = rootPos;

            string[] size = v[col["Size (tiles)"]].Split('x');
            if (size.Length == 2 && Num(size[0]) > 0f)
            {
                TownBuilding tb = building.GetComponent<TownBuilding>();
                if (tb == null) tb = Undo.AddComponent<TownBuilding>(building);
                Undo.RecordObject(tb, "Import town layout");
                tb.number = no;
                tb.size = new Vector2(Num(size[0]), Num(size[1]));
                tb.enterable = v[col["Enterable"]].Trim() == "Yes";
            }

            if (!string.IsNullOrEmpty(targetId))
                PlaceDoor(building.transform, doorPos, targetId, spot, label);

            count++;
        }

        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        Debug.Log("Town layout: " + count + " objects placed from " + path + ".");
    }

    static void PlaceDoor(Transform building, Vector3 pos, string targetId, bool spot, string label)
    {
        GameObject door = Child(building, "Door " + targetId);
        Undo.RecordObject(door.transform, "Import town layout");
        door.transform.position = pos;

        NavTarget nav = door.GetComponent<NavTarget>();
        if (nav == null) nav = Undo.AddComponent<NavTarget>(door);
        Undo.RecordObject(nav, "Import town layout");
        nav.targetId = targetId;

        // Buildings with a room (Build Interiors) have a Door here instead of an errand spot: leave them alone
        if (!spot || door.GetComponent<Door>() != null) return;

        BoxCollider2D box = door.GetComponent<BoxCollider2D>();
        if (box == null)
        {
            box = Undo.AddComponent<BoxCollider2D>(door);
            box.size = new Vector2(2f, 1.5f);
        }
        box.isTrigger = true;

        ErrandSpot es = door.GetComponent<ErrandSpot>();
        if (es == null) es = Undo.AddComponent<ErrandSpot>(door);
        Undo.RecordObject(es, "Import town layout");
        es.targetId = targetId;
        if (string.IsNullOrEmpty(es.idleLabel)) es.idleLabel = label;
    }

    // The child for this layout number ("23 ..."), renamed if the name in the CSV changed; made if missing
    static GameObject Numbered(Transform parent, string no, string childName)
    {
        foreach (Transform t in parent)
        {
            if (!t.name.StartsWith(no + " ")) continue;
            if (t.name != childName)
            {
                Undo.RecordObject(t.gameObject, "Import town layout");
                t.name = childName;
            }
            return t.gameObject;
        }
        return Child(parent, childName);
    }

    static GameObject Child(Transform parent, string childName)
    {
        Transform t = parent.Find(childName);
        if (t != null) return t.gameObject;

        GameObject go = new GameObject(childName);
        Undo.RegisterCreatedObjectUndo(go, "Import town layout");
        go.transform.SetParent(parent, false);
        return go;
    }

    static float Num(string s)
    {
        return float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : 0f;
    }
}
