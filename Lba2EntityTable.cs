using System.IO;

namespace LBAAssembler;

// LBA2's entity table (RESS.HQR entry 44: a table of U32 offsets, then one record list per entity; not an HQR archive): which bodies (BODY.HQR entries) and animations
// (ANIM.HQR entries) each entity - a kind of actor - has. An actor's body and animation only go together when they belong to the same entity: the body's bones
// are what the entity's animations move. The records (FICHE.CPP, F_BODY = 1 and F_ANIM = 3, ended by 255):
//   body      1, generic number (1 byte), size, BODY.HQR index (S16), ...     the next record starts 2 + size bytes on
//   animation 3, generic number (2 bytes), size, ANIM.HQR index (S16), ...    the next record starts 3 + size bytes on
//   anything else: the command, a one-byte number, a size, ...                 2 + size bytes on
internal sealed class Lba2EntityTable
{
    public sealed record Entity(int Id, IReadOnlyList<(int Generic, int Body)> Bodies, IReadOnlyList<(int Generic, int Anim)> Anims);

    public IReadOnlyList<Entity> Entities { get; }

    private Lba2EntityTable(IReadOnlyList<Entity> entities) => Entities = entities;

    // The table of a game folder, or null when RESS.HQR has none.
    public static Lba2EntityTable? Load(string gameDirectory)
    {
        try
        {
            var path = Path.Combine(gameDirectory, "RESS.HQR");
            return File.Exists(path) ? Parse(HqrArchive.Open(path).Read(44)) : null;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or ArgumentException or UnauthorizedAccessException)
        {
            DebugLog.Log($"Lba2EntityTable: {error.Message}");
            return null;
        }
    }

    public static Lba2EntityTable Parse(byte[] table)
    {
        var entities = new List<Entity>();
        var count = BitConverter.ToInt32(table, 0) / 4 - 1;      // (the first offset is where the records start)
        for (var id = 0; id < count; id++)
        {
            var start = BitConverter.ToInt32(table, id * 4);
            var end = id + 1 < count ? BitConverter.ToInt32(table, (id + 1) * 4) : table.Length;
            var bodies = new List<(int, int)>();
            var anims = new List<(int, int)>();
            for (var p = start; p + 2 < end && table[p] != 255;)
            {
                var command = table[p];
                if (command == 1 && p + 5 <= end) { bodies.Add((table[p + 1], table[p + 3] | table[p + 4] << 8)); p += 2 + table[p + 2]; }
                else if (command == 3 && p + 6 <= end)
                {
                    var hqr = (short)(table[p + 4] | table[p + 5] << 8);
                    if (hqr >= 0) anims.Add((table[p + 1] | table[p + 2] << 8, hqr));
                    p += 3 + table[p + 3];
                }
                else p += command == 3 ? 3 + table[p + 3] : 2 + table[p + 2];
            }
            entities.Add(new Entity(id, bodies, anims));
        }
        return new Lba2EntityTable(entities);
    }

    // The entities that have this BODY.HQR entry as one of their bodies.
    public IEnumerable<Entity> EntitiesWithBody(int bodyIndex) => Entities.Where(e => e.Bodies.Any(b => b.Body == bodyIndex));

    // The BODY.HQR entry the engine draws for a scene's actor of `entity` whose body is `body` -- the number a scene stores is the body's
    // generic number in its entity (InitBody/SearchBody, OBJECT.CPP), and an entity without that number gets its normal body, generic 0. Null
    // when the entity has neither (or isn't in the table).
    public int? BodyOf(int entity, int body)
    {
        if (entity < 0 || entity >= Entities.Count) return null;
        var bodies = Entities[entity].Bodies;
        return bodies.Where(b => b.Generic == body).Select(b => (int?)b.Body).FirstOrDefault()
            ?? bodies.Where(b => b.Generic == 0).Select(b => (int?)b.Body).FirstOrDefault();
    }

