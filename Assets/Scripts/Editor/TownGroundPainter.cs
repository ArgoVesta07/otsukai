using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// Otsukai > Town > Paint Ground And Roads: paints the "Ground" and "Road" tilemaps of the open scene from
// Design/town-v2/Town_Map_v2_ground.csv (written by town_map.py). Both tilemaps are cleared first; Undo works.
// Pick the tiles in the window. Defaults are the placeholder tiles of the "Town" palette.
public class TownGroundPainter : EditorWindow
{
    const string CsvPath = "Design/town-v2/Town_Map_v2_ground.csv";
    const string TileFolder = "Assets/Art/Tilesets/TIles/";

    TileBase groundTile;   // "ground" row: whole map, on the Ground tilemap
    TileBase roadTile;     // road, plaza, arcade, path, lot, bridge
    TileBase waterTile;    // river
    TileBase railTile;     // railway
    // park, field and hill stay grass: they clear the Road tilemap

    [MenuItem("Otsukai/Town/Paint Ground And Roads")]
    static void Open()
    {
        TownGroundPainter w = GetWindow<TownGroundPainter>("Paint Town");
        w.groundTile = AssetDatabase.LoadAssetAtPath<TileBase>(TileFolder + "Tile_Grass_1x1_0.asset");
        w.roadTile = AssetDatabase.LoadAssetAtPath<TileBase>(TileFolder + "Tile_Road_1x1_0.asset");
        w.waterTile = AssetDatabase.LoadAssetAtPath<TileBase>(TileFolder + "Tile_Floor_1x1_0.asset");
        w.railTile = w.waterTile;
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox("Clears the Ground and Road tilemaps, then paints them from " + CsvPath + ".", MessageType.Info);
        groundTile = (TileBase)EditorGUILayout.ObjectField("Ground (grass)", groundTile, typeof(TileBase), false);
        roadTile = (TileBase)EditorGUILayout.ObjectField("Road / path / plaza", roadTile, typeof(TileBase), false);
        waterTile = (TileBase)EditorGUILayout.ObjectField("River", waterTile, typeof(TileBase), false);
        railTile = (TileBase)EditorGUILayout.ObjectField("Railway", railTile, typeof(TileBase), false);

        if (GUILayout.Button("Paint")) Paint();
    }

    void Paint()
    {
        Tilemap ground = FindTilemap("Ground");
        Tilemap road = FindTilemap("Road");
        if (ground == null || road == null) return;

        if (!File.Exists(CsvPath))
        {
            Debug.LogWarning("Town paint: " + CsvPath + " not found. Run town_map.py first.");
            return;
        }

        if (!EditorUtility.DisplayDialog("Paint town", "This clears the Ground and Road tilemaps and paints them again. " +
                                         "You can undo it with Ctrl+Z.", "Paint", "Cancel"))
            return;

        Undo.RegisterCompleteObjectUndo(new Object[] { ground, road }, "Paint town");
        ground.ClearAllTiles();
        road.ClearAllTiles();

        string[] lines = File.ReadAllLines(CsvPath);
        int painted = 0;
        for (int i = 1; i < lines.Length; i++)
        {
            string[] v = lines[i].Split(',');
            if (v.Length < 5) continue;

            BoundsInt area = new BoundsInt(Int(v[1]), Int(v[2]), 0, Int(v[3]), Int(v[4]), 1);
            string kind = v[0].Trim();

            if (kind == "ground") Fill(ground, area, groundTile);
            else Fill(road, area, TileFor(kind));
            painted++;
        }

        EditorSceneManager.MarkSceneDirty(ground.gameObject.scene);
        Debug.Log("Town paint: " + painted + " areas painted from " + CsvPath + ".");
    }

    TileBase TileFor(string kind)
    {
        switch (kind)
        {
            case "river": return waterTile;
            case "rail": return railTile;
            case "park":
            case "field":
            case "hill": return null;   // grass from the Ground tilemap shows through
            default: return roadTile;
        }
    }

    static void Fill(Tilemap map, BoundsInt area, TileBase tile)
    {
        TileBase[] tiles = new TileBase[area.size.x * area.size.y];
        for (int i = 0; i < tiles.Length; i++) tiles[i] = tile;
        map.SetTilesBlock(area, tiles);
    }

    static Tilemap FindTilemap(string objectName)
    {
        GameObject go = GameObject.Find(objectName);
        Tilemap map = go != null ? go.GetComponent<Tilemap>() : null;
        if (map == null) Debug.LogWarning("Town paint: no active Tilemap named '" + objectName + "' in the open scene.");
        return map;
    }

    static int Int(string s)
    {
        return int.Parse(s.Trim(), CultureInfo.InvariantCulture);
    }
}
