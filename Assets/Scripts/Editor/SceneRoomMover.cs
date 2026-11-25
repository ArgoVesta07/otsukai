using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

// Otsukai > Town > Move House And Supermarket Into Town: copies what is inside the House and Supermarket scenes
// into the open town scene as two rooms outside the map (under "Rooms From Scenes"), so the player never changes
// scene. For each one:
//   - every object except the camera, the player and the EventSystem moves over, keeping its layout
//   - a RoomArea with the camera bounds that scene used, and a NavTarget with the building id in the middle
//   - its exit Door becomes Stay In Scene back to "<id>/Out"; its "Entrance" spawn point becomes "<id>/In"
//   - the town door (in the House / Supermarket prefab) becomes Stay In Scene to "<id>/In", with "<id>/Out" next to it
// The old scene files are not changed (closed without saving). Commit before running: undo across scenes is unreliable.
public static class SceneRoomMover
{
    const string RootName = "Rooms From Scenes";

    struct Source { public string scene, id, prefab, prefabDoor; public Vector2 slot; }

    static readonly Source[] Sources =
    {
        new Source { scene = "Assets/Scenes/House.unity", id = "Home", prefab = "Assets/Prefab/House.prefab", prefabDoor = "door", slot = new Vector2(80f, -50f) },
        new Source { scene = "Assets/Scenes/Supermarket.unity", id = "Supermarket", prefab = "Assets/Prefab/Supermarket.prefab", prefabDoor = "door supa", slot = new Vector2(80f, -80f) },
    };

    [MenuItem("Otsukai/Town/Move House And Supermarket Into Town")]
    static void Move()
    {
        Scene town = SceneManager.GetActiveScene();
        if (GameObject.Find("Town Layout") == null)
        {
            Debug.LogWarning("Rooms: open MainScene first.");
            return;
        }
        if (GameObject.Find(RootName) != null)
        {
            Debug.LogWarning("Rooms: '" + RootName + "' already exists. Delete it first to move the scenes again.");
            return;
        }
        if (!EditorUtility.DisplayDialog("Move rooms into town",
                "Copies House and Supermarket into this scene as rooms and changes their doors. Commit first: this cannot be undone with Ctrl+Z.",
                "Move", "Cancel"))
            return;

        GameObject root = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(root, town);

        foreach (Source src in Sources)
        {
            Scene scene = EditorSceneManager.OpenScene(src.scene, OpenSceneMode.Additive);
            GameObject room = new GameObject("Room " + src.id);
            SceneManager.MoveGameObjectToScene(room, town);
            room.transform.SetParent(root.transform, false);

            // Camera bounds the scene used, read before its camera is left behind
            Vector2 center = Vector2.zero, size = new Vector2(22f, 12f);
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                CameraFollow follow = go.GetComponentInChildren<CameraFollow>(true);
                if (follow == null) continue;
                SerializedObject so = new SerializedObject(follow);
                center = so.FindProperty("boundsCenter").vector2Value;
                size = so.FindProperty("boundsSize").vector2Value;
            }

            int moved = 0;
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (go.GetComponentInChildren<Camera>(true) != null || go.GetComponentInChildren<PlayerMovement>(true) != null ||
                    go.GetComponentInChildren<EventSystem>(true) != null)
                    continue;
                SceneManager.MoveGameObjectToScene(go, town);
                go.transform.SetParent(room.transform, true);   // room is at (0, 0): world positions stay the same
                moved++;
            }

            // Doors and spawn points inside the room
            foreach (Door d in room.GetComponentsInChildren<Door>(true))
            {
                d.stayInScene = true;
                d.sceneToLoad = "";
                d.spawnAtTarget = src.id + "/Out";
                d.spawnOffset = new Vector2(0f, -1f);
            }
            foreach (NavTarget t in room.GetComponentsInChildren<NavTarget>(true))
                if (t.targetId == "Entrance") t.targetId = src.id + "/In";

            RoomArea area = room.AddComponent<RoomArea>();
            area.size = size;
            area.centerOffset = center;
            GameObject mid = new GameObject(src.id + " (inside)");
            mid.transform.SetParent(room.transform, false);
            mid.transform.localPosition = center;
            mid.AddComponent<NavTarget>().targetId = src.id;   // the arrow inside the building points into the room

            room.transform.position = src.slot;   // move everything outside the town map
            EditorSceneManager.CloseScene(scene, true);   // not saved: the old scene file stays as it was

            SetTownDoor(src);
            Debug.Log("Rooms: " + src.id + " moved (" + moved + " objects) to " + src.slot + ".");
        }

        EditorSceneManager.MarkSceneDirty(town);
    }

    // The door in the building prefab: into the room, with a spawn point for coming back out
    static void SetTownDoor(Source src)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(src.prefab);
        try
        {
            Transform doorT = null;
            foreach (Transform t in contents.GetComponentsInChildren<Transform>(true))
                if (t.name == src.prefabDoor) { doorT = t; break; }
            Door d = doorT != null ? doorT.GetComponent<Door>() : null;
            if (d == null)
            {
                Debug.LogWarning("Rooms: no Door on '" + src.prefabDoor + "' in " + src.prefab + ".");
                return;
            }
            d.stayInScene = true;
            d.sceneToLoad = "";
            d.spawnAtTarget = src.id + "/In";
            d.spawnOffset = Vector2.zero;

            if (doorT.Find("Out") == null)
            {
                GameObject outGo = new GameObject("Out");
                outGo.transform.SetParent(doorT, false);
                outGo.AddComponent<NavTarget>().targetId = src.id + "/Out";
            }
            PrefabUtility.SaveAsPrefabAsset(contents, src.prefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }
}