    // What a scene's actor stores to be drawn with BODY.HQR entry `bodyIndex`: an entity (a kind of actor) that has it, the body's generic
    // number in it, and an animation of it (its generic number: the standing one, generic 0, else the first). Of the entities with the body,
    // one whose normal body (generic 0) it is first, then one with a standing animation, then the first. Null when no entity has it -- the
    // engine can't put it on an actor. (A raw BODY.HQR index stored as the body of entity 0 -- the LBA2 scene editor's new actors until
    // 2026-10-17 -- is a generic number Twinsen's entity hasn't got: the game drew Twinsen in its place.)
    public (int Entity, int Body, int Anim)? KindForBody(int bodyIndex)
    {
        var owner = EntitiesWithBody(bodyIndex)
            .OrderByDescending(e => e.Bodies.Any(b => b.Body == bodyIndex && b.Generic == 0))
            .ThenByDescending(e => e.Anims.Any(a => a.Generic == 0))
            .ThenBy(e => e.Id)
            .FirstOrDefault();
        if (owner is null) return null;
        var body = owner.Bodies.Where(b => b.Body == bodyIndex).OrderBy(b => b.Generic).First().Generic;
        var anim = owner.Anims.Count == 0 ? 0 : owner.Anims.Any(a => a.Generic == 0) ? 0 : owner.Anims[0].Generic;
        return (owner.Id, body, anim);
    }

    // The animations (ANIM.HQR indices, without repeats, in the table's order) of the entities that have this body, and the one that is each entity's standing
    // still (its generic animation 0), first, when there is one.
    public IReadOnlyList<int> AnimationsOfBody(int bodyIndex, out int? standing)
    {
        var owners = EntitiesWithBody(bodyIndex).ToList();
        standing = owners.SelectMany(e => e.Anims).Where(a => a.Generic == 0).Select(a => (int?)a.Anim).FirstOrDefault();
        return owners.SelectMany(e => e.Anims).Select(a => a.Anim).Distinct().ToList();
    }

    // What the animation list and the chosen animation should be once `body` is picked for an actor that had animation `currentAnim`: the animations of the
    // entities that have the body (only those with as many groups as the body has bones, when it is known and some do), and the current animation kept when it
    // is one of them or otherwise has as many groups as the body has bones, else the body's standing animation (generic animation 0), else the first.
    // Null when no entity has this body (one made with Body Studio, for instance): nothing to go by.
    // Fits is false only when NONE of this entity's own animations have enough groups for the body (a real
    // data mismatch in the archive itself, not a selection bug -- see ChooseAnimations's own comment) -- Chosen
    // is still populated in that case (the closest available, picked the same way as ever), but the caller
    // should not expect the native renderer to actually accept it.
    public sealed record Choice(IReadOnlyList<int> Natural, int Chosen, bool Fits);

    public Choice? ChooseAnimations(int body, int? bones, Func<int, int?> groups, int currentAnim)
    {
        var owned = EntitiesWithBody(body).SelectMany(e => e.Anims).GroupBy(a => a.Anim).Select(g => g.First()).ToList();
        if (owned.Count == 0) return null;
        var fitting = bones is { } n ? owned.Where(a => groups(a.Anim) == n).ToList() : owned;
        // No animation has EXACTLY as many groups as the body has bones (real, confirmed case: BODY.HQR 251,
        // 21 bones, none of its entity's own listed animations happen to have exactly 21 groups) -- falling
        // back to "any of this entity's own animations, fit or not" used to pick one the native renderer's
        // own AnimFitsBody (EXTFUNC.CPP) then rejects outright (animGroups must be >= the body's own group
        // count, since ObjectDisplay's transform loop walks the ANIMATION's own per-group data once per BODY
        // group -- fewer groups than that is an out-of-bounds read, not just a visual mismatch), silently
        // showing "no body to preview for this index" instead of a real render. Prefer any animation that
        // still satisfies that same >= requirement (extra groups beyond the body's own just go unused, same
        // as AnimFitsBody itself treats it) before giving up and using literally anything.
        if (fitting.Count == 0 && bones is { } atLeast) fitting = owned.Where(a => groups(a.Anim) >= atLeast).ToList();
        // Confirmed real case (BODY.HQR 251): even the >= fallback above can come up empty -- this entity's
        // own animations are ALL genuinely too short for this body (251's own tallest is 20 groups, needs 21).
        // No amount of re-selecting among them fixes that; the caller needs to know so it can show an honest
        // message instead of attempting (and failing) a native render with whatever this last-resort tier picks.
        var fits = fitting.Count > 0;
        if (fitting.Count == 0) fitting = owned;
        var natural = fitting.Select(a => a.Anim).ToList();
        var keep = natural.Contains(currentAnim) || bones is { } count && groups(currentAnim) == count;
        var chosen = keep ? currentAnim : fitting.Where(a => a.Generic == 0).Select(a => (int?)a.Anim).FirstOrDefault() ?? natural[0];
        return new Choice(natural, chosen, fits);
    }
}
