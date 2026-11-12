using System.Globalization;
using System.Text;

namespace LNXcounter.Services;

/// <summary>Chests per day, week or month as a CSV file (opens in Excel / Google Sheets). Times are Paris time, like the rest of the app.</summary>
public static class CsvExport
{
    public enum Period { Daily, Weekly, Monthly }

    /// <summary>Calendar week: starts on Monday.</summary>
    internal static DateTime Start(DateTime d, Period p) => p switch
    {
        Period.Daily => d.Date,
        Period.Weekly => d.Date.AddDays(-(((int)d.DayOfWeek + 6) % 7)),
        _ => new DateTime(d.Year, d.Month, 1),
    };

    internal static string Label(DateTime start, Period p) => p switch
    {
        Period.Daily => start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        Period.Weekly => $"{start:yyyy-MM-dd} to {start.AddDays(6):yyyy-MM-dd}",
        _ => start.ToString("yyyy-MM", CultureInfo.InvariantCulture),
    };

    public static string Build(IEnumerable<MatchRecord> all, int addedByHand, Period period)
    {
        var sep = CultureInfo.CurrentCulture.TextInfo.ListSeparator is { Length: > 0 } s ? s : ",";   // ";" on a French Excel
        var ok = all.Where(m => m.Error == null).ToList();
        var types = ok.SelectMany(m => m.OpenedByType.Keys).Distinct().OrderBy(k => k).ToList();

        string Esc(string v) => v.Contains(sep) || v.Contains('"') || v.Contains('\n') ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(sep, new[] { period == Period.Daily ? "Day" : period == Period.Weekly ? "Week (Mon-Sun)" : "Month",
            "Matches", "Chests opened", "Chests broken", "Average per match" }
            .Concat(types.Select(t => "Opened: " + t)).Concat(new[] { "Treasures found (you)" }).Select(Esc)));

        var groups = ok.GroupBy(m => Start(Paris.FromLocal(m.Started), period)).OrderBy(g => g.Key);
        foreach (var g in groups)
        {
            int opened = g.Sum(m => m.Opened), n = g.Count();
            var finds = g.SelectMany(m => m.Finds).Where(f => f.Mine).Select(f => f.Name).ToList();
            var cells = new List<string>
            {
                Label(g.Key, period), n.ToString(CultureInfo.InvariantCulture), opened.ToString(CultureInfo.InvariantCulture),
                g.Sum(m => m.Broken).ToString(CultureInfo.InvariantCulture), (opened / (double)n).ToString("0.0", CultureInfo.CurrentCulture),
            };
            cells.AddRange(types.Select(t => g.Sum(m => m.OpenedByType.GetValueOrDefault(t)).ToString(CultureInfo.InvariantCulture)));
            cells.Add(string.Join(", ", finds.GroupBy(x => x).Select(x => x.Count() > 1 ? $"{x.Key} x{x.Count()}" : x.Key)));
            sb.AppendLine(string.Join(sep, cells.Select(Esc)));
        }

        // chests added by hand have no date: one line of their own, so the sum of the column is your total
        if (addedByHand > 0)
        {
            var cells = new List<string> { "Added by hand", "", addedByHand.ToString(CultureInfo.InvariantCulture), "", "" };
            cells.AddRange(types.Select(t => t == "normal" ? addedByHand.ToString(CultureInfo.InvariantCulture) : ""));
            cells.Add("");
            sb.AppendLine(string.Join(sep, cells.Select(Esc)));
        }
        return sb.ToString();
    }

    public static void Save(string path, string csv) => File.WriteAllText(path, csv, new UTF8Encoding(true));   // BOM: Excel reads accents correctly
}
