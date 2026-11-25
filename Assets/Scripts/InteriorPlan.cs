using System;
using UnityEngine;

// Which buildings get a room, with which layout. Made as an asset: Create > Otsukai > Interior Plan.
// Otsukai > Town > Build Interiors reads it and builds every room outside the town map.
[CreateAssetMenu(menuName = "Otsukai/Interior Plan", fileName = "InteriorPlan")]
public class InteriorPlan : ScriptableObject
{
    public enum Template { SmallShop, Store, Office, Library, Cafe, Laundry }

    [Serializable]
    public class Room
    {
        [Tooltip("Target Id of the building (from the town layout), e.g. \"Konbini\". Its errand steps happen at the counter inside.")]
        public string buildingId;

        [Tooltip("Shown above the [E] Enter prompt at the door in town.")]
        [GameText] public string placeName;

        public Template template;

        [Tooltip("Floor size in tiles (even width). 0 = the template's size.")]
        public Vector2Int size;
    }

    [Tooltip("Where the first room goes (far outside the town map), and the space between rooms.")]
    public Vector2 firstRoom = new Vector2(-40f, -50f);
    public Vector2 spacing = new Vector2(28f, 20f);
    public int roomsPerRow = 4;

    public Room[] rooms;
}
