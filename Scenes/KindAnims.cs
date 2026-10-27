using System.IO;

namespace LBAAssembler.Scenes;

// A kind of actor's animations (an entity: LBA2's RESS.HQR entry 44, LBA1's FILE3D.HQR entry), each by the generic number scripts name it
// with (anim(n), the track's ANIM), and what its frames do: the actions FICHE.CPP / FICHE.C run as the animation plays -- a hit on whatever
// the actor touches (F_HIT, F_SUPER_HIT, F_HIT_TWINSEN), something thrown (F_THROW*), a sound, a step. Which animations hit is what makes a
// kind of actor able to attack: the behaviours (ActorBehaviours) pick its walking and attacking animations from here, and describe a script
// as attacking when its track plays one.
//   LBA2 record: 3, generic (U16), size, ANIM.HQR index (S16), number of actions, the actions -- the next record 3 + size bytes on
//   LBA1 record: 3, generic (U8),  size, ANIM.HQR index (U16), number of actions, the actions -- the next record 2 + size bytes on
//   (a body: 1, generic (U8), size, ...; anything else: command, a byte, size, ...)
internal sealed class KindAnims
{
    // Generic animation numbers (DEFINES.H GEN_ANIM_*, the same in both games for these)
    public const int Stand = 0, Walk = 1, Back = 2, Talk = 28;

    // Action codes (COMMON.H F_*)
    private const byte FHit = 5, FHitTwinsen = 17, FSuperHit = 22;
    private static readonly HashSet<byte> Throws = new() { 8, 9, 11, 12, 18, 19, 20, 21, 23, 27, 30, 41, 42 };

    // Bytes after an action's code (the frame byte included), per FICHE.CPP's / FICHE.C's cases.
    private static readonly Dictionary<byte, int> Lba2Sizes = new()
    {
        [5] = 2, [6] = 3, [7] = 5, [8] = 12, [9] = 9, [10] = 10, [11] = 8, [12] = 12, [13] = 3, [14] = 13, [15] = 1, [16] = 1, [17] = 1,
        [18] = 16, [19] = 16, [20] = 12, [21] = 13, [22] = 10, [23] = 17, [26] = 8, [27] = 9, [28] = 2, [29] = 1, [30] = 8, [31] = 1, [32] = 2,
        [33] = 1, [34] = 1, [35] = 0, [36] = 4, [37] = 1, [38] = 1, [39] = 8, [40] = 9, [41] = 9, [42] = 1,
    };
    private static readonly Dictionary<byte, int> Lba1Sizes = new()
    {
        [5] = 2, [6] = 3, [7] = 5, [8] = 12, [9] = 9, [10] = 5, [11] = 7, [12] = 12, [13] = 3, [15] = 1, [16] = 1, [17] = 1,
        [18] = 16, [19] = 16, [20] = 12, [21] = 13,
    };

    // generic animation number -> the action codes its frames carry (empty: none)
    public IReadOnlyDictionary<int, IReadOnlyList<byte>> Anims { get; }
    // generic animations whose actions couldn't be read to the end (an action code this doesn't know the size of)
    public IReadOnlySet<int> Unread { get; }

    private KindAnims(Dictionary<int, IReadOnlyList<byte>> anims, HashSet<int> unread) { Anims = anims; Unread = unread; }

    public bool Has(int generic) => Anims.ContainsKey(generic);
    public bool Hits(int generic) => Anims.TryGetValue(generic, out var a) && a.Any(c => c is FHit or FHitTwinsen or FSuperHit);
    public bool ThrowsSomething(int generic) => Anims.TryGetValue(generic, out var a) && a.Any(Throws.Contains);

