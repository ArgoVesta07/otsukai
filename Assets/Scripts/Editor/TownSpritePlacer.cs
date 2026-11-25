using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Otsukai > Town > Place Building Sprites: puts the blockout sprites from Assets/Art/Buildings onto the
// "Town Layout" objects made by Import Layout CSV, following the project's object structure:
//   root (Sorting Group + wall collider, only the lower wall part, not the roof) + child "Visual" (SpriteRenderer).
// Buildings use their number (TownBuilding), spots use their name ("Kotak pos", "Halte bus").
// The player's house (2) and Supa (1) are prefabs with their own Visual: the sprite is set in the prefab asset.
// Mode boards are their own objects (ModeSelectPoint), not Town Layout ones: they get the "ModeBoard" signpost
// sprite in their Visual and a small solid collider at the pole. Running it again updates what is there.
public static class TownSpritePlacer
{
    const string RootName = "Town Layout";
    const string Folder = "Assets/Art/Buildings/";
    const string VisualName = "Visual";

    [Serializable] class Item { public string key; public string name; public float w; public float h; public float wall; }
    [Serializable] class Meta { public Item[] items; }

    static readonly Dictionary<string, string> PrefabFor = new Dictionary<string, string>
    {
        { "1", "Assets/Prefab/Supermarket.prefab" },
        { "2", "Assets/Prefab/House.prefab" },
    };
    static readonly Dictionary<string, string> SpotKey = new Dictionary<string, string>
    {
        { "Kotak pos", "Mailbox" },
        { "Halte bus", "BusStop" },
    };

    [MenuItem("Otsukai/Town/Place Building Sprites")]
    static void Place()
    {
        string metaPath = Folder + "buildings.json";
        if (!File.Exists(metaPath))
        {
            Debug.LogWarning("Town sprites: " + metaPath + " not found. Run 'node Design/sprites/buildings.js export' first.");
            return;
        }
        Dictionary<string, Item> items = new Dictionary<string, Item>();
        foreach (Item it in JsonUtility.FromJson<Meta>(File.ReadAllText(metaPath)).items) items[it.key] = it;

        GameObject root = GameObject.Find(RootName);
        if (root == null)
        {
            Debug.LogWarning("Town sprites: no '" + RootName + "' in the open scene. Run Otsukai > Town > Import Layout CSV first.");
            return;
        }

        int placed = 0, missing = 0;
        foreach (Transform child in root.transform)
        {
            TownBuilding tb = child.GetComponent<TownBuilding>();
            string key = tb != null ? tb.number : KeyForSpot(child.name);
            if (key == null || key.StartsWith("27")) continue;

            if (!items.TryGetValue(key, out Item item))
            {
                missing++;
                Debug.LogWarning("Town sprites: no sprite for '" + child.name + "' (key " + key + ").", child);
                continue;
            }
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "Building_" + key + ".png");
            if (sprite == null)
            {
                missing++;
                Debug.LogWarning("Town sprites: " + Folder + "Building_" + key + ".png is missing or not a sprite.", child);
                continue;
            }

            if (PrefabFor.TryGetValue(key, out string prefabPath)) SetPrefabSprite(prefabPath, sprite);
            else Dress(child.gameObject, sprite, item, tb == null);
            placed++;
        }

