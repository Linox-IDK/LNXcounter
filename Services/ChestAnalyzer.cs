using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using FortniteReplayReader.Models.NetFieldExports;
using FortniteReplayReader.Models.NetFieldExports.RPC;
using Unreal.Core;
using Unreal.Core.Contracts;
using Unreal.Core.Models;
using Unreal.Core.Models.Enums;

namespace LNXcounter.Services;

/// <summary>Reads a Fortnite .replay file (offline, file only) and counts the chests the recording player opened or broke.</summary>
public static class ChestAnalyzer
{
    public static MatchRecord Analyze(string path)
    {
        var rec = new MatchRecord { File = Path.GetFileName(path), Started = StartFromName(path) };
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var reader = new ChestReader();
            reader.ReadReplay(fs);
            reader.Fill(rec);
        }
        catch (Exception e) { rec.Error = e.GetType().Name + ": " + e.Message; }
        return rec;
    }

    static DateTime StartFromName(string path)
    {
        var m = Regex.Match(Path.GetFileName(path), @"(\d{4})\.(\d{2})\.(\d{2})-(\d{2})\.(\d{2})\.(\d{2})");
        if (m.Success && DateTime.TryParse($"{m.Groups[1]}-{m.Groups[2]}-{m.Groups[3]} {m.Groups[4]}:{m.Groups[5]}:{m.Groups[6]}", out var d)) return d;
        return File.GetCreationTime(path);
    }

    sealed class Track { public uint Actor; public string Class = ""; public bool Searched; public double SearchedAt; public uint? LastPawn, PawnAtOpen; }

    sealed class ChestReader : FortniteReplayReader.ReplayReader
    {
        static readonly HashSet<string> Fields = new() { "bAlreadySearched", "SearchingPawn", "SearchedMesh", "SearchAnimationCount" };
        readonly Dictionary<string, bool> groupCache = new();
        readonly Dictionary<uint, uint> chanToActor = new(), pawnToPS = new();
        readonly Dictionary<uint, Track> tracks = new();
        readonly List<(uint hitter, uint target, bool fatal, double t)> hits = new();
        uint? recorder; double now;

        public ChestReader() : base(null!, ParseMode.Full) { }

        protected override void OnChannelOpened(uint ch, NetworkGUID? actor) { base.OnChannelOpened(ch, actor); if (actor != null) chanToActor[ch] = actor.Value; }
        protected override void OnChannelClosed(uint ch, NetworkGUID? actor) { chanToActor.Remove(ch); base.OnChannelClosed(ch, actor); }

        protected override void OnExportRead(uint ch, INetFieldExportGroup? g)
        {
            if (g is GameState gs)
            {
                if (recorder == null && gs.RecorderPlayerState?.Value is uint v && v != 0) recorder = v;
                if (gs.ReplicatedWorldTimeSecondsDouble is double d && d > 0) now = d;
                else if (gs.ReplicatedWorldTimeSeconds is float s && s > 0) now = s;
            }
            else if (g is PlayerPawn pp && pp.PlayerState is uint ps && ps != 0 && chanToActor.TryGetValue(ch, out var a)) pawnToPS[a] = ps;
            else if (g is BatchedDamageCues dc && chanToActor.TryGetValue(ch, out var hitter))
            {
                if (dc.NonPlayerHitActor is uint t1 && t1 != 0) hits.Add((hitter, t1, dc.NonPlayerbIsFatal == true, now));
                if (dc.HitActor is uint t2 && t2 != 0) hits.Add((hitter, t2, dc.bIsFatal == true, now));
            }
            base.OnExportRead(ch, g);
        }

        bool IsContainer(NetFieldExportGroup g)
        {
            if (groupCache.TryGetValue(g.PathName, out var v) && v) return true;
            bool yes = g.NetFieldExports.Any(e => e != null && Fields.Contains(e.Name));
            if (yes) groupCache[g.PathName] = true;
            return yes;
        }

        public override bool ReceiveProperties(FBitArchive ar, NetFieldExportGroup group, uint ch, [NotNullWhen(true)] out INetFieldExportGroup? export, bool checksum = true, bool delta = false)
        {
            if (delta || !IsContainer(group)) return base.ReceiveProperties(ar, group, ch, out export, checksum, delta);
            export = null;
            if (Channels[ch] == null || !chanToActor.TryGetValue(ch, out var actor)) return false;
            if (checksum) ar.ReadBit();
            if (!tracks.TryGetValue(actor, out var t)) tracks[actor] = t = new Track { Actor = actor, Class = group.PathName };
            while (true)
            {
                uint idx = ar.ReadIntPacked(); if (idx == 0) break; idx--;
                if (!group.IsValidIndex(idx)) return false;
                var fe = group.NetFieldExports[idx]; int bits = (int)ar.ReadIntPacked(); if (bits == 0) continue;
                if (fe == null || ar.GetBitsLeft() < bits) { ar.SkipBits(bits); continue; }
                switch (fe.Name)
                {
                    case "bAlreadySearched":
                    {
                        bool b = ar.ReadBit(); if (bits > 1) ar.SkipBits(bits - 1);
                        if (b && !t.Searched) { t.Searched = true; t.SearchedAt = now; t.PawnAtOpen = t.LastPawn; }
                        break;
                    }
                    case "SearchingPawn":
                    {
                        uint p = new NetBitReader(ar.ReadBits(bits), bits).SerializePropertyObject();
                        if (p != 0) { t.LastPawn = p; if (t.Searched) t.PawnAtOpen ??= p; }
                        break;
                    }
                    default: ar.SkipBits(bits); break;
                }
            }
            return true;
        }

        static string Classify(string path)
        {
            var p = path.ToLowerInvariant(); var n = path.Split('.').Last();
            if (p.Contains("ammo") || !p.Contains("chest")) return "";
            if (p.Contains("buriedchest")) return "buried";
            if (p.Contains("sprite")) return p.Contains("jewel") || p.Contains("gem") ? "sprite_jewel" : "sprite";
            if (p.Contains("relic")) return "relic";
            if (p.Contains("rarechest") || n.Contains("Rare")) return "rare";
            return n.StartsWith("Tiered_Chest_Athena") || n.StartsWith("AlwaysSpawn_NormalChest") || n.StartsWith("Creative_Tiered_Chest") ? "normal" : "other";
        }

        public void Fill(MatchRecord rec)
        {
            var mine = pawnToPS.Where(kv => kv.Value == recorder).Select(kv => kv.Key).ToHashSet();
            var myKills = new Dictionary<uint, double>();
            foreach (var h in hits) if (h.fatal && mine.Contains(h.hitter) && !myKills.ContainsKey(h.target)) myKills[h.target] = h.t;
            foreach (var t in tracks.Values)
            {
                var type = Classify(t.Class); if (type == "") continue;
                bool opened = t.Searched && t.PawnAtOpen is uint p && mine.Contains(p);
                bool broke = !opened && myKills.TryGetValue(t.Actor, out var kt) && (!t.Searched || t.SearchedAt > kt);
                if (opened) { rec.Opened++; rec.OpenedByType[type] = rec.OpenedByType.GetValueOrDefault(type) + 1; }
                else if (broke) rec.Broken++;
            }
        }
    }
}
