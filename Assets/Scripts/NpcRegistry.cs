using System.Collections.Generic;
using UnityEngine;

// Remembers every NPC that can hand out errands (id -> name), across scenes.
// NPCs add themselves when they appear. Classic and challenge mode pick random givers from here.
public static class NpcRegistry
{
    static readonly Dictionary<string, string> givers = new Dictionary<string, string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        givers.Clear();
    }

    public static void Register(string npcId, string npcName)
    {
        if (string.IsNullOrEmpty(npcId)) return;
        givers[npcId] = npcName;
    }

    public static IEnumerable<string> Ids => givers.Keys;
    public static int Count => givers.Count;

    public static string NameOf(string npcId)
    {
        return npcId != null && givers.TryGetValue(npcId, out string name) ? name : npcId;
    }

    // Shuffled list of NPC ids that aren't waiting for an errand they already gave
    public static List<string> FreeGiversShuffled()
    {
        List<string> list = new List<string>();
        QuestManager quest = QuestManager.Instance;

        foreach (string id in givers.Keys)
        {
            if (quest != null && IsBusy(quest, id)) continue;
            list.Add(id);
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
