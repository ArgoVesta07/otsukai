using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// Otsukai > Tools > Fit Floor To Walls: for an interior scene (House, Supermarket, ...).
// Reads the colliders of the objects named "Wall..." and repaints the "FloorTiles" tilemap so it covers
// exactly the inside of the walls: missing tiles are added, tiles outside the walls are removed.
// The tile used is the one the floor already uses most (so your own floor tile stays); Undo works.
public static class FloorFitter
{
    const string FloorName = "FloorTiles";
    const string WallPrefix = "Wall";
    const string FallbackTile = "Assets/Art/Tilesets/TIles/Tile_Floor_1x1_0.asset";

    [MenuItem("Otsukai/Tools/Fit Floor To Walls")]
    static void Fit()
    {
        GameObject floorGo = GameObject.Find(FloorName);
        Tilemap floor = floorGo != null ? floorGo.GetComponent<Tilemap>() : null;
        if (floor == null)
        {
            Debug.LogWarning("Fit floor: no active Tilemap named '" + FloorName + "' in the open scene.");
            return;
        }

        // Outer edge of all wall colliders, and the thinnest wall
        bool any = false;
        Bounds outer = new Bounds();
        float thickness = float.MaxValue;
        List<Collider2D> colliders = new List<Collider2D>();
        foreach (GameObject root in floor.gameObject.scene.GetRootGameObjects())
            colliders.AddRange(root.GetComponentsInChildren<Collider2D>());

        foreach (Collider2D c in colliders)
        {
            if (!c.name.StartsWith(WallPrefix) || c.isTrigger) continue;
            Bounds b = c.bounds;
            if (!any) { outer = b; any = true; } else outer.Encapsulate(b);
            thickness = Mathf.Min(thickness, Mathf.Min(b.size.x, b.size.y));
        }
        if (!any)
        {
            Debug.LogWarning("Fit floor: no wall colliders found (objects whose name starts with '" + WallPrefix + "').");
            return;
        }

        // Inside of the walls, in tilemap cells
        Vector3Int min = floor.WorldToCell(outer.min + new Vector3(thickness, thickness, 0f) + Vector3.one * 0.01f);
        Vector3Int max = floor.WorldToCell(outer.max - new Vector3(thickness, thickness, 0f) - Vector3.one * 0.01f);
        if (max.x < min.x || max.y < min.y)
        {
            Debug.LogWarning("Fit floor: the walls leave no room inside.");
            return;
        }

        TileBase tile = MostUsedTile(floor) ?? AssetDatabase.LoadAssetAtPath<TileBase>(FallbackTile);
        if (tile == null)
        {
            Debug.LogWarning("Fit floor: no tile to paint with (the floor is empty and " + FallbackTile + " was not found).");
            return;
        }

        Undo.RegisterCompleteObjectUndo(floor, "Fit floor to walls");
        floor.ClearAllTiles();
        BoundsInt area = new BoundsInt(min.x, min.y, 0, max.x - min.x + 1, max.y - min.y + 1, 1);
        TileBase[] tiles = new TileBase[area.size.x * area.size.y];
        for (int i = 0; i < tiles.Length; i++) tiles[i] = tile;
        floor.SetTilesBlock(area, tiles);
        floor.CompressBounds();

        EditorSceneManager.MarkSceneDirty(floor.gameObject.scene);
        Debug.Log("Fit floor: " + area.size.x + "x" + area.size.y + " tiles, from cell (" + min.x + ", " + min.y +
                  ") to (" + max.x + ", " + max.y + ").");
    }

    static TileBase MostUsedTile(Tilemap map)
    {
        Dictionary<TileBase, int> count = new Dictionary<TileBase, int>();
        foreach (Vector3Int p in map.cellBounds.allPositionsWithin)
        {
            TileBase t = map.GetTile(p);
            if (t == null) continue;
            count.TryGetValue(t, out int n);
            count[t] = n + 1;
        }

        TileBase best = null;
        int bestCount = 0;
        foreach (var pair in count)
        {
            if (pair.Value > bestCount) { best = pair.Key; bestCount = pair.Value; }
        }
        return best;
    }
}
