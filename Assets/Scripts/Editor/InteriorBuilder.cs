using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

// Otsukai > Town > Build Interiors: builds one blockout room per entry of Assets/Data/InteriorPlan.asset,
// far outside the town map, in the open town scene (MainScene). For every room:
//   - floor, 2-tile back wall and wall edge painted on the "InteriorTiles" tilemap, wall colliders, a RoomArea
//   - furniture from the template (root + Visual + Sorting Group + wall collider)
//   - the errand spot (ErrandSpot + NavTarget with the building's Target Id) in front of the counter
//   - an exit Door (Stay In Scene) back to "<id>/Out" and the "<id>/In" spawn point
// In town, the building's door object ("Door <id>" under Town Layout) loses its outside errand spot and gets a
// Door (Stay In Scene) to "<id>/In" plus the "<id>/Out" spawn point. Running it again rebuilds every room.
public static class InteriorBuilder
{
    const string PlanPath = "Assets/Data/InteriorPlan.asset";
    const string ArtFolder = "Assets/Art/Interior/";
    const string TileFolder = "Assets/Art/Interior/Tiles/";
    const string RootName = "Interiors";
    const string TownRoot = "Town Layout";
    const string TilemapName = "InteriorTiles";
    const string ExitPrompt = "[E] Exit";
    const string EnterPrompt = "[E] Enter";

    [Serializable] class Item { public string key; public string name; public float w; public float h; public float wall; }
    [Serializable] class Meta { public Item[] items; }

    // A piece of furniture: x = centre from the room's middle line, y = bottom row from the floor's bottom (tiles)
    struct Piece
    {
        public string key; public float x, y;
        public Piece(string key, float x, float y) { this.key = key; this.x = x; this.y = y; }
    }

    static Vector2Int DefaultSize(InteriorPlan.Template t)
    {
        switch (t)
        {
            case InteriorPlan.Template.Store:
            case InteriorPlan.Template.Office:
            case InteriorPlan.Template.Library: return new Vector2Int(14, 8);
            default: return new Vector2Int(10, 6);
        }
    }

    // Furniture and the errand spot for each template. Back row = h - 2 (2-tile pieces stand against the wall).
    static List<Piece> Layout(InteriorPlan.Template t, int w, int h, out Vector2 spot)
    {
        int back = h - 2;
        List<Piece> p = new List<Piece>();
        switch (t)
        {
            case InteriorPlan.Template.Store:
                p.Add(new Piece("Counter", 4.5f, back));
                p.Add(new Piece("Shelf", -5f, back)); p.Add(new Piece("Shelf", -3f, back));
                p.Add(new Piece("Shelf", -5f, 2f)); p.Add(new Piece("Shelf", -3f, 2f)); p.Add(new Piece("Shelf", -1f, 2f));
                spot = new Vector2(4.5f, back - 0.5f);
                break;
            case InteriorPlan.Template.Office:
                p.Add(new Piece("Counter", -3.5f, back)); p.Add(new Piece("Counter", -0.5f, back)); p.Add(new Piece("Counter", 2.5f, back));
                p.Add(new Piece("Bench", -4f, 2f)); p.Add(new Piece("Bench", 4f, 2f));
                spot = new Vector2(-0.5f, back - 0.5f);
                break;
            case InteriorPlan.Template.Library:
                p.Add(new Piece("Counter", 4.5f, back));
                p.Add(new Piece("Bookshelf", -5f, back)); p.Add(new Piece("Bookshelf", -3f, back)); p.Add(new Piece("Bookshelf", -1f, back));
                p.Add(new Piece("Bookshelf", -5f, 2f)); p.Add(new Piece("Bookshelf", -3f, 2f));
                spot = new Vector2(4.5f, back - 0.5f);
                break;
            case InteriorPlan.Template.Cafe:
                p.Add(new Piece("Counter", 2.5f, back));
                p.Add(new Piece("CafeTable", -3f, 1f)); p.Add(new Piece("CafeTable", 3f, 1f));
                spot = new Vector2(2.5f, back - 0.5f);
                break;
            case InteriorPlan.Template.Laundry:
                for (int i = 0; i < 4; i++) p.Add(new Piece("Washer", -4.5f + i, back));
                p.Add(new Piece("Counter", 2.5f, back));
                p.Add(new Piece("Bench", -3f, 1f));
                spot = new Vector2(2.5f, back - 0.5f);
                break;
            default: // SmallShop
                p.Add(new Piece("Counter", 2.5f, back));
                p.Add(new Piece("Shelf", -3f, back)); p.Add(new Piece("Shelf", -3f, 1f));
                spot = new Vector2(2.5f, back - 0.5f);
                break;
        }
        return p;
    }

    static bool TileFloor(InteriorPlan.Template t) => t == InteriorPlan.Template.Office || t == InteriorPlan.Template.Library;

