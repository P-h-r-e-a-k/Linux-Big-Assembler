using System.Buffers.Binary;
using System.IO;

namespace LBAAssembler.Terrain;

// The race track's jump flight: a longer copy of the retail car jump's (ANIM.HQR entry 51, which Twinsen plays as his generic animation 67 in
// the buggy), added to ANIM.HQR and given to his buggy entity (RESS.HQR entry 44, entity 12) under a generic number of its own, so the retail
// jump in scene 62 keeps its own flight. The flight is all in the animation: 18 keyframes whose root steps carry the car 8990 units forward
// and up to 1921 above where it starts, ending 201 below it, with the "master" bit (no gravity) on keyframes 1-17.
internal static class RaceTrackJumpAnim
{
    public const int RetailEntry = 51;
    // The generic animation number the hero's track script plays (ANIM(200)); the retail entities use numbers up to 83.
    public const int Generic = 200;
    // Each island's jump has a flight of its own, sized to its gap, so several tracks can be built into one folder: 200 plus the island's
    // number (a scene's island byte), 12 more for the track of its other file (Citadel Island's town circuit; Celebration Island's lava
    // lake, RaceTrackIsland.OtherFile), and 24 more for each of a track's jumps after its first (the lava lake has two) -- up to four a
    // track, 200 to 295, the numbers the engine's race-track mode flies at the car's speed (RACEMOD.CPP RACE_JUMP_ANIM_FIRST..LAST).
    public const int TwinOffset = 12, JumpOffset = 24, MaxJumps = 4, Last = Generic + MaxJumps * JumpOffset - 1;
    public static int GenericFor(RaceTrackIsland island, bool twin = false, int jump = 0) =>
        Generic + island.IslandByte + (twin || island.OtherFile ? TwinOffset : 0) + JumpOffset * jump;
    // Twinsen's entity while he drives (behaviour C_BUGGY = 12: the engine loads entity n for behaviour n).
    public const int BuggyEntity = 12;
    // The retail flight's steps (measured in the game: 17.4-17.6 cells): 8990 units forward, ending 201 below where it starts.
    public const double RetailForward = 8990, RetailEndDrop = -201;

    // A copy `forward` times as long climbs a little less than that much higher and takes a little longer, so the arc keeps its look:
    // x1.3 forward is x1.25 up and x1.15 the time.
    public static double ClimbScale(double forward) => 1 + (forward - 1) * 0.85;
    public static double TimeScale(double forward) => 1 + (forward - 1) * 0.5;
    public static double Distance(double forward) => RetailForward * forward / 512;
    public static double EndDrop(double forward) => RetailEndDrop * ClimbScale(forward);
    // The retail flight's keyframes: time (ms), the root's step forward and up (ANIM.HQR 51).
    private static readonly (int Ms, int Forward, int Up)[] RetailSteps =
    {
        (100, 440, 0), (100, 564, 526), (100, 564, 131), (100, 564, 131), (100, 564, 263), (100, 564, 190), (100, 564, 190), (80, 450, 190), (100, 564, 190),
        (100, 564, 55), (100, 564, 55), (100, 564, -244), (100, 564, -122), (100, 564, -122), (100, 333, -332), (100, 333, -506), (100, 333, -615), (100, 333, -181),
    };

    // How long a flight `forward` times the retail one takes, and how high above its start it is `cells` along (the steps are covered
    // evenly over each keyframe).
    public static double Seconds(double forward) => RetailSteps.Sum(f => f.Ms) * TimeScale(forward) / 1000;

    public static double Climb(double forward, double cells)
    {
        var at = cells * 512 / forward; double gone = 0, up = 0;
        foreach (var (_, f, u) in RetailSteps)
        {
            if (gone + f >= at) return (up + u * (at - gone) / f) * ClimbScale(forward);
            gone += f; up += u;
        }
        return up * ClimbScale(forward);
    }

    // The scale that flies `cells` (never shorter than the retail flight, or than `least` of it: a plan's short hops, RaceTrackPlan.JumpMinScale).
    public static double ForwardFor(double cells, double least = 1) => Math.Max(least, Math.Ceiling(cells * 512 / RetailForward * 100) / 100);

