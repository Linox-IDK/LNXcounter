using System.Globalization;
using System.Text;

namespace LNXcounter.Services;

/// <summary>The same table as the CSV export, as a small black-and-gold PDF report. Written by hand (standard Helvetica fonts, no library).</summary>
public static class PdfExport
{
    const double W = 842, H = 595;   // A4 landscape, in points
    record Row(string Label, int Matches, int Opened, int Broken, string Avg, string Finds, bool Blank = false);

    public static byte[] Build(IEnumerable<MatchRecord> all, int addedByHand, CsvExport.Period period)
    {
        var ok = all.Where(m => m.Error == null).ToList();
        var rows = new List<Row>();
        foreach (var g in ok.GroupBy(m => CsvExport.Start(Paris.FromLocal(m.Started), period)).OrderBy(g => g.Key))
        {
            int opened = g.Sum(m => m.Opened), n = g.Count();
            var finds = g.SelectMany(m => m.Finds).Where(f => f.Mine).Select(f => f.Name).GroupBy(x => x)
                .Select(x => x.Count() > 1 ? $"{x.Key} x{x.Count()}" : x.Key);
            rows.Add(new Row(CsvExport.Label(g.Key, period).Replace(" to ", "  >  "), n, opened, g.Sum(m => m.Broken),
                (opened / (double)n).ToString("0.0", CultureInfo.InvariantCulture), string.Join(", ", finds)));
        }
        if (addedByHand > 0) rows.Add(new Row("Added by hand", 0, addedByHand, 0, "", "", true));

        int totalOpened = rows.Sum(r => r.Opened), totalMatches = rows.Sum(r => r.Matches);
        int treasures = ok.SelectMany(m => m.Finds).Count(f => f.Mine);
        string avg = totalMatches > 0 ? (ok.Sum(m => m.Opened) / (double)totalMatches).ToString("0.0", CultureInfo.InvariantCulture) : "0";
        int maxOpened = Math.Max(1, rows.Count == 0 ? 1 : rows.Max(r => r.Opened));
        string what = period == CsvExport.Period.Daily ? "per day" : period == CsvExport.Period.Weekly ? "per week (Monday to Sunday)" : "per month";
        const string gold = "0.831 0.686 0.216", muted = "0.56 0.53 0.41";

        var pages = new List<Page>();
        Page NewPage(bool first)
        {
            var p = new Page(); pages.Add(p);
            p.Rect(0, 0, W, H, "0.039 0.039 0.039");
            p.Rect(0, 0, W, 7, gold);
            if (first)
            {
                p.Text(40, 66, 30, true, gold, "LNXcounter");
                p.Text(40, 88, 12, false, muted, "Chest report, " + what);
                p.TextRight(W - 40, 66, 11, false, muted, "Made " + Paris.FromUtc(DateTime.UtcNow).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " (Paris time)");
                string[] t = { "CHESTS OPENED", "MATCHES", "AVERAGE PER MATCH", "TREASURES FOUND" };
                string[] v = { totalOpened.ToString("N0", CultureInfo.InvariantCulture), totalMatches.ToString(CultureInfo.InvariantCulture), avg, treasures.ToString(CultureInfo.InvariantCulture) };
                for (int i = 0; i < 4; i++)
                {
                    double x = 40 + i * 195;
                    p.Rect(x, 108, 180, 64, "0.078 0.078 0.078");
                    p.Rect(x, 108, 4, 64, gold);
                    p.Text(x + 18, 130, 8.5, true, muted, t[i]);
                    p.Text(x + 18, 160, 24, true, "0.949 0.839 0.459", v[i]);
                }
            }
            return p;
        }

        var page = NewPage(true);
        double y = 204;
        void Header(Page p)
        {
            p.Rect(40, y - 14, W - 80, 24, "0.11 0.10 0.08");
            p.Text(52, y + 2, 9, true, gold, "PERIOD");
            p.TextRight(300, y + 2, 9, true, gold, "MATCHES");
            p.TextRight(380, y + 2, 9, true, gold, "OPENED");
            p.TextRight(450, y + 2, 9, true, gold, "BROKEN");
            p.TextRight(520, y + 2, 9, true, gold, "AVG");
            p.Text(545, y + 2, 9, true, gold, "SHARE");
            p.Text(650, y + 2, 9, true, gold, "TREASURES FOUND");
            y += 28;
        }
        Header(page);
        int alt = 0;
        foreach (var r in rows)
        {
            if (y > H - 46) { page = NewPage(false); y = 44; Header(page); }
            if (alt++ % 2 == 1) page.Rect(40, y - 14, W - 80, 22, "0.075 0.072 0.06");
            const string txt = "0.93 0.90 0.82";
            page.Text(52, y, 10.5, true, r.Blank ? muted : txt, r.Label);
            if (!r.Blank) page.TextRight(300, y, 10.5, false, txt, r.Matches.ToString(CultureInfo.InvariantCulture));
            page.TextRight(380, y, 10.5, true, "0.949 0.839 0.459", r.Opened.ToString("N0", CultureInfo.InvariantCulture));
            if (!r.Blank) page.TextRight(450, y, 10.5, false, txt, r.Broken.ToString(CultureInfo.InvariantCulture));
            if (!r.Blank) page.TextRight(520, y, 10.5, false, txt, r.Avg);
            page.Rect(545, y - 8, 90, 9, "0.16 0.14 0.09");
            page.Rect(545, y - 8, Math.Max(2, 90.0 * r.Opened / maxOpened), 9, gold);
            page.Text(650, y, 9.5, false, "0.62 0.9 0.62", Fit(r.Finds, 9.5, 152));
            y += 22;
        }
        if (rows.Count == 0) page.Text(52, y, 11, false, muted, "No matches counted yet.");

        for (int n = 0; n < pages.Count; n++)
        {
            pages[n].Text(40, H - 18, 8.5, false, "0.45 0.42 0.32", "LNXcounter  -  counted offline from your Fortnite replays");
            pages[n].TextRight(W - 40, H - 18, 8.5, false, "0.45 0.42 0.32", $"Page {n + 1} / {pages.Count}");
        }
        return Assemble(pages);
    }