    [MenuItem("Otsukai/Town/Build Interiors")]
    static void Build()
    {
        InteriorPlan plan = AssetDatabase.LoadAssetAtPath<InteriorPlan>(PlanPath);
        if (plan == null || plan.rooms == null) { Debug.LogWarning("Interiors: " + PlanPath + " not found."); return; }
        if (!File.Exists(ArtFolder + "interior.json")) { Debug.LogWarning("Interiors: run 'node Design/sprites/buildings.js export' first."); return; }

        Dictionary<string, Item> items = new Dictionary<string, Item>();
        foreach (Item it in JsonUtility.FromJson<Meta>(File.ReadAllText(ArtFolder + "interior.json")).items) items[it.key] = it;

        GameObject gridGo = GameObject.Find("Grid");
        Grid grid = gridGo != null ? gridGo.GetComponent<Grid>() : null;
        GameObject town = GameObject.Find(TownRoot);
        if (grid == null || town == null)
        {
            Debug.LogWarning("Interiors: open MainScene first (needs 'Grid' and '" + TownRoot + "').");
            return;
        }
        if (!EditorUtility.DisplayDialog("Build interiors", "Rebuilds every room in " + PlanPath +
                                         " and turns the matching town doors into doors to those rooms. Ctrl+Z undoes it.", "Build", "Cancel"))
            return;

        Tilemap tiles = InteriorTilemap(grid);
        Undo.RegisterCompleteObjectUndo(tiles, "Build interiors");
        tiles.ClearAllTiles();

        GameObject old = GameObject.Find(RootName);
        if (old != null) Undo.DestroyObjectImmediate(old);
        GameObject root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Build interiors");

        TileBase wood = TileFor("FloorWood"), floorTile = TileFor("FloorTile");
        TileBase wallLow = TileFor("WallFace"), wallHigh = TileFor("WallFaceUpper"), wallTop = TileFor("WallTop");

        int built = 0;
        for (int i = 0; i < plan.rooms.Length; i++)
        {
            InteriorPlan.Room r = plan.rooms[i];
            if (string.IsNullOrEmpty(r.buildingId)) continue;

            Vector2Int size = r.size.x > 0 && r.size.y > 0 ? r.size : DefaultSize(r.template);
            int w = size.x + (size.x % 2), h = size.y;   // even width keeps the door on the middle line
            int ox = Mathf.RoundToInt(plan.firstRoom.x + (i % plan.roomsPerRow) * plan.spacing.x);
            int oy = Mathf.RoundToInt(plan.firstRoom.y - (i / plan.roomsPerRow) * plan.spacing.y);

            // Tiles: floor, two wall rows at the back, dark edge all around
            Fill(tiles, ox - w / 2, oy, w, h, TileFloor(r.template) ? floorTile : wood);
            Fill(tiles, ox - w / 2, oy + h, w, 1, wallLow);
            Fill(tiles, ox - w / 2, oy + h + 1, w, 1, wallHigh);
            Fill(tiles, ox - w / 2 - 1, oy - 1, w + 2, 1, wallTop);
            Fill(tiles, ox - w / 2 - 1, oy + h + 2, w + 2, 1, wallTop);
            Fill(tiles, ox - w / 2 - 1, oy, 1, h + 2, wallTop);
            Fill(tiles, ox + w / 2, oy, 1, h + 2, wallTop);

            GameObject room = Child(root.transform, "Room " + r.buildingId, new Vector3(ox, oy, 0f));
            RoomArea area = room.AddComponent<RoomArea>();
            area.size = new Vector2(w + 2, h + 4);
            area.centerOffset = new Vector2(0f, -1f + (h + 4) / 2f);

            // Walls: the back wall rows are solid too, the player walks on the floor only
            GameObject walls = Child(room.transform, "Walls", room.transform.position);
            AddBox(walls, new Vector2(0f, -0.5f), new Vector2(w + 2, 1));
            AddBox(walls, new Vector2(0f, h + 1.5f), new Vector2(w + 2, 3));
            AddBox(walls, new Vector2(-w / 2f - 0.5f, -1f + (h + 4) / 2f), new Vector2(1, h + 4));
            AddBox(walls, new Vector2(w / 2f + 0.5f, -1f + (h + 4) / 2f), new Vector2(1, h + 4));

            List<Piece> pieces = Layout(r.template, w, h, out Vector2 spot);
            foreach (Piece piece in pieces)
            {
                if (!items.TryGetValue(piece.key, out Item item)) continue;
                Furniture(room.transform, piece, item);
            }

            // Errand spot in front of the counter: the building's errand steps happen here
            GameObject spotGo = Child(room.transform, "Counter Spot", room.transform.position + (Vector3)spot);
            BoxCollider2D trig = spotGo.AddComponent<BoxCollider2D>();
            trig.isTrigger = true;
            trig.size = new Vector2(2f, 1.2f);
            ErrandSpot es = spotGo.AddComponent<ErrandSpot>();
            es.targetId = r.buildingId;
            spotGo.AddComponent<NavTarget>().targetId = r.buildingId;

            // Exit door on the bottom row, spawn point just above it
            GameObject exit = Child(room.transform, "Exit", room.transform.position + new Vector3(0f, 0.5f, 0f));
            BoxCollider2D exitTrig = exit.AddComponent<BoxCollider2D>();
            exitTrig.isTrigger = true;
            exitTrig.size = new Vector2(1.5f, 1f);
            Door exitDoor = exit.AddComponent<Door>();
            exitDoor.stayInScene = true;
            exitDoor.spawnAtTarget = r.buildingId + "/Out";
            exitDoor.spawnOffset = new Vector2(0f, -1f);
            exitDoor.promptText = ExitPrompt;
            Child(room.transform, "Entrance", room.transform.position + new Vector3(0f, 1.6f, 0f))
                .AddComponent<NavTarget>().targetId = r.buildingId + "/In";

            if (!ConnectTownDoor(town.transform, r)) Debug.LogWarning("Interiors: no 'Door " + r.buildingId + "' under " + TownRoot + ". Run Import Layout CSV.");
            built++;
        }

        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("Interiors: " + built + " rooms built.");
    }