    // Adds a flight `forward` times the retail one to the game folder's ANIM.HQR and RESS.HQR, as Twinsen's generic animation `generic` in
    // the buggy (the copies the build starts from are the originals, so this runs once per jump a build has). Returns a line for the build's log.
    public static string Install(string gameDirectory, double forward, int generic = Generic)
    {
        var animPath = Path.Combine(gameDirectory, "ANIM.HQR");
        var ressPath = Path.Combine(gameDirectory, "RESS.HQR");
        var retail = HqrArchive.Open(animPath).Read(RetailEntry);
        var flight = Scale(retail, forward);
        var index = HqrArchive.CountEntries(animPath);
        File.WriteAllBytes(animPath, HqrWriter.AppendEntry(File.ReadAllBytes(animPath), HqrWriter.StoredEntry(flight)));

        var ress = File.ReadAllBytes(ressPath);
        var table = HqrArchive.Open(ressPath).Read(44);
        table = WithAnim(table, BuggyEntity, generic, index);
        File.WriteAllBytes(ressPath, HqrWriter.ReplaceEntry(ress, 44, HqrWriter.StoredEntry(table)));
        return $"jump flight: ANIM.HQR entry {index} (entry {RetailEntry} with the steps forward x{forward:0.00}, the climb x{ClimbScale(forward):0.00} and the time x{TimeScale(forward):0.00}: " +
               $"{Distance(forward):0.0} cells), played by Twinsen in the buggy as animation {generic}";
    }

    // The retail flight with its keyframes' root steps and times scaled. Layout (ANIM.HQR): U16 keyframes, U16 bones, U16 loop frame, U16 0;
    // then per keyframe U16 time (ms), S16 step X, Y, Z, and 8 bytes per bone.
    public static byte[] Scale(byte[] anim, double forward)
    {
        double climb = ClimbScale(forward), time = TimeScale(forward);
        var copy = (byte[])anim.Clone();
        int frames = BinaryPrimitives.ReadUInt16LittleEndian(copy), bones = BinaryPrimitives.ReadUInt16LittleEndian(copy.AsSpan(2));
        for (var f = 0; f < frames; f++)
        {
            var p = 8 + f * (8 + bones * 8);
            if (p + 8 > copy.Length) throw new InvalidDataException("The jump animation is shorter than its header says.");
            void Put(int at, double value) => BinaryPrimitives.WriteInt16LittleEndian(copy.AsSpan(at), (short)Math.Clamp(Math.Round(value), short.MinValue, short.MaxValue));
            BinaryPrimitives.WriteUInt16LittleEndian(copy.AsSpan(p), (ushort)Math.Clamp(Math.Round(BinaryPrimitives.ReadUInt16LittleEndian(copy.AsSpan(p)) * time), 1, ushort.MaxValue));
            Put(p + 2, BinaryPrimitives.ReadInt16LittleEndian(copy.AsSpan(p + 2)) * forward);
            Put(p + 4, BinaryPrimitives.ReadInt16LittleEndian(copy.AsSpan(p + 4)) * climb);
            Put(p + 6, BinaryPrimitives.ReadInt16LittleEndian(copy.AsSpan(p + 6)) * forward);
        }
        return copy;
    }

    // ---- a drop: a flight of its own, off a raised road's end and down onto ground far below (Celebration Island's lava lake: off the
    // mesa's rim, 6300 up, onto the dock, 2026-10-01). The retail flight's arc can't reach that far down, so it is drawn: a hop as steep
    // as the retail one's first climb (clear of the take-off ramp's lip), then from DiveFrom of the way a smooth dive of `drop`, the nose
    // pitched down along the arc and level again at both ends (the engine stands the car level when a flight ends, RACEMOD.CPP).
    public const double DropHop = 1200, DiveFrom = 0.2;

    // How far above its start the drop's flight is, `u` of the way along it (0 to 1), diving `drop` in all.
    public static double DropAt(double u, double drop)
    {
        var v = Math.Clamp((u - DiveFrom) / (1 - DiveFrom), 0, 1);
        return 4 * DropHop * u * (1 - u) - drop * v * v * (3 - 2 * v);
    }