    // The animation to attack with: the punches first (GEN_ANIM_COUP_1..3), then any that hits, then any that throws; null when none does.
    public int? Attack
    {
        get
        {
            foreach (var g in new[] { 17, 18, 19 }) if (Hits(g)) return g;
            foreach (var g in Anims.Keys.Order()) if (g > Talk && Hits(g)) return g;
            foreach (var g in Anims.Keys.Order()) if (Hits(g) && g is not (5 or 6 or 7 or 22)) return g;
            foreach (var g in Anims.Keys.Order()) if (ThrowsSomething(g) && g is not (5 or 6 or 7 or 22)) return g;
            return null;
        }
    }

    // The kinds of actor of a game folder (null when it has no entity table), cached by folder and file time.
    private static readonly Dictionary<string, (DateTime Stamp, List<KindAnims?> Kinds)> cache = new(StringComparer.OrdinalIgnoreCase);

    public static KindAnims? Of(SceneGame game, string directory, int entity)
    {
        var kinds = All(game, directory);
        return kinds is not null && entity >= 0 && entity < kinds.Count ? kinds[entity] : null;
    }

    public static IReadOnlyList<KindAnims?>? All(SceneGame game, string directory)
    {
        var path = Path.Combine(directory, game == SceneGame.Lba1 ? "FILE3D.HQR" : "RESS.HQR");
        if (!File.Exists(path)) return null;
        var stamp = File.GetLastWriteTimeUtc(path);
        lock (cache)
        {
            if (cache.TryGetValue(path, out var hit) && hit.Stamp == stamp) return hit.Kinds;
            List<KindAnims?> kinds;
            try { kinds = game == SceneGame.Lba1 ? Lba1(HqrArchive.Open(path)) : Lba2(HqrArchive.Open(path).Read(44)); }
            catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException or IndexOutOfRangeException)
            {
                DebugLog.Log($"KindAnims: {path}: {e.Message}");
                return null;
            }
            cache[path] = (stamp, kinds);
            return kinds;
        }
    }

    private static List<KindAnims?> Lba2(byte[] table)
    {
        var kinds = new List<KindAnims?>();
        var count = BitConverter.ToInt32(table, 0) / 4 - 1;
        for (var id = 0; id < count; id++)
        {
            var start = BitConverter.ToInt32(table, id * 4);
            var end = id + 1 < count ? BitConverter.ToInt32(table, (id + 1) * 4) : table.Length;
            kinds.Add(Parse(table, start, end, lba1: false));
        }
        return kinds;
    }

    private static List<KindAnims?> Lba1(HqrArchive file3d)
    {
        var kinds = new List<KindAnims?>();
        for (var id = 0; id < file3d.Count; id++)
            kinds.Add(file3d.IsValid(id) ? Parse(file3d.Read(id), 0, file3d.Read(id).Length, lba1: true) : null);
        return kinds;
    }

    private static KindAnims Parse(byte[] d, int start, int end, bool lba1)
    {
        var anims = new Dictionary<int, IReadOnlyList<byte>>();
        var unread = new HashSet<int>();
        var sizes = lba1 ? Lba1Sizes : Lba2Sizes;
        var p = start;
        while (p + 2 < end && d[p] != 255)
        {
            var command = d[p];
            int sizeAt = command == 3 && !lba1 ? p + 3 : p + 2;     // (LBA2's animation records have a 2-byte generic number)
            if (sizeAt >= end) break;
            var next = sizeAt + Math.Max((int)d[sizeAt], 1);
            if (command == 3)
            {
                var generic = lba1 ? d[p + 1] : d[p + 1] | d[p + 2] << 8;
                var actions = new List<byte>();
                var at = sizeAt + 3;                                     // (past the size and the ANIM.HQR index)
                var count = at < end ? d[at++] : 0;
                for (var n = 0; n < count; n++)
                {
                    if (at >= next || !sizes.TryGetValue(d[at], out var size)) { unread.Add(generic); break; }
                    actions.Add(d[at]);
                    at += 1 + size;
                }
                if (at > next) unread.Add(generic);
                anims.TryAdd(generic, actions);
            }
            p = next;
        }
        return new KindAnims(anims, unread);
    }
}
