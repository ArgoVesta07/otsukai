using System.Collections.Generic;
using UnityEngine;

// Family = gives otsukai (main errands, a star above the head).
// Townsperson = gives requests (side errands, "!").
public enum NpcRole { Townsperson, Family }

// Remembers every NPC that can hand out errands, across scenes.
// NPCs add themselves when they appear. Classic and challenge mode pick random givers from here.
public static class NpcRegistry
{
    class Info
    {
        public string name;
        public NpcRole role;
        public string awayNavId;   // where the arrow points when the NPC is in another scene
    }

    static readonly Dictionary<string, Info> givers = new Dictionary<string, Info>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        givers.Clear();
    }

    public static void Register(string npcId, string npcName, NpcRole role, string awayNavId)
    {
        if (string.IsNullOrEmpty(npcId)) return;
        givers[npcId] = new Info { name = npcName, role = role, awayNavId = awayNavId };
    }

    public static IEnumerable<string> Ids => givers.Keys;
    public static int Count => givers.Count;

    public static int CountOf(NpcRole role)
    {
        int n = 0;
        foreach (Info info in givers.Values)
        {
            if (info.role == role) n++;
        }
        return n;
    }

    public static string NameOf(string npcId)
    {
        return npcId != null && givers.TryGetValue(npcId, out Info info) ? info.name : npcId;
    }

    // NavTarget id for an NPC: its own id if it's in this scene, otherwise its Away Nav Id (e.g. "Home")
    public static string NavIdFor(string npcId)
    {
        if (string.IsNullOrEmpty(npcId) || NavTarget.Find(npcId) != null) return npcId;
        return givers.TryGetValue(npcId, out Info info) && !string.IsNullOrEmpty(info.awayNavId) ? info.awayNavId : npcId;
    }

    // Shuffled ids of NPCs with this role that aren't waiting for an errand they already gave
    public static List<string> FreeGiversShuffled(NpcRole role)
    {
        List<string> list = new List<string>();
        QuestManager quest = QuestManager.Instance;

        foreach (KeyValuePair<string, Info> pair in givers)
        {
            if (pair.Value.role != role) continue;
            if (quest != null && IsBusy(quest, pair.Key)) continue;
            list.Add(pair.Key);
        }

        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            string tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }

        return list;
    }

    static bool IsBusy(QuestManager quest, string npcId)
    {
        foreach (ActiveErrand e in quest.Active)
        {
            if (e.giverId == npcId) return true;
        }
        return false;
    }
}
