using System.Collections.Generic;

// Story flags: simple "name -> number" values (0 = off). Examples: "MetGrandma" = 1, "BridgeRepairs" = 2.
// Dialogue entries and errands read and set these. The save system will store them later.
public static class GameFlags
{
    static readonly Dictionary<string, int> values = new Dictionary<string, int>();

    public static void Set(string flag, int value = 1)
    {
        if (string.IsNullOrEmpty(flag)) return;
        values[flag] = value;
    }

    public static int Get(string flag)
    {
        if (string.IsNullOrEmpty(flag)) return 0;
        return values.TryGetValue(flag, out int value) ? value : 0;
    }

    // True when the flag exists and is not 0
    public static bool Has(string flag)
    {
        return Get(flag) != 0;
    }

    public static void Clear()
    {
        values.Clear();
    }

    // Read-only view, for the save system and debug tools
    public static IReadOnlyDictionary<string, int> All => values;
}
