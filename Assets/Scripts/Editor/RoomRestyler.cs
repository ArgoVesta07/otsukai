using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

// Otsukai > Town > Restyle Home And Supermarket: gives the two rooms moved in from their old scenes
// ("Rooms From Scenes") the same blockout look as the other interiors. Scripts stay on the same objects.
//   - floor (wood at home, tiles in the shop), 2-tile back wall and wall edge, painted on the room's own tilemap,
//     with a collider over the back wall rows; the room size comes from its Wall* colliders
//   - Bed and the letter desk get their new sprites; the three ShopItem shelves get the shelf sprite and stand
//     in a row in front of the back wall; a checkout counter (decoration) goes next to the shop door
// Ctrl+Z undoes it.
public static class RoomRestyler
{
    const string RootName = "Rooms From Scenes";
    const string ArtFolder = "Assets/Art/Interior/";

    [Serializable] class Item { public string key; public string name; public float w; public float h; public float wall; }
    [Serializable] class Meta { public Item[] items; }

    [MenuItem("Otsukai/Town/Restyle Home And Supermarket")]
    static void Restyle()
    {
        GameObject root = GameObject.Find(RootName);
        if (root == null) { Debug.LogWarning("Restyle: no '" + RootName + "'. Run Move House And Supermarket Into Town first."); return; }

        Dictionary<string, Item> items = new Dictionary<string, Item>();
        foreach (Item it in JsonUtility.FromJson<Meta>(File.ReadAllText(ArtFolder + "interior.json")).items) items[it.key] = it;

        Transform home = root.transform.Find("Room Home"), shop = root.transform.Find("Room Supermarket");
        if (home != null) RestyleRoom(home, "FloorWood", items, false);
        if (shop != null) RestyleRoom(shop, "FloorTile", items, true);

        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("Restyle: " + (home != null ? "Home " : "") + (shop != null ? "Supermarket" : "") + " done.");
    }