    // Town side: the door object of the building becomes a Door into its room
    static bool ConnectTownDoor(Transform town, InteriorPlan.Room r)
    {
        foreach (Transform building in town)
        {
            Transform doorT = building.Find("Door " + r.buildingId);
            if (doorT == null) continue;
            GameObject door = doorT.gameObject;

            ErrandSpot es = door.GetComponent<ErrandSpot>();
            string label = es != null ? es.idleLabel : null;
            if (es != null) Undo.DestroyObjectImmediate(es);

            BoxCollider2D trig = door.GetComponent<BoxCollider2D>();
            if (trig == null) { trig = Undo.AddComponent<BoxCollider2D>(door); trig.size = new Vector2(2f, 1.5f); }
            trig.isTrigger = true;

            Door d = door.GetComponent<Door>();
            if (d == null) d = Undo.AddComponent<Door>(door);
            Undo.RecordObject(d, "Build interiors");
            d.stayInScene = true;
            d.spawnAtTarget = r.buildingId + "/In";
            d.spawnOffset = Vector2.zero;
            d.promptText = EnterPrompt;
            d.placeName = !string.IsNullOrEmpty(r.placeName) ? r.placeName : label;

            Transform outT = doorT.Find("Out");
            GameObject outGo = outT != null ? outT.gameObject : null;
            if (outGo == null)
            {
                outGo = new GameObject("Out");
                Undo.RegisterCreatedObjectUndo(outGo, "Build interiors");
                outGo.transform.SetParent(doorT, false);
                outGo.AddComponent<NavTarget>();
            }
            outGo.GetComponent<NavTarget>().targetId = r.buildingId + "/Out";
            return true;
        }
        return false;
    }

    static void Furniture(Transform room, Piece piece, Item item)
    {
        GameObject go = Child(room, piece.key, room.position + new Vector3(piece.x, piece.y, 0f));
        GameObject visual = Child(go.transform, "Visual", go.transform.position + new Vector3(0f, item.h / 2f, 0f));
        visual.AddComponent<SpriteRenderer>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "Interior_" + piece.key + ".png");
        go.AddComponent<SortingGroup>();
        AddBox(go, new Vector2(0f, item.wall / 2f), new Vector2(item.w, item.wall));
    }

    static GameObject Child(Transform parent, string name, Vector3 worldPos)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = worldPos;
        return go;
    }

    static void AddBox(GameObject go, Vector2 offset, Vector2 size)
    {
        BoxCollider2D b = go.AddComponent<BoxCollider2D>();
        b.offset = offset;
        b.size = size;
    }

    static void Fill(Tilemap map, int x, int y, int w, int h, TileBase tile)
    {
        if (tile == null || w <= 0 || h <= 0) return;
        TileBase[] arr = new TileBase[w * h];
        for (int i = 0; i < arr.Length; i++) arr[i] = tile;
        map.SetTilesBlock(new BoundsInt(x, y, 0, w, h, 1), arr);
    }

    static Tilemap InteriorTilemap(Grid grid)
    {
        Transform t = grid.transform.Find(TilemapName);
        if (t != null) return t.GetComponent<Tilemap>();

        GameObject go = new GameObject(TilemapName, typeof(Tilemap), typeof(TilemapRenderer));
        Undo.RegisterCreatedObjectUndo(go, "Build interiors");
        go.transform.SetParent(grid.transform, false);
        go.GetComponent<TilemapRenderer>().sortingOrder = -100;   // below everything, like Ground
        return go.GetComponent<Tilemap>();
    }

    // A Tile asset for an interior sprite, made once in Assets/Art/Interior/Tiles
    internal static TileBase TileFor(string key)
    {
        string path = TileFolder + key + ".asset";
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "Interior_" + key + ".png");
        if (sprite == null) { Debug.LogWarning("Interiors: " + ArtFolder + "Interior_" + key + ".png is missing or not a sprite."); return null; }

        Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
        if (tile != null)
        {
            if (tile.sprite != sprite) { tile.sprite = sprite; EditorUtility.SetDirty(tile); }   // the sprite was re-imported
            return tile;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Art/Interior/Tiles")) AssetDatabase.CreateFolder("Assets/Art/Interior", "Tiles");
        tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sprite;
        AssetDatabase.CreateAsset(tile, path);
        return tile;
    }
}
