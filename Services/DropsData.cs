namespace LNXcounter.Services;

/// <summary>What a drop slot holds in a given week: icon file (in Assets, without .png), hover category and title. Unknown = not revealed yet.</summary>
public record WeekItem(string Icon, string Category, string Title, bool Unknown = false, string? Video = null, string? Song = null);

/// <summary>One mini-season week on the Drops page. HeroIcon is the character shown in the header (a _bw copy exists for ended weeks).</summary>
public record WeekDef(int Number, string Title, string Range, DateTime StartUtc, DateTime EndUtc, string AccentHex, bool Available,
    string HeroIcon = "", Dictionary<string, WeekItem>? Items = null);

/// <summary>
/// A chest drop slot. OneIn is the "1 in N per chest" rate. Kind picks the tile colour (token / solid / purple / blue),
/// Tier is the prize tier (supreme / major / standard). Look = how the picture sits in the frame: fill (cover), outfit (bottom aligned), or a fixed width.
/// </summary>
public record DropDef(string Id, string ListName, int OneIn, string Kind, string Tier, int Row, int Col, int RowSpan, int ColSpan,
    bool Trackable, string Look, double IconWidth = 0, bool Floats = false, bool Listed = true);

public static class DropsData
{
    static DateTime U(int m, int d, int h = 14) => new(2026, m, d, h, 0, 0, DateTimeKind.Utc);

    // Gizmos are the same every week (names from fortnite.bot/treasure)
    static void AddGizmos(Dictionary<string, WeekItem> d)
    {
        d["gizmo1"] = new("gizmo1", "SPRITE GIZMO", "EXTRACTION ACCELERATOR");
        d["gizmo2"] = new("gizmo2", "SPRITE GIZMO", "PORTABLE EXTRACTOR");
        d["gizmo3"] = new("gizmo3", "SPRITE GIZMO", "SPRITE LOCATOR");
        d["gizmo4"] = new("gizmo4", "SPRITE GIZMO", "LUCKY LOCATOR");
        d["gizmo5"] = new("gizmo5", "SPRITE GIZMO", "GOLDEN FIREWORK");
    }

    static Dictionary<string, WeekItem> Week1Items()
    {
        var d = new Dictionary<string, WeekItem>
        {
            ["token"]   = new("token", "CONSUMABLE", "PIRATE SHOP TOKEN"),
            ["outfit"]  = new("outfit_w1", "OUTFIT", "SOLID GOLD A$AP ROCKY"),
            ["pickaxe"] = new("w1_pickaxe", "PICKAXE", "SOLID GOLD A$AP ROCKY'S GAUNTLETS"),
            ["vehicle"] = new("w1_car", "CAR DECAL", "GOLD MERCEDES-AMG G 63 DECAL"),
            ["jam"]     = new("w1_jam", "JAM TRACK", "A$AP ROCKY JAM TRACK", false, "ZMbeyfL7C-A", "No Trespassing · A$AP Rocky"),
            ["loading"] = new("w1_loading", "LOADING SCREEN", "PLUNDER IN PARADISE"),
        };
        AddGizmos(d); return d;
    }

    static Dictionary<string, WeekItem> Week2Items()
    {
        var d = new Dictionary<string, WeekItem>
        {
            ["token"]   = new("token", "CONSUMABLE", "PIRATE SHOP TOKEN"),
            ["outfit"]  = new("outfit_w2", "OUTFIT", "SOLID GOLD LIL UZI VERT"),
            ["pickaxe"] = new("w2_backbling", "BACK BLING", "DIAMOND AND GOLD"),
            ["vehicle"] = new("w2_car", "CAR DECAL", "GOLD CADILLAC CT5-V DECAL"),
            ["jam"]     = new("w2_jam", "JAM TRACK", "LONDON JAM TRACK", false, "mxDfQjae39Y", "London · Lil Uzi Vert"),
            ["loading"] = new("w2_loading", "LOADING SCREEN", "SO KAWAII"),
        };
        AddGizmos(d); return d;
    }

    // Weeks 3 to 5: only the token and the gizmos are known, the rest is still encrypted (shown as "?")
    static Dictionary<string, WeekItem> HiddenItems()
    {
        var d = new Dictionary<string, WeekItem>
        {
            ["token"]   = new("token", "CONSUMABLE", "PIRATE SHOP TOKEN"),
            ["outfit"]  = new("unknown", "OUTFIT", "UNKNOWN", true),
            ["pickaxe"] = new("unknown", "PICKAXE OR BACK BLING", "UNKNOWN", true),
            ["vehicle"] = new("unknown", "CAR DECAL", "UNKNOWN", true),
            ["jam"]     = new("unknown", "JAM TRACK", "UNKNOWN", true),
            ["loading"] = new("unknown", "LOADING SCREEN", "UNKNOWN", true),
        };
        AddGizmos(d); return d;
    }