    // Adds a drop's flight -- `cells` long, diving `drop`, flown at heading `beta` -- to the game folder's ANIM.HQR and RESS.HQR as
    // Twinsen's generic animation `generic` in the buggy. Its keyframes are equal steps along the ground; each keyframe's slot 0 carries
    // the master bits (1 the angles are the whole car's, 2 no gravity) and the change of the car's three angles over it, and its step is
    // in the car's own pitched frame. The engine turns a body and its step by M = M(Alpha) M(Gamma) M(Beta) (LIB386/3D/IMATSTDF.CPP:
    // the heading, then Gamma and Alpha about the world's Z and X axes), so a pitch in the car's own frame takes all three angles at a
    // heading that isn't along Z -- the ones RACEMOD.CPP CarPitch gives a loop's car (Alpha alone rolls a car heading along X).
    public static string InstallDrop(string gameDirectory, double cells, double drop, int beta, int generic)
    {
        var animPath = Path.Combine(gameDirectory, "ANIM.HQR");
        var ressPath = Path.Combine(gameDirectory, "RESS.HQR");
        var flight = Drop(HqrArchive.Open(animPath).Read(RetailEntry), cells, drop, beta, out var frames, out var pitch);
        var index = HqrArchive.CountEntries(animPath);
        File.WriteAllBytes(animPath, HqrWriter.AppendEntry(File.ReadAllBytes(animPath), HqrWriter.StoredEntry(flight)));
        var table = WithAnim(HqrArchive.Open(ressPath).Read(44), BuggyEntity, generic, index);
        File.WriteAllBytes(ressPath, HqrWriter.ReplaceEntry(File.ReadAllBytes(ressPath), 44, HqrWriter.StoredEntry(table)));
        return $"drop flight: ANIM.HQR entry {index}, {frames} keyframes over {cells:0.0} cells diving {drop:0} (the nose down {pitch:0} degrees at the most), " +
               $"played by Twinsen in the buggy as animation {generic}";
    }

    // The engine's three angles (4096 a turn) of a car heading `beta` pitched `phi` radians nose down in its own frame: M(Alpha) M(Gamma)
    // M(Beta) = M(beta) M_x(phi).
    public static (int Alpha, int Beta, int Gamma) Pitched(int beta, double phi)
    {
        double b = beta * 2 * Math.PI / 4096, sb = Math.Sin(b), cb = Math.Cos(b), sp = Math.Sin(phi), cp = Math.Cos(phi);
        int Units(double radians) => (int)Math.Round(radians * 4096 / (2 * Math.PI));
        return (Units(Math.Atan2(cb * sp, cp)), Units(Math.Atan2(sb * cp, cb)), Units(Math.Asin(Math.Clamp(-sb * sp, -1, 1))));
    }

