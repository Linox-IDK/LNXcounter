namespace LNXcounter.Services;

/// <summary>
/// The game only sends ids for Treasure Hunters prizes (no names). This maps the known ids to a drop on the Drops page.
/// The ids for the Pirate Shop Access and for weeks 1 and 2 are the ones published by the community
/// (see the README credits); weeks 3 to 5 are added once they are known.
/// </summary>
public static class TreasureIds
{
    // DelilahPrize:dpid_itemN -> (week, drop slot on the Drops page)
    static readonly Dictionary<string, (int Week, string Drop)> Prizes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DelilahPrize:dpid_item1"]  = (1, "outfit"),
        ["DelilahPrize:dpid_item2"]  = (1, "pickaxe"),
        ["DelilahPrize:dpid_item3"]  = (1, "jam"),
        ["DelilahPrize:dpid_item4"]  = (1, "loading"),
        ["DelilahPrize:dpid_item5"]  = (1, "vehicle"),
        ["DelilahPrize:dpid_item14"] = (2, "outfit"),
        ["DelilahPrize:dpid_item15"] = (2, "pickaxe"),
        ["DelilahPrize:dpid_item16"] = (2, "jam"),
        ["DelilahPrize:dpid_item17"] = (2, "loading"),
        ["DelilahPrize:dpid_item18"] = (2, "vehicle"),
    };

    const string PirateShopId = "9f73f94d-2e61-4523-97d7-d61235d3365e";

    /// <summary>Drop slot ("token", "outfit"...), week (0 for the token) and a readable name for a prize id.</summary>
    public static (string Drop, int Week, string Name) Resolve(string prizeId)
    {
        if (string.Equals(prizeId, PirateShopId, StringComparison.OrdinalIgnoreCase)) return ("token", 0, "Pirate Shop Access");
        if (Prizes.TryGetValue(prizeId, out var p))
        {
            var week = DropsData.Weeks.First(w => w.Number == p.Week);
            return (p.Drop, p.Week, week.Items![p.Drop].Title);
        }
        return ("", 0, "Treasure reward (" + prizeId + ")");
    }
}
