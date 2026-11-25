using UnityEngine;

// Walks after the player while a running errand has this as its companion (ErrandData > Companion Id):
// the little sister going to school, a parent to the hospital, the dog on a walk.
// Put it on the NPC (or any sprite). When the errand ends it stays where it is.
public class Follower : MonoBehaviour
{
    [Tooltip("Companion id errands use (e.g. \"Sibling\", \"Pet\"). Empty = the Target Id of the NPC on this object, " +
             "so family members work with Companion Id = \"Giver\".")]
    public string followerId;

    public float speed = 5f;

    [Tooltip("Stays this far behind the player (tiles).")]
    public float keepDistance = 1.2f;

    [Tooltip("Farther than this (e.g. after a loading screen) = jump next to the player.")]
    public float catchUpDistance = 15f;

    Transform player;
    NPC npc;

    string Id => !string.IsNullOrEmpty(followerId) ? followerId : npc != null ? npc.targetId : null;

    // True while a running errand has this as its companion
    public bool IsFollowing
    {
        get
        {
            QuestManager quest = QuestManager.Instance;
            return quest != null && quest.IsCompanion(Id);
        }
    }

    void Awake()
    {
        npc = GetComponent<NPC>();
    }

    void Update()
    {
        if (!IsFollowing) return;

        if (player == null)
        {
            GameObject p = PlayerMovement.FindPlayer();
            if (p == null) return;
            player = p.transform;
        }

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.z = 0f;
        float distance = toPlayer.magnitude;
        if (distance <= keepDistance) return;

        Vector3 spot = player.position - toPlayer.normalized * keepDistance;
        spot.z = transform.position.z;

        // shortcut: walks straight at the player and ignores walls; add pathfinding if companions get stuck
        transform.position = distance > catchUpDistance
            ? spot
            : Vector3.MoveTowards(transform.position, spot, speed * Time.deltaTime);
    }
}