    public static byte[] Drop(byte[] retail, double cells, double drop, int beta, out int frames, out double steepest)
    {
        int bones = BinaryPrimitives.ReadUInt16LittleEndian(retail.AsSpan(2));
        var frameSize = 8 + bones * 8;
        var pose = retail.AsSpan(8 + frameSize + 16, (bones - 1) * 8).ToArray();      // (the retail flight's second keyframe's)
        frames = Math.Max(16, (int)Math.Round(cells * 1.5));
        var length = cells * 512;
        double Smooth(double t) { t = Math.Clamp(t, 0, 1); return t * t * (3 - 2 * t); }
        // the pitch, nose down positive: along the arc, eased in from level and back to it over the ends
        double Down(double u)
        {
            const double e = 1e-3;
            var slope = (DropAt(Math.Min(1, u + e), drop) - DropAt(Math.Max(0, u - e), drop)) / ((Math.Min(1, u + e) - Math.Max(0, u - e)) * length);
            return -Math.Atan(slope) * Smooth(u / 0.15) * Smooth((1 - u) / 0.15);
        }
        static short Turn(int from, int to) => (short)((((to - from) % 4096) + 4096 + 2048) % 4096 - 2048);
        var anim = new byte[8 + frames * frameSize];
        BinaryPrimitives.WriteUInt16LittleEndian(anim, (ushort)frames);
        BinaryPrimitives.WriteUInt16LittleEndian(anim.AsSpan(2), (ushort)bones);
        BinaryPrimitives.WriteUInt16LittleEndian(anim.AsSpan(4), (ushort)(frames - 1));
        steepest = 0;
        for (var f = 0; f < frames; f++)
        {
            double u0 = (double)f / frames, u1 = (double)(f + 1) / frames, phi = Down((u0 + u1) / 2);
            double fw = length / frames, up = DropAt(u1, drop) - DropAt(u0, drop);
            steepest = Math.Max(steepest, phi * 180 / Math.PI);
            // the arc's step (forward fw along the heading, up) in the car's own frame, pitched phi nose down
            double cp = Math.Cos(phi), sp = Math.Sin(phi);
            double y = cp * up + sp * fw, z = -sp * up + cp * fw;
            var (a0, b0, g0) = Pitched(beta, Down(u0));
            var (a1, b1, g1) = Pitched(beta, Down(u1));
            var p = 8 + f * frameSize;
            // (the retail flight's pace: 564 units of its arc in 100 ms)
            BinaryPrimitives.WriteUInt16LittleEndian(anim.AsSpan(p), (ushort)Math.Max(1, Math.Round(Math.Sqrt(fw * fw + up * up) * 100 / 564)));
            BinaryPrimitives.WriteInt16LittleEndian(anim.AsSpan(p + 2), 0);
            BinaryPrimitives.WriteInt16LittleEndian(anim.AsSpan(p + 4), (short)Math.Round(y));
            BinaryPrimitives.WriteInt16LittleEndian(anim.AsSpan(p + 6), (short)Math.Round(z));
            BinaryPrimitives.WriteInt16LittleEndian(anim.AsSpan(p + 8), 3);
            BinaryPrimitives.WriteInt16LittleEndian(anim.AsSpan(p + 10), Turn(a0, a1));
            BinaryPrimitives.WriteInt16LittleEndian(anim.AsSpan(p + 12), Turn(b0, b1));
            BinaryPrimitives.WriteInt16LittleEndian(anim.AsSpan(p + 14), Turn(g0, g1));
            pose.CopyTo(anim.AsSpan(p + 16));
        }
        return anim;
    }

    // The entity table (see Lba2EntityTable) with an animation record -- 3, generic number (U16), size 4, ANIM.HQR index (S16), no actions --
    // added to one entity just before its end mark (255), or its index changed when the entity already has that generic number. The entities
    // after it move, so their offsets do too.
    public static byte[] WithAnim(byte[] table, int entity, int generic, int animIndex)
    {
        var count = BinaryPrimitives.ReadInt32LittleEndian(table) / 4 - 1;
        if (entity < 0 || entity >= count) throw new InvalidDataException($"RESS.HQR has no entity {entity}.");
        var start = BinaryPrimitives.ReadInt32LittleEndian(table.AsSpan(entity * 4));
        var end = BinaryPrimitives.ReadInt32LittleEndian(table.AsSpan((entity + 1) * 4));
        var p = start;
        while (p < end && table[p] != 255)
        {
            var command = table[p];
            if (command == 3)
            {
                if ((table[p + 1] | table[p + 2] << 8) == generic)
                {
                    var same = (byte[])table.Clone();
                    BinaryPrimitives.WriteInt16LittleEndian(same.AsSpan(p + 4), (short)animIndex);
                    return same;
                }
                p += 3 + table[p + 3];
            }
            else p += 2 + table[p + 2];
        }
        if (p >= end) throw new InvalidDataException($"Entity {entity}'s records have no end mark.");
        var record = new byte[] { 3, (byte)generic, (byte)(generic >> 8), 4, (byte)animIndex, (byte)(animIndex >> 8), 0 };
        var result = new byte[table.Length + record.Length];
        table.AsSpan(0, p).CopyTo(result);
        record.CopyTo(result.AsSpan(p));
        table.AsSpan(p).CopyTo(result.AsSpan(p + record.Length));
        for (var i = entity + 1; i <= count; i++)
        {
            var at = i * 4;
            if (at + 4 > (count + 1) * 4 || at + 4 > result.Length) break;
            var offset = BinaryPrimitives.ReadInt32LittleEndian(result.AsSpan(at));
            if (offset >= p) BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(at), offset + record.Length);
        }
        return result;
    }
}