    // Week boundaries are estimates (assumed 14:00 UTC). Week names come from fortnite.bot/treasure.
    public static readonly List<WeekDef> Weeks = new()
    {
        new(1, "A$AP ARRIVES",        "NOV 1 – 8",      U(11, 1),  U(11, 8),  "#8E73E0", true, "hero_w1", Week1Items()),
        new(2, "LIL UZI ARRIVES",     "NOV 8 – 15",     U(11, 8),  U(11, 15), "#F06AD0", true, "hero_w2", Week2Items()),
        new(3, "MIKU & TETO ARRIVE",  "NOV 15 – 22",    U(11, 15), U(11, 22), "#2EC4B6", true, "hero_w3", HiddenItems()),
        new(4, "OSCAR ARRIVES",       "NOV 22 – 29",    U(11, 22), U(11, 29), "#5B93D6", true, "hero_w4", HiddenItems()),
        new(5, "THE FINAL HUNT",      "NOV 29 – DEC 5", U(11, 29), U(12, 5),  "#D4AF37", true, "hero_w5", HiddenItems()),
    };

    // Rates (per chest) as published for the Treasure Hunters mini season. Same table every week; the items themselves change.
    // Grid is 9 columns x 2 rows.
    public static readonly List<DropDef> Drops = new()
    {
        new("token",   "Pirate Shop Access",           137_500,   "token",  "supreme",  0, 0, 2, 2, true,  "center", 175, Floats: true),
        new("outfit",  "Weekly Gold Outfit",           1_562_500, "solid",  "supreme",  0, 2, 1, 1, true,  "outfit"),
        new("pickaxe", "Weekly Pickaxe or Back Bling", 568,       "purple", "major",    0, 3, 1, 1, true,  "center", 78),
        new("vehicle", "Weekly Vehicle & Decal",       7_813,     "purple", "major",    0, 4, 1, 2, true,  "center", 190),
        new("jam",     "Weekly Jam Track",             391,       "purple", "major",    0, 6, 1, 1, true,  "center", 92),
        new("loading", "Weekly Loading Screen",        272,       "purple", "major",    0, 7, 1, 2, true,  "fill"),
        new("gizmo1",  "Gizmo (Any)",                  127,       "blue",   "standard", 1, 2, 1, 1, false, "center", 74),
        new("gizmo2",  "",                             127,       "blue",   "standard", 1, 3, 1, 1, false, "center", 74, Listed: false),
        new("gizmo3",  "",                             127,       "blue",   "standard", 1, 4, 1, 1, false, "center", 74, Listed: false),
        new("gizmo4",  "",                             127,       "blue",   "standard", 1, 5, 1, 1, false, "center", 74, Listed: false),
        new("gizmo5",  "",                             127,       "blue",   "standard", 1, 6, 1, 1, false, "center", 74, Listed: false),
    };

    /// <summary>"0.79%", "0.013%", "0.00073%": about two significant digits, never scientific notation.</summary>
    public static string Percent(int oneIn)
    {
        double p = 100.0 / oneIn;
        int decimals = Math.Clamp((int)Math.Ceiling(-Math.Log10(p)) + 1, 2, 7);
        return p.ToString("F" + decimals, System.Globalization.CultureInfo.InvariantCulture) + "%";
    }

    public static string Rate(int oneIn) => "1:" + oneIn.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
    public static string OneInText(int oneIn) => Loc.F("1 in {0}", oneIn.ToString("N0", System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>Random Fortnite-flavoured tips for the loading screen preview.</summary>
    public static readonly string[] Tips =
    {
        "Use Preferred Item Slots in Settings to set how weapons and items sort in your Inventory.",
        "Hold the ping button to mark enemies, loot and locations for your squad.",
        "Shields soak up damage before your health does. Top them up before every fight.",
        "Rare chests hold better loot than normal ones, so they are worth a detour.",
        "You can hear a chest before you see it. Listen for the jingle when you run through a building.",
        "Landing away from the busiest spots on the map usually means a calmer start.",
        "Heal before the fight, not during it. Build a wall or find cover first.",
        "Vehicles are a fast way to outrun the storm and cross the map.",
        "Gizmos are the only treasure you can find more than once. Everything else drops a single time.",
        "Treasure Hours boost your chances at the Gold Outfit and the Pirate Shop token.",
        "Pick up a fallen teammate's reboot card and bring it to a Reboot Van to get them back in the match.",
        "Reload when nobody is shooting at you, not in the middle of a fight.",
        "Buried chests look like a mound of sand. Keep an eye out for them while you loot.",
        "Check the map regularly. The storm circle decides where the last fights will happen.",
        "Practice in Creative to sharpen your aim, your building and your edits.",
        "LNXcounter reads your replay files after each match, it never touches the game itself.",
        "Cover your back when you open a chest. The sound can attract company.",
        "Higher ground gives you a clear view and a better angle on your enemies.",
        "Swap your loadout to match the fight. Close quarters and long range need different weapons.",
        "Wait for the storm to close in before leaving a good defensive spot.",
    };
}