        // Mode boards: signpost sprite
        if (items.TryGetValue("ModeBoard", out Item board))
        {
            Sprite boardSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "Building_ModeBoard.png");
            foreach (GameObject sceneRoot in root.scene.GetRootGameObjects())
            {
                foreach (ModeSelectPoint point in sceneRoot.GetComponentsInChildren<ModeSelectPoint>())
                {
                    if (boardSprite == null) break;
                    DressBoard(point.gameObject, boardSprite, board);
                    placed++;
                }
            }
        }

        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("Town sprites: " + placed + " placed" + (missing > 0 ? ", " + missing + " missing (see warnings)." : "."));
    }

    static string KeyForSpot(string objectName)
    {
        foreach (var pair in SpotKey)
            if (objectName.EndsWith(pair.Key)) return pair.Value;
        return null;
    }

    // Root keeps its position; Visual at half height. Spots stand on a tile centre, so they sit half a tile lower.
    static void Dress(GameObject go, Sprite sprite, Item item, bool isSpot)
    {
        float baseY = isSpot ? -0.5f : 0f;

        Transform visual = go.transform.Find(VisualName);
        if (visual == null)
        {
            GameObject v = new GameObject(VisualName);
            Undo.RegisterCreatedObjectUndo(v, "Place building sprites");
            Undo.SetTransformParent(v.transform, go.transform, "Place building sprites");
            visual = v.transform;
        }
        Undo.RecordObject(visual, "Place building sprites");
        visual.localPosition = new Vector3(0f, baseY + item.h / 2f, 0f);
        visual.localScale = Vector3.one;

        SpriteRenderer sr = visual.GetComponent<SpriteRenderer>();
        if (sr == null) sr = Undo.AddComponent<SpriteRenderer>(visual.gameObject);
        Undo.RecordObject(sr, "Place building sprites");
        sr.sprite = sprite;

        if (go.GetComponent<SortingGroup>() == null) Undo.AddComponent<SortingGroup>(go);

        // Solid wall collider on the root: full width (a little less for small props), wall height only
        BoxCollider2D wall = null;
        foreach (BoxCollider2D c in go.GetComponents<BoxCollider2D>())
            if (!c.isTrigger) { wall = c; break; }
        if (wall == null) wall = Undo.AddComponent<BoxCollider2D>(go);
        Undo.RecordObject(wall, "Place building sprites");
        float width = isSpot ? item.w * 0.75f : item.w;
        float height = isSpot ? Mathf.Min(item.wall, 0.5f) : item.wall;
        wall.size = new Vector2(width, height);
        wall.offset = new Vector2(0f, baseY + height / 2f);
    }

    // Root stays where it is (bottom centre); Visual gets the sprite at scale 1. Only the solid collider is resized:
    // the trigger that opens the mode menu keeps its size.
    static void DressBoard(GameObject go, Sprite sprite, Item item)
    {
        Transform visual = go.transform.Find(VisualName);
        SpriteRenderer sr = visual != null ? visual.GetComponent<SpriteRenderer>() : go.GetComponent<SpriteRenderer>();
        if (sr == null)
        {
            Debug.LogWarning("Town sprites: '" + go.name + "' has no SpriteRenderer. Run Otsukai > Tools > Convert To Root + Visual on it first.", go);
            return;
        }
        if (visual == null)
        {
            Debug.LogWarning("Town sprites: '" + go.name + "' has no Visual child. Run Otsukai > Tools > Convert To Root + Visual on it first.", go);
            return;
        }

        Undo.RecordObject(visual, "Place building sprites");
        visual.localPosition = new Vector3(0f, item.h / 2f, 0f);
        visual.localRotation = Quaternion.identity;
        visual.localScale = Vector3.one;
        Undo.RecordObject(sr, "Place building sprites");
        sr.sprite = sprite;

        foreach (BoxCollider2D c in go.GetComponents<BoxCollider2D>())
        {
            if (c.isTrigger) continue;
            Undo.RecordObject(c, "Place building sprites");
            c.size = new Vector2(0.5f, item.wall);
            c.offset = new Vector2(0f, item.wall / 2f);
        }
    }

    static void SetPrefabSprite(string prefabPath, Sprite sprite)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            Transform visual = contents.transform.Find(VisualName);
            SpriteRenderer sr = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
            if (sr == null)
            {
                Debug.LogWarning("Town sprites: " + prefabPath + " has no Visual with a SpriteRenderer, skipped.");
                return;
            }
            sr.sprite = sprite;
            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }
}