    static void RestyleRoom(Transform room, string floorKey, Dictionary<string, Item> items, bool isShop)
    {
        Undo.RegisterFullObjectHierarchyUndo(room.gameObject, "Restyle rooms");

        // Inside of the walls (world), from the Wall* colliders
        Bounds outer = new Bounds(); bool any = false; float thick = float.MaxValue;
        foreach (BoxCollider2D c in room.GetComponentsInChildren<BoxCollider2D>(true))
        {
            if (!c.name.StartsWith("Wall") || c.isTrigger) continue;
            if (!any) { outer = c.bounds; any = true; } else outer.Encapsulate(c.bounds);
            thick = Mathf.Min(thick, Mathf.Min(c.bounds.size.x, c.bounds.size.y));
        }
        if (!any) { Debug.LogWarning("Restyle: no Wall colliders in " + room.name + ".", room); return; }

        int x0 = Mathf.RoundToInt(outer.min.x + thick), x1 = Mathf.RoundToInt(outer.max.x - thick);   // world, inner edges
        int y0 = Mathf.RoundToInt(outer.min.y + thick), y1 = Mathf.RoundToInt(outer.max.y - thick);
        int floorTop = y1 - 2;   // the top two rows become the back wall

        // Paint on the room's own tilemap (it moves with the room and Build Interiors never clears it)
        Tilemap map = room.GetComponentInChildren<Tilemap>(true);
        if (map == null) { Debug.LogWarning("Restyle: no tilemap in " + room.name + ".", room); return; }
        Undo.RegisterCompleteObjectUndo(map, "Restyle rooms");
        map.ClearAllTiles();
        TileBase floor = InteriorBuilder.TileFor(floorKey), low = InteriorBuilder.TileFor("WallFace"),
                 high = InteriorBuilder.TileFor("WallFaceUpper"), edge = InteriorBuilder.TileFor("WallTop");
        Paint(map, x0, y0, x1, floorTop, floor);
        Paint(map, x0, floorTop, x1, floorTop + 1, low);
        Paint(map, x0, floorTop + 1, x1, y1, high);
        Paint(map, x0 - 1, y0 - 1, x1 + 1, y0, edge);
        Paint(map, x0 - 1, y1, x1 + 1, y1 + 1, edge);
        Paint(map, x0 - 1, y0, x0, y1, edge);
        Paint(map, x1, y0, x1 + 1, y1, edge);

        // The back wall rows are solid
        Transform bw = room.Find("BackWall");
        GameObject backWall = bw != null ? bw.gameObject : new GameObject("BackWall");
        if (bw == null) { Undo.RegisterCreatedObjectUndo(backWall, "Restyle rooms"); backWall.transform.SetParent(room, false); }
        backWall.transform.position = new Vector3((x0 + x1) / 2f, floorTop + 1f, 0f);
        BoxCollider2D bc = backWall.GetComponent<BoxCollider2D>();
        if (bc == null) bc = backWall.AddComponent<BoxCollider2D>();   // not ??: Unity objects need the == check
        bc.size = new Vector2(x1 - x0, 2f);

        // Furniture with scripts: new sprites, same objects
        foreach (Bed b in room.GetComponentsInChildren<Bed>(true)) Dress(b.gameObject, "Bed", items);
        foreach (ErrandSpot es in room.GetComponentsInChildren<ErrandSpot>(true))
            if (es.targetId == "LetterDesk") Dress(es.gameObject, "Desk", items);

        if (isShop)
        {
            ShopItem[] shelves = room.GetComponentsInChildren<ShopItem>(true);
            float cx = (x0 + x1) / 2f;
            for (int i = 0; i < shelves.Length; i++)
            {
                Transform s = shelves[i].transform;
                s.position = new Vector3(cx - 6f + i * 3f, floorTop - 2f, s.position.z);   // a row in front of the back wall
                Dress(s.gameObject, "Shelf", items);
                foreach (BoxCollider2D c in s.GetComponents<BoxCollider2D>())
                    if (c.isTrigger) { c.size = new Vector2(2f, 1f); c.offset = new Vector2(0f, -0.3f); }   // stand in front to take
            }

            Transform counter = room.Find("Counter");
            if (counter == null && items.TryGetValue("Counter", out Item ci))
            {
                GameObject go = new GameObject("Counter");
                Undo.RegisterCreatedObjectUndo(go, "Restyle rooms");
                go.transform.SetParent(room, false);
                go.transform.position = new Vector3(cx + 5.5f, y0 + 1f, 0f);   // right of the door
                GameObject v = new GameObject("Visual");
                v.transform.SetParent(go.transform, false);
                v.transform.localPosition = new Vector3(0f, ci.h / 2f, 0f);
                v.AddComponent<SpriteRenderer>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "Interior_Counter.png");
                go.AddComponent<SortingGroup>();
                BoxCollider2D col = go.AddComponent<BoxCollider2D>();
                col.size = new Vector2(ci.w, ci.wall);
                col.offset = new Vector2(0f, ci.wall / 2f);
            }
        }
    }

    // New sprite in the Visual child (scale 1, no turn), solid collider over the lower part
    static void Dress(GameObject go, string key, Dictionary<string, Item> items)
    {
        if (!items.TryGetValue(key, out Item item)) return;
        Transform visual = go.transform.Find("Visual");
        if (visual == null) { Debug.LogWarning("Restyle: '" + go.name + "' has no Visual. Run Convert To Root + Visual on it first.", go); return; }

        go.transform.rotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        visual.localRotation = Quaternion.identity;
        visual.localScale = Vector3.one;
        visual.localPosition = new Vector3(0f, item.h / 2f, 0f);
        SpriteRenderer sr = visual.GetComponent<SpriteRenderer>();
        if (sr != null) sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "Interior_" + key + ".png");

        foreach (BoxCollider2D c in go.GetComponents<BoxCollider2D>())
        {
            if (c.isTrigger) continue;
            c.size = new Vector2(item.w, item.wall);
            c.offset = new Vector2(0f, item.wall / 2f);
        }
    }

    // Cells from (x0, y0) up to but not including (x1, y1), in world units = tiles
    static void Paint(Tilemap map, int x0, int y0, int x1, int y1, TileBase tile)
    {
        if (tile == null) return;
        for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
                map.SetTile(map.WorldToCell(new Vector3(x + 0.5f, y + 0.5f, 0f)), tile);
    }
}
