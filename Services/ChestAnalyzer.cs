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
    /// <summary>Raise when the analysis learns something new: matches whose replay is still on disk are then read again.</summary>
    public const int Version = 3;   // 2 = treasure finds (golden chest prizes, gizmos), 3 = where each chest was opened

    public static MatchRecord Analyze(string path)
    {
        var rec = new MatchRecord { File = Path.GetFileName(path), Started = StartFromName(path), Analysis = Version };
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

    sealed class Track { public uint Actor; public string Class = ""; public bool Searched; public double SearchedAt; public uint? LastPawn, PawnAtOpen; public (double X, double Y)? PawnSpot; public bool Destroyed; public double DestroyedAt; public uint? Destroyer; }

    sealed class ChestReader : FortniteReplayReader.ReplayReader
    {
        static readonly HashSet<string> Fields = new() { "bAlreadySearched", "SearchingPawn", "SearchedMesh", "SearchAnimationCount", "bDestroyed" };
        readonly Dictionary<string, bool> groupCache = new();
        readonly Dictionary<uint, uint> chanToActor = new(), pawnToPS = new();
        readonly Dictionary<uint, Track> tracks = new();
        readonly List<(uint hitter, uint target, bool fatal, double t)> hits = new();
        uint? recorder; double now, firstTime;
        readonly Dictionary<uint, (double X, double Y)> chanSpot = new(), pawnPos = new();   // chest spawn spot by actor, last seen spot of each pawn by actor

        // ---- treasure grants: every treasure the recorder receives adds a "GA_TreasureHandler_*" ability to their ability system ----
        readonly List<(double At, uint Actor, int Element)> grants = new();
        NetGuidCache? guidCache;
        string PathOfGuid(uint g)
        {
            try
            {
                guidCache ??= (NetGuidCache)typeof(Unreal.Core.ReplayReader<FortniteReplayReader.Models.FortniteReplay>).GetField("_netGuidCache", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(this)!;
                return guidCache.NetGuidToPathName.TryGetValue(g, out var p) ? p : "";
            }
            catch { return ""; }
        }

        public override bool ReadFieldHeaderAndPayload(FBitArchive archive, NetFieldExportGroup group, out NetFieldExport outField, out uint? payload)
        {
            bool ok = base.ReadFieldHeaderAndPayload(archive, group, out outField, out payload);
            if (ok && payload is uint nb && nb >= 129 && nb < 20000 && outField?.Name == "ActivatableAbilities"
                && group.PathName.Contains("AbilitySystemComponent", StringComparison.Ordinal))
            {
                try
                {
                    int start = (int)archive.Position;
                    var data = archive.ReadBits((int)nb).ToArray();
                    archive.Seek(start, SeekOrigin.Begin);
                    DecodeGrants(data, (int)nb);
                }
                catch { }
            }
            return ok;
        }

        /// <summary>A FastArray delta of the ability list: header, then for each changed ability its id and raw properties. One that points at a treasure handler class = one treasure given.</summary>
        void DecodeGrants(byte[] data, int bits)
        {
            int pos = 1;
            int Bit() => pos < bits ? (data[pos >> 3] >> (pos & 7)) & 1 : throw new IndexOutOfRangeException();
            uint Read(int n) { uint v = 0; for (int k = 0; k < n; k++) { v |= (uint)Bit() << k; pos++; } return v; }
            uint Packed() { uint v = 0; for (int sh = 0; sh < 35; sh += 7) { var b = Read(8); v |= (b >> 1) << sh; if ((b & 1) == 0) return v; } throw new InvalidDataException(); }
            try
            {
                _ = Read(32); _ = Read(32);
                var deletes = Read(32); var changed = Read(32);
                if (deletes > 512 || changed > 512) return;
                pos += (int)deletes * 32;
                for (int i = 0; i < changed; i++)
                {
                    int element = (int)Read(32);
                    while (true)
                    {
                        var handle = Packed(); if (handle == 0) break;
                        int nb = (int)Packed();
                        if (nb < 0 || pos + nb > bits) return;
                        if (nb is 8 or 16 or 24 or 32 or 40)
                        {
                            int st = pos; uint guid = 0;
                            for (int sh = 0; pos < st + nb; sh += 7) { var b = Read(8); guid |= (b >> 1) << sh; if ((b & 1) == 0) break; }
                            pos = st + nb;
                            if (guid > 0 && PathOfGuid(guid).Contains("TreasureHandler", StringComparison.Ordinal))
                            {
                                uint actor = 0;
                                if (curChannel != 0 && chanToActor.TryGetValue(curChannel, out var a)) actor = a;
                                if (!grants.Any(g => g.Actor == actor && g.Element == element && now - g.At < 60)) grants.Add((now, actor, element));
                            }
                        }
                        else pos += nb;
                    }
                }
            }
            catch (Exception e) when (e is IndexOutOfRangeException or InvalidDataException) { }
        }
        uint curChannel;
        public override bool ReceivedReplicatorBunch(DataBunch bunch, FBitArchive archive, uint? repObject, bool bHasRepLayout)
        {
            curChannel = (uint)bunch.ChIndex;
            return base.ReceivedReplicatorBunch(bunch, archive, repObject, bHasRepLayout);
        }

        public ChestReader() : base(null!, ParseMode.Full) { }

        protected override void OnChannelOpened(uint ch, NetworkGUID? actor)
        {
            base.OnChannelOpened(ch, actor);
            if (actor == null) return;
            chanToActor[ch] = actor.Value;
            try { if (Channels[ch]?.Actor?.Location is { } l && (l.X != 0 || l.Y != 0)) chanSpot[actor.Value] = (l.X, l.Y); } catch { }
        }
        protected override void OnChannelClosed(uint ch, NetworkGUID? actor) { chanToActor.Remove(ch); base.OnChannelClosed(ch, actor); }

        protected override void OnExportRead(uint ch, INetFieldExportGroup? g)
        {
            if (g is GameState gs)
            {
                if (recorder == null && gs.RecorderPlayerState?.Value is uint v && v != 0) recorder = v;
                if (gs.ReplicatedWorldTimeSecondsDouble is double d && d > 0) now = d;
                else if (gs.ReplicatedWorldTimeSeconds is float s && s > 0) now = s;
                if (firstTime == 0 && now > 0) firstTime = now;
            }
            else if (g is PlayerPawn pp && chanToActor.TryGetValue(ch, out var a))
            {
                if (pp.PlayerState is uint ps && ps != 0) pawnToPS[a] = ps;
                var loc = pp.ReplicatedMovement?.Location ?? pp.Location;
                if (loc != null && (loc.X != 0 || loc.Y != 0)) pawnPos[a] = (loc.X, loc.Y);
            }
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

        // ---- treasure hunt: gizmos (they appear when a player equips one) and golden chest prizes ----
        static readonly Regex GizmoActor = new(@"RelicExtractor|RelicFinder|LuckyLocator|FlameCeleb|SmugglerExtractionKey|ExtractionAccelerat", RegexOptions.IgnoreCase);
        static readonly Regex PrizeText = new(@"DelilahPrize:dpid_item\d+|[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
        sealed record Gizmo(string Name, uint? Holder, double At);
        sealed record Prize(string Id, uint? Opener, double At);
        readonly Dictionary<uint, Gizmo> gizmos = new();   // one per actor channel
        readonly Dictionary<uint, Prize> prizes = new();

        static bool IsPrizeGroup(string path) => path.Contains("TreasureHunt", StringComparison.OrdinalIgnoreCase) && path.Contains("Chest", StringComparison.OrdinalIgnoreCase);
        static bool IsGizmoGroup(string path) => path.Contains("/B_", StringComparison.Ordinal) && GizmoActor.IsMatch(path) && !path.Contains("Consumable", StringComparison.OrdinalIgnoreCase);

        static string GizmoName(string path)
        {
            if (path.Contains("RelicExtractor", StringComparison.OrdinalIgnoreCase)) return "Portable Extractor";
            if (path.Contains("RelicFinder", StringComparison.OrdinalIgnoreCase)) return "Sprite Locator";
            if (path.Contains("LuckyLocator", StringComparison.OrdinalIgnoreCase)) return "Lucky Locator";
            if (path.Contains("FlameCeleb", StringComparison.OrdinalIgnoreCase)) return "Golden Firework";
            if (path.Contains("ExtractionAccelerat", StringComparison.OrdinalIgnoreCase)) return "Extraction Accelerator";
            return "Smuggler Extraction Key";
        }

        /// <summary>Reads the fields we need from a group the library does not decode: object references and the raw text of the prize id.</summary>
        bool ReadTreasureGroup(FBitArchive ar, NetFieldExportGroup group, uint ch, bool checksum, bool prize)
        {
            if (Channels[ch] == null || !chanToActor.TryGetValue(ch, out _)) return false;
            if (checksum) ar.ReadBit();
            uint? holder = null; string? prizeId = null; bool? success = null;
            while (true)
            {
                uint idx = ar.ReadIntPacked(); if (idx == 0) break; idx--;
                if (!group.IsValidIndex(idx)) return false;
                var fe = group.NetFieldExports[idx]; int bits = (int)ar.ReadIntPacked(); if (bits == 0) continue;
                if (fe == null || ar.GetBitsLeft() < bits) { ar.SkipBits(bits); continue; }
                switch (fe.Name)
                {
                    case "Instigator": case "OpeningPawn": case "Owner":
                    {
                        uint p = new NetBitReader(ar.ReadBits(bits), bits).SerializePropertyObject();
                        if (p != 0 && (holder == null || fe.Name != "Owner")) holder = p;
                        break;
                    }
                    case "bSuccess": { success = ar.ReadBit(); if (bits > 1) ar.SkipBits(bits - 1); break; }
                    case "PrizeTemplateId":
                    {
                        var bytes = ar.ReadBits(bits);
                        var sb = new System.Text.StringBuilder(bytes.Length);
                        foreach (var b in bytes) sb.Append(b is >= 32 and < 127 ? (char)b : ' ');
                        var text = sb.ToString();
                        var m = PrizeText.Match(text); if (m.Success) prizeId = m.Value;
                        break;
                    }
                    default: ar.SkipBits(bits); break;
                }
            }
            if (prize) { if (prizeId != null && success != false && !prizes.ContainsKey(ch)) prizes[ch] = new Prize(prizeId, holder, now); }
            else if (!gizmos.TryGetValue(ch, out var g) || (g.Holder == null && holder != null)) gizmos[ch] = new Gizmo(GizmoName(group.PathName), holder, g?.At ?? now);
            return true;
        }

        public override bool ReceiveProperties(FBitArchive ar, NetFieldExportGroup group, uint ch, [NotNullWhen(true)] out INetFieldExportGroup? export, bool checksum = true, bool delta = false)
        {
            if (!delta)
            {
                bool prizeGroup = IsPrizeGroup(group.PathName);
                if (prizeGroup || IsGizmoGroup(group.PathName)) { export = null; return ReadTreasureGroup(ar, group, ch, checksum, prizeGroup); }
            }
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
                        if (b && !t.Searched) { t.Searched = true; t.SearchedAt = now; t.PawnAtOpen = t.LastPawn; if (t.LastPawn is uint lp && pawnPos.TryGetValue(lp, out var sp)) t.PawnSpot = sp; }
                        break;
                    }
                    case "bDestroyed":
                    {
                        bool b = ar.ReadBit(); if (bits > 1) ar.SkipBits(bits - 1);
                        if (b && !t.Destroyed) { t.Destroyed = true; t.DestroyedAt = now; t.Destroyer = t.LastPawn; if (t.LastPawn is uint dp && pawnPos.TryGetValue(dp, out var dsp)) t.PawnSpot ??= dsp; }
                        break;
                    }
                    case "SearchingPawn":
                    {
                        uint p = new NetBitReader(ar.ReadBits(bits), bits).SerializePropertyObject();
                        if (p != 0) { t.LastPawn = p; if (t.Destroyed) { t.Destroyer ??= p; if (t.PawnSpot == null && pawnPos.TryGetValue(p, out var dsp2)) t.PawnSpot = dsp2; } if (t.Searched) { t.PawnAtOpen ??= p; if (t.PawnSpot == null && pawnPos.TryGetValue(p, out var sp2)) t.PawnSpot = sp2; } }
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
                // smashed by you (car, explosion...): the loot spills out and the server names you as the one who destroyed it: counted like an opened chest
                bool smashed = !opened && t.Destroyed && t.Destroyer is uint dd && mine.Contains(dd);
                bool broke = !opened && !smashed && myKills.TryGetValue(t.Actor, out var kt) && (!t.Searched || t.SearchedAt > kt);
                if (smashed) opened = true;
                if (opened) { rec.Opened++; rec.OpenedByType[type] = rec.OpenedByType.GetValueOrDefault(type) + 1; }
                else if (broke) rec.Broken++;
                if (opened || broke)
                {
                    var spot = chanSpot.TryGetValue(t.Actor, out var cs) ? cs : t.PawnSpot;
                    if (spot is { } sp) rec.Spots.Add(new ChestSpot { X = (int)sp.X, Y = (int)sp.Y, Type = type, Broken = !opened, AtSeconds = (int)Math.Max(0, (t.Searched ? t.SearchedAt : t.DestroyedAt) - (firstTime > 0 ? firstTime : 0)), Exact = chanSpot.ContainsKey(t.Actor) });
                }
            }

            double t0 = firstTime > 0 ? firstTime : 0;
            // Every treasure you receive adds a treasure-handler ability to you, at the exact moment: that is the count of gizmos.
            // Their name is only known when you equip one later (before the next treasure), otherwise they stay "Treasure gizmo".
            var myEquips = gizmos.Values.Where(g => g.Holder is uint h && mine.Contains(h)).OrderBy(g => g.At).ToList();
            var myGrants = grants.Where(g => g.Actor == recorder).OrderBy(g => g.At).ToList();
            if (myGrants.Count > 0)
            {
                for (int i = 0; i < myGrants.Count; i++)
                {
                    double at = myGrants[i].At, next = i + 1 < myGrants.Count ? myGrants[i + 1].At : double.MaxValue;
                    if (prizes.Values.Any(pz => pz.Opener is uint o && mine.Contains(o) && Math.Abs(pz.At - at) <= 5)) continue;   // a golden chest prize: listed below
                    var names = myEquips.Where(g => g.At >= at - 1 && g.At < next).Select(g => g.Name).Distinct().ToList();
                    rec.Finds.Add(new TreasureFind { Kind = "gizmo", Name = names.Count == 1 ? names[0] : "Treasure gizmo", Mine = true, AtSeconds = Math.Max(0, at - t0) });
                }
            }
            else   // no grant could be read: fall back on the gizmos you held in hand
                foreach (var g in myEquips.GroupBy(g => g.Name).Select(x => x.First()))
                    rec.Finds.Add(new TreasureFind { Kind = "gizmo", Name = g.Name, Mine = true, AtSeconds = Math.Max(0, g.At - t0) });
            foreach (var p in prizes.Values)
            {
                var (drop, week, name) = TreasureIds.Resolve(p.Id);
                rec.Finds.Add(new TreasureFind { Kind = "prize", Name = name, DropId = drop, Week = week, Mine = p.Opener is uint o && mine.Contains(o), AtSeconds = Math.Max(0, p.At - t0) });
            }
        }
    }
}