    public static void Save(string path, byte[] pdf) => File.WriteAllBytes(path, pdf);

    // ----- tiny PDF writer -----
    sealed class Page
    {
        public readonly StringBuilder S = new();
        static string N(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        public void Rect(double x, double top, double w, double h, string rgb) => S.Append($"{rgb} rg {N(x)} {N(H - top - h)} {N(w)} {N(h)} re f\n");
        public void Text(double x, double baseline, double size, bool bold, string rgb, string s)
        {
            if (s.Length == 0) return;
            S.Append($"BT /{(bold ? "F2" : "F1")} {N(size)} Tf {rgb} rg {N(x)} {N(H - baseline)} Td ({Esc(s)}) Tj ET\n");
        }
        public void TextRight(double right, double baseline, double size, bool bold, string rgb, string s) => Text(right - Width(s, size, bold), baseline, size, bold, rgb, s);
    }

    static string Esc(string s)
    {
        var sb = new StringBuilder();
        foreach (var c in s) sb.Append(c is '\\' or '(' or ')' ? "\\" + c : c is >= ' ' and <= '~' ? c.ToString() : "?");
        return sb.ToString();
    }

    static double Width(string s, double size, bool bold = false)
    {
        double w = 0;
        foreach (var c in s) w += char.IsDigit(c) ? 0.556 : c is ' ' or '.' or ',' or ':' or '/' or 'i' or 'l' or 'j' or 't' or 'f' or 'I' ? 0.28 : char.IsUpper(c) ? 0.69 : c is 'm' or 'w' or 'M' or 'W' ? 0.8 : 0.53;
        return w * size * (bold ? 1.06 : 1);
    }

    static string Fit(string s, double size, double max)
    {
        if (Width(s, size) <= max) return s;
        while (s.Length > 1 && Width(s + "...", size) > max) s = s[..^1];
        return s + "...";
    }

    static byte[] Assemble(List<Page> pages)
    {
        var enc = Encoding.Latin1;
        var objs = new List<string>();
        var kids = string.Join(" ", pages.Select((_, i) => $"{5 + i * 2} 0 R"));
        objs.Add("<< /Type /Catalog /Pages 2 0 R >>");
        objs.Add($"<< /Type /Pages /Kids [{kids}] /Count {pages.Count} >>");
        objs.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        objs.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");
        for (int i = 0; i < pages.Count; i++)
        {
            objs.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {W} {H}] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {6 + i * 2} 0 R >>");
            var c = pages[i].S.ToString();
            objs.Add($"<< /Length {enc.GetByteCount(c)} >>\nstream\n{c}endstream");
        }
        using var ms = new MemoryStream();
        void Put(string s) { var b = enc.GetBytes(s); ms.Write(b, 0, b.Length); }
        Put("%PDF-1.4\n");
        var offs = new List<long>();
        for (int i = 0; i < objs.Count; i++) { offs.Add(ms.Position); Put($"{i + 1} 0 obj\n{objs[i]}\nendobj\n"); }
        long xref = ms.Position;
        Put($"xref\n0 {objs.Count + 1}\n0000000000 65535 f \n");
        foreach (var o in offs) Put($"{o:0000000000} 00000 n \n");
        Put($"trailer\n<< /Size {objs.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return ms.ToArray();
    }
}
