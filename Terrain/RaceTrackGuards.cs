using System.Buffers.Binary;
using System.IO;

namespace LBAAssembler.Terrain;

// People in the cars' way (RaceTrackPlan.Guards; RACEMOD.CPP guard=): the Emperor's palace's own -- the user, 2026-10-13: "The retail game has
// enemies inside the palace, 3 pigs, Stan from Time commando, and lots of soldiers with swords, let's add some of these enemies inside as
// obstacles, perhaps the could chase and attack the cars, or throw random things at them". Each stands at its post on the tiles by a room's
// carpets, an actor of every scene it can be seen from (the engine draws a scene's actors only while it is the one about), its own scripts
// gone: the race-track mode moves and animates it (RACEMOD.CPP Guards) -- a soldier or Stan steps out at a car coming and strikes at it, a
// pighead throws things at it.
//
// Then (the user: "The enemies are too easy to avoid, perhaps making them bigger would help with this?") twice their size: each kind's body
// Size times as big (RaceTrackSmallCars.Scaled: its points, its box, its spheres), given to its entity as body BigBody (Install), and the
// race-track mode told (guard_size=): their reach, their pace and how near a car runs into them all Size times as much.
//
// And the palace dungeon's torturers (the user, 2026-10-17: "Let's add some grobo torturers with bull whips etc."): Grobos with a whip
// (Palace.PalaceTorturers: their body and animations their own, made by its Install), each cracking it at his post, never at a car.
internal static class RaceTrackGuards
{
    public const int Soldier = 0, Pighead = 1, Stan = 2, Torturer = 3;
    public const double Seen = 40;          // (cells: an actor in every scene whose cube comes this near the post)
    public const double Size = 2.0;         // how much bigger than the game's they are
    public const int BigBody = 20;          // the entity's body number for its big body (none of theirs has one so high)

    // A kind's actor and animations, the game's own (the palace scenes' scripts, 151-166 and 80): its entity, its body (the entity's own
    // number for it -- the game's, which BigBody is made from), standing at its post, walking, knocked back, and its strikes (a pighead's:
    // its throw; a torturer's: his whip's crack); the body its actors are drawn with (BigBody but a torturer's).
    public sealed record Look(int Entity, int Body, int Idle, int Walk, int Hit, int[] Strikes, int Drawn = BigBody);

    public static Look Of(int kind, int which) => kind switch
    {
        // (the Emperor's soldier, entity 184, his sword drawn -- body 2, BODY.HQR 261 -- in his fighting stance (434), walking with it (437);
        // his four strikes as his scripts choose among them at random within 900 of Twinsen)
        Soldier => new(184, 2, 434, 437, 6, new[] { 142, 277, 438, 439 }),
        // (the pigheads, entities 313-315: the red one with the hammer, the brown with the garden tool, the yellow with the broom -- at their
        // work as they are found (231, 585, 584), walking, swinging their tool -- here, throwing)
        Pighead => which switch
        {
            0 => new(313, 0, 231, 1, 6, new[] { 142 }),
            1 => new(314, 0, 585, 1, 6, new[] { 142 }),
            _ => new(315, 0, 584, 1, 6, new[] { 142 }),
        },
        // (a torturer: the grey Grobo, entity 336, hooded and his whip in hand -- standing, and the crack)
        Torturer => new(Palace.PalaceTorturers.Entity, 0, Palace.PalaceTorturers.Idle, Palace.PalaceTorturers.Idle, Palace.PalaceTorturers.Idle,
            new[] { Palace.PalaceTorturers.Crack }, Palace.PalaceTorturers.Body),
        // (Stan, come over from Time Commando, entity 316: standing, walking, his three blows)
        _ => new(316, 0, 0, 1, 6, new[] { 142, 277, 587 }),
    };

    // The kinds' looks, every one there is (Install makes each its big body).
    private static readonly Look[] All = { Of(Soldier, 0), Of(Pighead, 0), Of(Pighead, 1), Of(Pighead, 2), Of(Stan, 0) };

    public static string KindName(int kind, int count) => kind switch
    {
        Soldier => count == 1 ? "soldier" : "soldiers",
        Pighead => count == 1 ? "pighead" : "pigheads",
        Torturer => count == 1 ? "torturer" : "torturers",
        _ => "Stan",
    };

    // Each kind's big body: its game body (the entity's own, from the entity table -- RESS.HQR entry 44) Size times as big, at the end of
    // BODY.HQR, and in the entity table as the entity's body BigBody. Returns lines for the build's log.
    public static List<string> Install(string gameDirectory)
    {
        var log = new List<string>();
        var bodyPath = Path.Combine(gameDirectory, "BODY.HQR");
        var ressPath = Path.Combine(gameDirectory, "RESS.HQR");
        var bodies = HqrArchive.Open(bodyPath);
        var bodyFile = File.ReadAllBytes(bodyPath);
        var table = HqrArchive.Open(ressPath).Read(44);
        var next = HqrArchive.CountEntries(bodyPath);
        foreach (var look in All)
        {
            if (BodyOf(table, look.Entity, look.Body) is not { } source)
            {
                log.Add($"the guards: entity {look.Entity} has no body {look.Body} -- left the game's size");
                continue;
            }
            bodyFile = HqrWriter.AppendEntry(bodyFile, HqrWriter.StoredEntry(RaceTrackSmallCars.Scaled(bodies.Read(source), Size)));
            table = RaceTrackBaldinoCar.WithBody(table, look.Entity, BigBody, next);
            log.Add($"the guards: entity {look.Entity}'s body {look.Body} (BODY.HQR {source}) {Size} times as big, BODY.HQR {next}, its body {BigBody}");
            next++;
        }
        File.WriteAllBytes(bodyPath, bodyFile);
        File.WriteAllBytes(ressPath, HqrWriter.ReplaceEntry(File.ReadAllBytes(ressPath), 44, HqrWriter.StoredEntry(table)));
        return log;
    }

    // The BODY.HQR entry an entity's body `generic` is (its body record in the entity table), or null.
    public static int? BodyOf(byte[] table, int entity, int generic)
    {
        var count = BinaryPrimitives.ReadInt32LittleEndian(table) / 4 - 1;
        if (entity < 0 || entity >= count) return null;
        int p = BinaryPrimitives.ReadInt32LittleEndian(table.AsSpan(entity * 4)), end = BinaryPrimitives.ReadInt32LittleEndian(table.AsSpan((entity + 1) * 4));
        while (p < end && p + 4 < table.Length && table[p] != 255)
        {
            var command = table[p];
            if (command == 1 && table[p + 1] == generic) return BinaryPrimitives.ReadInt16LittleEndian(table.AsSpan(p + 3));
            p += command == 3 ? 3 + table[p + 3] : 2 + table[p + 2];
        }
        return null;
    }
}
